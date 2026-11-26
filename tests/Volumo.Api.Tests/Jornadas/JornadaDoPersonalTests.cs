using System.Net;
using System.Net.Http.Json;
using Volumo.Api.Tests.Support;
using Volumo.Shared.Autenticacao;
using static Volumo.Api.Tests.Support.ContasDeTeste;

namespace Volumo.Api.Tests.Jornadas;

/// <summary>
/// Jornadas completas pela API, como o app faria, com o cliente guardando cookies sozinho
/// (igual ao navegador). Cada passo depende do anterior.
/// </summary>
public sealed class JornadaDoPersonalTests(VolumoApiFactory factory) : IClassFixture<VolumoApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Cadastro_confirmacao_login_uso_renovacao_e_logout()
    {
        using var app = factory.CreateHttpsClient();
        var email = NovoEmail();

        // 1. Cadastro: a conta existe, mas ainda não entra.
        (await CadastrarPersonalAsync(app, email)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await LoginAsync(app, email)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // 2. Confirma pelo link do e-mail e entra.
        await ConfirmarEmailAsync(app, factory, email);
        using var login = await LoginAsync(app, email);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var sessao = await login.Content.ReadFromJsonAsync<RespostaDeLogin>(Ct);

        // 3. Usa uma rota protegida.
        (await GetContaAsync(app, sessao!.TokenDeAcesso)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // 4. O token de acesso vence: o app renova só com o cookie, sem pedir a senha.
        using var renovacao = await app.PostAsync("/auth/refresh", content: null, Ct);
        renovacao.StatusCode.ShouldBe(HttpStatusCode.OK);
        var renovada = await renovacao.Content.ReadFromJsonAsync<RespostaDeLogin>(Ct);
        (await GetContaAsync(app, renovada!.TokenDeAcesso)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // 5. Logout: o cookie some e não dá mais para renovar.
        (await app.PostAsync("/auth/logout", content: null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await app.PostAsync("/auth/refresh", content: null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Personal_configura_o_2fa_e_passa_a_entrar_com_o_codigo_do_celular()
    {
        using var app = factory.CreateHttpsClient();
        var email = await PersonalConfirmadoAsync(app, factory);

        // 1. Primeiro login: o app vê que falta configurar o 2FA.
        var (token, _) = await EntrarAsync(app, email);
        var conta = await (await GetContaAsync(app, token)).Content.ReadFromJsonAsync<DadosDaConta>(Ct);
        conta!.PrecisaConfigurarDoisFatores.ShouldBeTrue();

        // 2. Cadastra a chave no aplicativo autenticador e confirma com um código.
        var (chave, codigos, tokenComDoisFatores, _) = await AtivarDoisFatoresAsync(app, token);
        codigos.Count.ShouldBe(10);
        (await GetContaAsync(app, tokenComDoisFatores)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await app.PostAsync("/auth/logout", content: null, Ct)).Dispose();

        // 3. Próximo login: senha, depois o código do celular.
        using var primeiraEtapa = await LoginAsync(app, email);
        var desafio = (await primeiraEtapa.Content.ReadFromJsonAsync<RespostaDeLogin>(Ct))!.Desafio2Fa!;
        using var segundaEtapa = await app.PostAsJsonAsync("/auth/login/2fa", new SegundaEtapaDoLogin(desafio, Totp.Codigo(chave)), Ct);
        segundaEtapa.StatusCode.ShouldBe(HttpStatusCode.OK);

        // 4. A sessão renova sozinha e continua com segundo fator.
        (await app.PostAsync("/auth/refresh", content: null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
