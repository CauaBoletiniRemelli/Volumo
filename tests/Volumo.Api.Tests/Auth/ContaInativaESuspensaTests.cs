using System.Net;
using System.Net.Http.Json;
using Volumo.Api.Tests.Support;
using Volumo.Shared.Admin;
using Volumo.Shared.Autenticacao;
using static Volumo.Api.Tests.Support.ContasDeTeste;

namespace Volumo.Api.Tests.Auth;

/// <summary>Conta pausada pela própria pessoa (reversível por ela) e suspensa pela plataforma (só Admin desfaz).</summary>
public sealed class ContaInativaESuspensaTests(VolumoApiFactory factory) : IClassFixture<VolumoApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Inativar_exige_a_senha_e_encerra_as_sessoes()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var (emailAluno, _, _, _) = await AlunoViaConviteAsync(client, factory);
        var (token, cookie) = await EntrarAsync(client, emailAluno);

        using var senhaErrada = await EnviarAsync(client, HttpMethod.Post, "/conta/inativar", token, corpo: new ConfirmacaoDeSenha("senha errada qualquer"));
        using var certa = await EnviarAsync(client, HttpMethod.Post, "/conta/inativar", token, corpo: new ConfirmacaoDeSenha(SenhaValida));

        senhaErrada.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        certa.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await RefreshAsync(client, cookie)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Conta_inativa_entra_mas_so_consegue_reativar()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var (emailAluno, tokenAluno, _, _) = await AlunoViaConviteAsync(client, factory);
        (await EnviarAsync(client, HttpMethod.Post, "/conta/inativar", tokenAluno, corpo: new ConfirmacaoDeSenha(SenhaValida))).Dispose();

        var (tokenInativo, _) = await EntrarAsync(client, emailAluno);
        var conta = await (await GetContaAsync(client, tokenInativo)).Content.ReadFromJsonAsync<DadosDaConta>(Ct);
        conta!.ContaInativa.ShouldBeTrue();
        (await EnviarAsync(client, HttpMethod.Get, "/__test/politicas/aluno", tokenInativo)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var reativacao = await EnviarAsync(client, HttpMethod.Post, "/conta/reativar", tokenInativo);

        reativacao.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tokenNovo = (await reativacao.Content.ReadFromJsonAsync<RespostaDeLogin>(Ct))!.TokenDeAcesso;
        (await EnviarAsync(client, HttpMethod.Get, "/__test/politicas/aluno", tokenNovo)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Personal_inativo_reativado_mantem_o_2fa_na_sessao()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var (_, _, token) = await PersonalComDoisFatoresAsync(client, factory);
        (await EnviarAsync(client, HttpMethod.Post, "/conta/inativar", token, corpo: new ConfirmacaoDeSenha(SenhaValida))).Dispose();

        using var reativacao = await EnviarAsync(client, HttpMethod.Post, "/conta/reativar", token);

        var tokenNovo = (await reativacao.Content.ReadFromJsonAsync<RespostaDeLogin>(Ct))!.TokenDeAcesso;
        (await EnviarAsync(client, HttpMethod.Get, "/__test/politicas/personal", tokenNovo)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_suspende_e_a_conta_perde_o_acesso_ate_remover_a_suspensao()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var tokenAdmin = await AdminAsync(client);
        var emailAlvo = await PersonalConfirmadoAsync(client, factory);
        var (_, cookieDoAlvo) = await EntrarAsync(client, emailAlvo);
        var alvoId = await IdDoUsuarioAsync(emailAlvo);

        using var suspensao = await EnviarAsync(client, HttpMethod.Post, $"/admin/usuarios/{alvoId}/suspender", tokenAdmin, corpo: new Suspensao("CREF falso"));

        suspensao.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await RefreshAsync(client, cookieDoAlvo)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var loginCerto = await LoginAsync(client, emailAlvo);
        using var loginErrado = await LoginAsync(client, emailAlvo, "senha errada qualquer");
        loginCerto.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await loginCerto.Content.ReadAsStringAsync(Ct)).ShouldContain("suspensa");
        loginErrado.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "quem não sabe a senha não fica sabendo da suspensão");

        (await EnviarAsync(client, HttpMethod.Post, $"/admin/usuarios/{alvoId}/remover-suspensao", tokenAdmin)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await LoginAsync(client, emailAlvo)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Suspensao_derruba_tambem_o_login_pela_metade_com_2fa()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var tokenAdmin = await AdminAsync(client);
        var (emailAlvo, chave, _) = await PersonalComDoisFatoresAsync(client, factory);
        using var primeiraEtapa = await LoginAsync(client, emailAlvo);
        var desafio = (await primeiraEtapa.Content.ReadFromJsonAsync<RespostaDeLogin>(Ct))!.Desafio2Fa!;

        (await EnviarAsync(client, HttpMethod.Post, $"/admin/usuarios/{await IdDoUsuarioAsync(emailAlvo)}/suspender", tokenAdmin, corpo: new Suspensao("Abuso"))).Dispose();

        using var segundaEtapa = await client.PostAsJsonAsync("/auth/login/2fa", new SegundaEtapaDoLogin(desafio, Totp.Codigo(chave)), Ct);
        segundaEtapa.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Matriz_de_autorizacao_da_suspensao()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var tokenAdmin = await AdminAsync(client);
        var emailAdminSemDoisFatores = await PersonalConfirmadoAsync(client, factory);
        await TornarAdminAsync(factory, emailAdminSemDoisFatores);
        var (tokenAdminSemDoisFatores, _) = await EntrarAsync(client, emailAdminSemDoisFatores);
        var (_, _, tokenPersonal) = await PersonalComDoisFatoresAsync(client, factory);
        var alvoId = await IdDoUsuarioAsync(await PersonalConfirmadoAsync(client, factory));
        var rota = $"/admin/usuarios/{alvoId}/suspender";
        var corpo = new Suspensao("Teste");

        (await EnviarAsync(client, HttpMethod.Post, rota, null, corpo: corpo)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await EnviarAsync(client, HttpMethod.Post, rota, tokenPersonal, corpo: corpo)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await EnviarAsync(client, HttpMethod.Post, rota, tokenAdminSemDoisFatores, corpo: corpo)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await EnviarAsync(client, HttpMethod.Post, $"/admin/usuarios/{Guid.CreateVersion7()}/suspender", tokenAdmin, corpo: corpo))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Admin_nao_suspende_a_si_mesmo()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var email = await PersonalConfirmadoAsync(client, factory);
        await TornarAdminAsync(factory, email);
        var (token, _) = await EntrarAsync(client, email);
        var (_, _, tokenAdmin, _) = await AtivarDoisFatoresAsync(client, token);

        using var response = await EnviarAsync(
            client, HttpMethod.Post, $"/admin/usuarios/{await IdDoUsuarioAsync(email)}/suspender", tokenAdmin, corpo: new Suspensao("Teste"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>Admin com 2FA (o papel é dado direto na plataforma; o 2FA é ativado pela API).</summary>
    private async Task<string> AdminAsync(HttpClient client)
    {
        var email = await PersonalConfirmadoAsync(client, factory);
        await TornarAdminAsync(factory, email);
        var (token, _) = await EntrarAsync(client, email);
        var (_, _, tokenComDoisFatores, _) = await AtivarDoisFatoresAsync(client, token);
        return tokenComDoisFatores;
    }
}
