using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Volumo.Api.Tests.Support;
using Volumo.Shared.Autenticacao;
using static Volumo.Api.Tests.Support.ContasDeTeste;

namespace Volumo.Api.Tests.Auth;

public sealed partial class RecuperacaoDeSenhaTests(VolumoApiFactory factory) : IClassFixture<VolumoApiFactory>
{
    private const string NovaSenha = "outra senha bem forte";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Esqueci_a_senha_responde_202_sempre_e_so_envia_para_quem_tem_conta()
    {
        using var client = factory.CreateHttpsClient();
        var email = await PersonalConfirmadoAsync(client, factory);
        var inexistente = NovoEmail();

        using var r1 = await PedirRecuperacaoAsync(client, email);
        using var r2 = await PedirRecuperacaoAsync(client, inexistente);

        r1.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        r2.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        factory.Emails.Para(email).Last().Assunto.ShouldContain("Redefinir");
        factory.Emails.Para(inexistente).ShouldBeEmpty();
    }

    [Fact]
    public async Task Redefinir_troca_a_senha_derruba_as_sessoes_e_avisa_por_email()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var email = await PersonalConfirmadoAsync(client, factory);
        var (_, sessaoAberta) = await EntrarAsync(client, email);
        (await PedirRecuperacaoAsync(client, email)).Dispose();
        var (usuarioId, token) = LinkDeRedefinicao(email);

        using var response = await client.PostAsJsonAsync("/auth/redefinir-senha", new RedefinicaoDeSenha(usuarioId, token, NovaSenha), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await LoginAsync(client, email)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await LoginAsync(client, email, NovaSenha)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RefreshAsync(client, sessaoAberta)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        factory.Emails.Para(email).Last().Assunto.ShouldContain("alterada");
    }

    [Fact]
    public async Task Link_de_redefinicao_vale_uma_vez_so()
    {
        using var client = factory.CreateHttpsClient();
        var email = await PersonalConfirmadoAsync(client, factory);
        (await PedirRecuperacaoAsync(client, email)).Dispose();
        var (usuarioId, token) = LinkDeRedefinicao(email);
        (await client.PostAsJsonAsync("/auth/redefinir-senha", new RedefinicaoDeSenha(usuarioId, token, NovaSenha), Ct)).Dispose();

        using var reuso = await client.PostAsJsonAsync("/auth/redefinir-senha", new RedefinicaoDeSenha(usuarioId, token, "terceira senha forte"), Ct);

        reuso.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Token_invalido_ou_senha_fraca_responde_400()
    {
        using var client = factory.CreateHttpsClient();
        var email = await PersonalConfirmadoAsync(client, factory);
        (await PedirRecuperacaoAsync(client, email)).Dispose();
        var (usuarioId, token) = LinkDeRedefinicao(email);

        using var adulterado = await client.PostAsJsonAsync("/auth/redefinir-senha", new RedefinicaoDeSenha(usuarioId, token[..^4] + "AAAA", NovaSenha), Ct);
        using var outroUsuario = await client.PostAsJsonAsync("/auth/redefinir-senha", new RedefinicaoDeSenha(Guid.CreateVersion7(), token, NovaSenha), Ct);
        using var senhaFraca = await client.PostAsJsonAsync("/auth/redefinir-senha", new RedefinicaoDeSenha(usuarioId, token, "curta"), Ct);

        adulterado.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        outroUsuario.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        senhaFraca.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await senhaFraca.Content.ReadFromJsonAsync<ValidationProblemDetails>(Ct))!.Errors.Keys.ShouldContain("NovaSenha");
        (await LoginAsync(client, email)).StatusCode.ShouldBe(HttpStatusCode.OK, "a senha antiga continua valendo");
    }

    [Fact]
    public async Task Redefinir_desbloqueia_a_conta()
    {
        using var client = factory.CreateHttpsClient();
        var email = await PersonalConfirmadoAsync(client, factory);
        for (var i = 0; i < 5; i++)
        {
            (await LoginAsync(client, email, "senha errada qualquer")).Dispose();
        }

        (await PedirRecuperacaoAsync(client, email)).Dispose();
        var (usuarioId, token) = LinkDeRedefinicao(email);
        (await client.PostAsJsonAsync("/auth/redefinir-senha", new RedefinicaoDeSenha(usuarioId, token, NovaSenha), Ct)).Dispose();

        (await LoginAsync(client, email, NovaSenha)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Redefinir_a_senha_nao_dispensa_o_2fa_e_invalida_desafios_pendentes()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var email = await PersonalConfirmadoAsync(client, factory);
        var (token, _) = await EntrarAsync(client, email);
        var (chave, _, _, _) = await AtivarDoisFatoresAsync(client, token);
        using var loginAntigo = await LoginAsync(client, email);
        var desafioAntigo = (await loginAntigo.Content.ReadFromJsonAsync<RespostaDeLogin>(Ct))!.Desafio2Fa!;

        (await PedirRecuperacaoAsync(client, email)).Dispose();
        var (usuarioId, tokenDeRedefinicao) = LinkDeRedefinicao(email);
        (await client.PostAsJsonAsync("/auth/redefinir-senha", new RedefinicaoDeSenha(usuarioId, tokenDeRedefinicao, NovaSenha), Ct)).Dispose();

        using var loginNovo = await LoginAsync(client, email, NovaSenha);
        (await loginNovo.Content.ReadFromJsonAsync<RespostaDeLogin>(Ct))!.Requer2Fa.ShouldBeTrue();
        using var comDesafioAntigo = await client.PostAsJsonAsync("/auth/login/2fa", new SegundaEtapaDoLogin(desafioAntigo, Totp.Codigo(chave)), Ct);
        comDesafioAntigo.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static Task<HttpResponseMessage> PedirRecuperacaoAsync(HttpClient client, string email) =>
        client.PostAsJsonAsync("/auth/esqueci-senha", new PedidoDeRecuperacaoDeSenha(email, VerificacaoHumanaFalsa.TokenAprovado), Ct);

    private (Guid UsuarioId, string Token) LinkDeRedefinicao(string email)
    {
        var mensagem = factory.Emails.Para(email).Last(m => m.Corpo.Contains("/redefinir-senha?", StringComparison.Ordinal));
        var query = QueryHelpers.ParseQuery(new Uri(LinkRegex().Match(mensagem.Corpo).Value).Query);
        return (Guid.Parse(query["usuario"]!), query["token"]!);
    }

    [GeneratedRegex(@"https://\S+")]
    private static partial Regex LinkRegex();
}
