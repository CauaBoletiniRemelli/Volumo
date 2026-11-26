using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Volumo.Api.Security;
using Volumo.Api.Tests.Support;
using Volumo.Shared.Autenticacao;
using static Volumo.Api.Tests.Support.ContasDeTeste;

namespace Volumo.Api.Tests.Auth;

public sealed class TurnstileTests(VolumoApiFactory factory) : IClassFixture<VolumoApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Cadastro_reprovado_no_anti_robo_responde_400_e_nao_cria_nada()
    {
        using var client = factory.CreateHttpsClient();
        var email = NovoEmail();

        using var response = await CadastrarPersonalAsync(client, email, tokenTurnstile: VerificacaoHumanaFalsa.TokenReprovado);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        factory.Emails.Para(email).ShouldBeEmpty();
        (await LoginAsync(client, email)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Formularios_que_enviam_email_exigem_o_anti_robo()
    {
        using var client = factory.CreateHttpsClient();
        var email = await PersonalConfirmadoAsync(client, factory);
        var enviadosAntes = factory.Emails.Para(email).Count;

        using var recuperacao = await client.PostAsJsonAsync(
            "/auth/esqueci-senha", new PedidoDeRecuperacaoDeSenha(email, VerificacaoHumanaFalsa.TokenReprovado), Ct);
        using var reenvio = await client.PostAsJsonAsync(
            "/auth/reenviar-confirmacao", new PedidoDeReenvioDeConfirmacao(email, VerificacaoHumanaFalsa.TokenReprovado), Ct);
        using var semToken = await client.PostAsJsonAsync("/auth/esqueci-senha", new { email }, Ct);

        recuperacao.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        reenvio.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        semToken.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        factory.Emails.Para(email).Count.ShouldBe(enviadosAntes);
    }

    [Fact]
    public void Api_nao_sobe_sem_a_chave_do_turnstile()
    {
        using var semChave = new VolumoApiFactory().WithSetting("Turnstile:SecretKey", "");

        Should.Throw<InvalidOperationException>(() => semChave.CreateHttpsClient()).Message.ShouldContain("Turnstile");
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, """{"success":true}""", true)]
    [InlineData(HttpStatusCode.OK, """{"success":false,"error-codes":["invalid-input-response"]}""", false)]
    [InlineData(HttpStatusCode.OK, "não é json", false)]
    [InlineData(HttpStatusCode.InternalServerError, """{"success":true}""", false)]
    public async Task Verificador_so_aprova_resposta_de_sucesso_da_cloudflare(HttpStatusCode status, string corpo, bool esperado)
    {
        var handler = new HandlerFalso(_ => new HttpResponseMessage(status) { Content = new StringContent(corpo) });
        var verificador = Verificador(handler);

        (await verificador.VerifyAsync("token-do-widget", "203.0.113.7", Ct)).ShouldBe(esperado);

        handler.UltimoCorpo.ShouldContain("secret=segredo-de-teste");
        handler.UltimoCorpo.ShouldContain("response=token-do-widget");
        handler.UltimoCorpo.ShouldContain("remoteip=203.0.113.7");
        handler.UltimaUri.ShouldBe(TurnstileVerifier.Endpoint);
    }

    [Fact]
    public async Task Verificador_recusa_quando_a_cloudflare_esta_fora_do_ar()
    {
        var verificador = Verificador(new HandlerFalso(_ => throw new HttpRequestException("sem rede")));

        (await verificador.VerifyAsync("token-do-widget", null, Ct)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Verificador_recusa_token_vazio_sem_chamar_a_cloudflare(string token)
    {
        var handler = new HandlerFalso(_ => throw new InvalidOperationException("não deveria chamar"));

        (await Verificador(handler).VerifyAsync(token, null, Ct)).ShouldBeFalse();
    }

    private static TurnstileVerifier Verificador(HttpMessageHandler handler) =>
        new(
            new HttpClient(handler),
            TurnstileSettings.Load(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Turnstile:SecretKey"] = "segredo-de-teste" })
                .Build()),
            NullLogger<TurnstileVerifier>.Instance);

    private sealed class HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public string UltimoCorpo { get; private set; } = string.Empty;

        public Uri? UltimaUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            UltimaUri = request.RequestUri;
            UltimoCorpo = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return responder(request);
        }
    }
}
