using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Volumo.Api.Auth;
using Volumo.Api.Tests.Support;
using Volumo.Infrastructure.Identity;
using Volumo.Shared.Autenticacao;
using static Volumo.Api.Tests.Support.ContasDeTeste;

namespace Volumo.Api.Tests.Auth;

public sealed class DoisFatoresTests(VolumoApiFactory factory) : IClassFixture<VolumoApiFactory>
{
    private const string RotaDePersonal = "/__test/politicas/personal";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Personal_sem_2fa_entra_mas_nao_acessa_rotas_de_personal()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var email = await PersonalConfirmadoAsync(client, factory);
        var (token, _) = await EntrarAsync(client, email);

        using var conta = await GetContaAsync(client, token);
        using var rota = await EnviarAsync(client, HttpMethod.Get, RotaDePersonal, token);

        var dados = await conta.Content.ReadFromJsonAsync<DadosDaConta>(Ct);
        dados!.PrecisaConfigurarDoisFatores.ShouldBeTrue();
        dados.DoisFatoresAtivo.ShouldBeFalse();
        rota.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Ativar_2fa_troca_a_sessao_por_uma_com_segundo_fator()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var email = await PersonalConfirmadoAsync(client, factory);
        var (token, cookieAntigo) = await EntrarAsync(client, email);

        var (_, codigos, tokenNovo, cookieNovo) = await AtivarDoisFatoresAsync(client, token, cookieAntigo);

        codigos.Count.ShouldBe(10);
        codigos.Distinct().Count().ShouldBe(10);
        (await EnviarAsync(client, HttpMethod.Get, RotaDePersonal, tokenNovo)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RefreshAsync(client, cookieAntigo)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "a sessão sem 2FA foi encerrada");

        // A renovação mantém o segundo fator no token novo.
        using var renovacao = await RefreshAsync(client, cookieNovo);
        var renovada = await renovacao.Content.ReadFromJsonAsync<RespostaDeLogin>(Ct);
        (await EnviarAsync(client, HttpMethod.Get, RotaDePersonal, renovada!.TokenDeAcesso)).StatusCode.ShouldBe(HttpStatusCode.OK);
        using var conta = await GetContaAsync(client, renovada.TokenDeAcesso);
        (await conta.Content.ReadFromJsonAsync<DadosDaConta>(Ct))!.DoisFatoresAtivo.ShouldBeTrue();
    }

    [Fact]
    public async Task Ativar_com_codigo_errado_ou_sem_iniciar_falha()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var email = await PersonalConfirmadoAsync(client, factory);
        var (token, _) = await EntrarAsync(client, email);

        using var semIniciar = await EnviarAsync(client, HttpMethod.Post, "/auth/2fa/ativar", token, corpo: new AtivacaoDeDoisFatores("123456"));
        (await EnviarAsync(client, HttpMethod.Post, "/auth/2fa/iniciar", token)).Dispose();
        using var codigoErrado = await EnviarAsync(client, HttpMethod.Post, "/auth/2fa/ativar", token, corpo: new AtivacaoDeDoisFatores("000000"));

        semIniciar.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        codigoErrado.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var conta = await GetContaAsync(client, token);
        (await conta.Content.ReadFromJsonAsync<DadosDaConta>(Ct))!.DoisFatoresAtivo.ShouldBeFalse();
    }

    [Fact]
    public async Task Configurar_de_novo_com_2fa_ativo_responde_409()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var email = await PersonalConfirmadoAsync(client, factory);
        var (token, _) = await EntrarAsync(client, email);
        var (_, _, tokenComDoisFatores, _) = await AtivarDoisFatoresAsync(client, token);

        using var response = await EnviarAsync(client, HttpMethod.Post, "/auth/2fa/iniciar", tokenComDoisFatores);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Com_2fa_ativo_a_senha_sozinha_so_devolve_um_desafio()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var email = await PersonalComDoisFatoresAsync(client);

        using var login = await LoginAsync(client, email.Email);

        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await login.Content.ReadFromJsonAsync<RespostaDeLogin>(Ct);
        corpo!.Requer2Fa.ShouldBeTrue();
        corpo.TokenDeAcesso.ShouldBeNull();
        SetCookieDeAtualizacao(login).ShouldBeNull();
    }

    [Fact]
    public async Task Segunda_etapa_com_codigo_certo_abre_sessao_com_segundo_fator()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var conta = await PersonalComDoisFatoresAsync(client);
        var desafio = await DesafioAsync(client, conta.Email);

        using var response = await client.PostAsJsonAsync("/auth/login/2fa", new SegundaEtapaDoLogin(desafio, Totp.Codigo(conta.Chave)), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CookieDeAtualizacao(response).ShouldNotBeNull();
        var sessao = await response.Content.ReadFromJsonAsync<RespostaDeLogin>(Ct);
        (await EnviarAsync(client, HttpMethod.Get, RotaDePersonal, sessao!.TokenDeAcesso)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Codigo_errado_ou_desafio_adulterado_responde_401()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var conta = await PersonalComDoisFatoresAsync(client);
        var desafio = await DesafioAsync(client, conta.Email);

        using var codigoErrado = await client.PostAsJsonAsync("/auth/login/2fa", new SegundaEtapaDoLogin(desafio, "000000"), Ct);
        using var adulterado = await client.PostAsJsonAsync("/auth/login/2fa", new SegundaEtapaDoLogin(desafio[..^3] + "abc", Totp.Codigo(conta.Chave)), Ct);
        using var inventado = await client.PostAsJsonAsync("/auth/login/2fa", new SegundaEtapaDoLogin("desafio-inventado", Totp.Codigo(conta.Chave)), Ct);

        codigoErrado.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        adulterado.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        inventado.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Cinco_codigos_errados_bloqueiam_a_conta()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var conta = await PersonalComDoisFatoresAsync(client);
        var desafio = await DesafioAsync(client, conta.Email);
        for (var i = 0; i < 5; i++)
        {
            (await client.PostAsJsonAsync("/auth/login/2fa", new SegundaEtapaDoLogin(desafio, "000000"), Ct)).Dispose();
        }

        using var codigoCerto = await client.PostAsJsonAsync("/auth/login/2fa", new SegundaEtapaDoLogin(desafio, Totp.Codigo(conta.Chave)), Ct);
        using var novoLogin = await LoginAsync(client, conta.Email);

        codigoCerto.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        novoLogin.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Codigo_de_recuperacao_entra_uma_unica_vez()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var conta = await PersonalComDoisFatoresAsync(client);
        var codigo = conta.Codigos[0];

        using var primeira = await client.PostAsJsonAsync(
            "/auth/login/recuperacao", new LoginComCodigoDeRecuperacao(await DesafioAsync(client, conta.Email), codigo), Ct);
        using var segunda = await client.PostAsJsonAsync(
            "/auth/login/recuperacao", new LoginComCodigoDeRecuperacao(await DesafioAsync(client, conta.Email), codigo), Ct);

        primeira.StatusCode.ShouldBe(HttpStatusCode.OK);
        segunda.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Codigo_de_recuperacao_digitado_sem_hifen_e_em_minusculas_funciona()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var conta = await PersonalComDoisFatoresAsync(client);
        var digitado = conta.Codigos[1].Replace("-", " ", StringComparison.Ordinal).ToLowerInvariant();

        using var response = await client.PostAsJsonAsync(
            "/auth/login/recuperacao", new LoginComCodigoDeRecuperacao(await DesafioAsync(client, conta.Email), digitado), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Novos_codigos_de_recuperacao_exigem_sessao_com_2fa_e_invalidam_os_antigos()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var email = await PersonalConfirmadoAsync(client, factory);
        var (tokenSemDoisFatores, _) = await EntrarAsync(client, email);
        using (var negado = await EnviarAsync(client, HttpMethod.Post, "/auth/2fa/codigos-de-recuperacao", tokenSemDoisFatores))
        {
            negado.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        var (_, antigos, tokenComDoisFatores, _) = await AtivarDoisFatoresAsync(client, tokenSemDoisFatores);
        using var novos = await EnviarAsync(client, HttpMethod.Post, "/auth/2fa/codigos-de-recuperacao", tokenComDoisFatores);
        novos.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var comAntigo = await client.PostAsJsonAsync(
            "/auth/login/recuperacao", new LoginComCodigoDeRecuperacao(await DesafioAsync(client, email), antigos[0]), Ct);
        comAntigo.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Aparelho_lembrado_entra_so_com_a_senha_e_com_segundo_fator()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var conta = await PersonalComDoisFatoresAsync(client);
        var aparelho = await LembrarAparelhoAsync(client, conta);

        using var login = await LoginComAparelhoAsync(client, conta.Email, aparelho);

        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var sessao = await login.Content.ReadFromJsonAsync<RespostaDeLogin>(Ct);
        sessao!.Requer2Fa.ShouldBeFalse();
        CookieDeAtualizacao(login).ShouldNotBeNull();
        (await EnviarAsync(client, HttpMethod.Get, RotaDePersonal, sessao.TokenDeAcesso)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Sem_pedir_para_lembrar_nao_grava_o_cookie_do_aparelho()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var conta = await PersonalComDoisFatoresAsync(client);

        using var response = await client.PostAsJsonAsync("/auth/login/2fa", new SegundaEtapaDoLogin(await DesafioAsync(client, conta.Email), Totp.Codigo(conta.Chave)), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CookieDoAparelho(response).ShouldBeNull();
    }

    [Fact]
    public async Task Cookie_do_aparelho_grava_com_atributos_seguros()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var conta = await PersonalComDoisFatoresAsync(client);

        using var response = await client.PostAsJsonAsync(
            "/auth/login/2fa", new SegundaEtapaDoLogin(await DesafioAsync(client, conta.Email), Totp.Codigo(conta.Chave), LembrarAparelho: true), Ct);

        var linha = response.Headers.GetValues("Set-Cookie").Single(v => v.StartsWith(TrustedDeviceCookie.Name + "=", StringComparison.Ordinal)).ToLowerInvariant();
        linha.ShouldContain("httponly");
        linha.ShouldContain("secure");
        linha.ShouldContain("samesite=strict");
        linha.ShouldContain("path=/auth");
        linha.ShouldContain("expires=");
    }

    [Fact]
    public async Task Aparelho_lembrado_continua_exigindo_a_senha_certa()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var conta = await PersonalComDoisFatoresAsync(client);
        var aparelho = await LembrarAparelhoAsync(client, conta);

        using var login = await LoginComAparelhoAsync(client, conta.Email, aparelho, senha: "senha errada de teste");

        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        CookieDeAtualizacao(login).ShouldBeNull();
    }

    [Fact]
    public async Task Cookie_de_aparelho_de_outra_conta_ou_adulterado_pede_o_codigo()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var outra = await PersonalComDoisFatoresAsync(client);
        var aparelhoDaOutra = await LembrarAparelhoAsync(client, outra);
        var conta = await PersonalComDoisFatoresAsync(client);

        using var comOutro = await LoginComAparelhoAsync(client, conta.Email, aparelhoDaOutra);
        using var adulterado = await LoginComAparelhoAsync(client, outra.Email, aparelhoDaOutra[..^3] + "abc");

        (await comOutro.Content.ReadFromJsonAsync<RespostaDeLogin>(Ct))!.Requer2Fa.ShouldBeTrue();
        (await adulterado.Content.ReadFromJsonAsync<RespostaDeLogin>(Ct))!.Requer2Fa.ShouldBeTrue();
    }

    [Fact]
    public async Task Mudanca_de_seguranca_na_conta_esquece_os_aparelhos()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var conta = await PersonalComDoisFatoresAsync(client);
        var aparelho = await LembrarAparelhoAsync(client, conta);

        // Mesmo efeito de trocar a senha, refazer o 2FA ou suspender a conta.
        using (var escopo = factory.Services.CreateScope())
        {
            var userManager = escopo.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
            (await userManager.UpdateSecurityStampAsync((await userManager.FindByEmailAsync(conta.Email))!)).Succeeded.ShouldBeTrue();
        }

        using var login = await LoginComAparelhoAsync(client, conta.Email, aparelho);

        (await login.Content.ReadFromJsonAsync<RespostaDeLogin>(Ct))!.Requer2Fa.ShouldBeTrue();
    }

    private static async Task<string> LembrarAparelhoAsync(HttpClient client, (string Email, string Chave, IReadOnlyList<string> Codigos) conta)
    {
        using var response = await client.PostAsJsonAsync(
            "/auth/login/2fa", new SegundaEtapaDoLogin(await DesafioAsync(client, conta.Email), Totp.Codigo(conta.Chave), LembrarAparelho: true), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return CookieDoAparelho(response)!;
    }

    private static async Task<HttpResponseMessage> LoginComAparelhoAsync(HttpClient client, string email, string cookieDoAparelho, string senha = SenhaValida)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/login") { Content = JsonContent.Create(new PedidoDeLogin(email, senha)) };
        request.Headers.Add("Cookie", $"{TrustedDeviceCookie.Name}={cookieDoAparelho}");
        return await client.SendAsync(request, Ct);
    }

    private static string? CookieDoAparelho(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var valores)
            ? valores.Where(v => v.StartsWith(TrustedDeviceCookie.Name + "=", StringComparison.Ordinal)).Select(v => v[(TrustedDeviceCookie.Name.Length + 1)..].Split(';')[0]).FirstOrDefault()
            : null;

    private async Task<(string Email, string Chave, IReadOnlyList<string> Codigos)> PersonalComDoisFatoresAsync(HttpClient client)
    {
        var email = await PersonalConfirmadoAsync(client, factory);
        var (token, _) = await EntrarAsync(client, email);
        var (chave, codigos, _, _) = await AtivarDoisFatoresAsync(client, token);
        return (email, chave, codigos);
    }

    private static async Task<string> DesafioAsync(HttpClient client, string email)
    {
        using var login = await LoginAsync(client, email);
        var corpo = await login.Content.ReadFromJsonAsync<RespostaDeLogin>(Ct);
        return corpo!.Desafio2Fa!;
    }
}
