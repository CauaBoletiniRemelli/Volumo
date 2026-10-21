using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Volumo.Api.Auth;
using Volumo.Infrastructure.Email;

namespace Volumo.Api.Tests.Auth;

public sealed class EmailBrevoTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly MensagemDeEmail Mensagem = new(
        "aluno@exemplo.com", "Confirme seu e-mail", "Olá!\nConfirme pelo link: https://app.exemplo.com/confirmar?token=abc&id=1\nVolumo");

    [Fact]
    public async Task Envia_para_a_api_do_brevo_com_a_chave_o_remetente_e_os_dois_corpos()
    {
        var handler = new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("""{"messageId":"<1@brevo>"}""") });

        await Envio(handler).EnviarAsync(Mensagem, Ct);

        handler.UltimaUri.ShouldBe(EmailBrevo.Endpoint);
        handler.UltimaChave.ShouldBe("chave-de-teste");
        using var json = JsonDocument.Parse(handler.UltimoCorpo);
        var raiz = json.RootElement;
        raiz.GetProperty("sender").GetProperty("email").GetString().ShouldBe("nao-responda@exemplo.com");
        raiz.GetProperty("sender").GetProperty("name").GetString().ShouldBe("Volumo");
        raiz.GetProperty("to")[0].GetProperty("email").GetString().ShouldBe("aluno@exemplo.com");
        raiz.GetProperty("subject").GetString().ShouldBe("Confirme seu e-mail");
        raiz.GetProperty("textContent").GetString().ShouldBe(Mensagem.Corpo);
        raiz.GetProperty("htmlContent").GetString()!
            .ShouldContain("<a href=\"https://app.exemplo.com/confirmar?token=abc&amp;id=1\">");
    }

    [Fact]
    public async Task Recusa_do_brevo_vira_excecao_sem_o_conteudo_da_mensagem()
    {
        var handler = new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("""{"code":"unauthorized","message":"Key not found"}"""),
        });

        var erro = await Should.ThrowAsync<HttpRequestException>(() => Envio(handler).EnviarAsync(Mensagem, Ct));

        erro.Message.ShouldContain("401");
        erro.Message.ShouldContain("unauthorized");
        erro.Message.ShouldNotContain("token=abc");
    }

    [Fact]
    public void Html_escapa_o_texto_antes_de_criar_os_links()
    {
        var html = EmailBrevo.ParaHtml("Nome: <script>alert(1)</script>\nhttps://app.exemplo.com/x");

        html.ShouldNotContain("<script>");
        html.ShouldContain("&lt;script&gt;");
        html.ShouldContain("<br><a href=\"https://app.exemplo.com/x\">");
    }

    [Theory]
    [InlineData("", "nao-responda@exemplo.com")]
    [InlineData("chave", "")]
    public void Configuracao_exige_chave_e_remetente(string chave, string remetente)
    {
        var configuracao = Configuracao(chave, remetente);

        Should.Throw<InvalidOperationException>(() => AuthSetup.LoadBrevo(configuracao)).Message.ShouldContain("Email__Brevo__");
    }

    [Fact]
    public void Api_nao_sobe_com_brevo_sem_chave()
    {
        using var semChave = new VolumoApiFactory().WithSetting("Email:Provider", "Brevo");

        Should.Throw<InvalidOperationException>(() => semChave.CreateHttpsClient()).Message.ShouldContain("Brevo");
    }

    private static EmailBrevo Envio(HttpMessageHandler handler) =>
        new(new HttpClient(handler), AuthSetup.LoadBrevo(Configuracao("chave-de-teste", "nao-responda@exemplo.com")));

    private static IConfiguration Configuracao(string chave, string remetente) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:Brevo:ApiKey"] = chave,
                ["Email:Brevo:SenderEmail"] = remetente,
            })
            .Build();

    private sealed class HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public string UltimoCorpo { get; private set; } = string.Empty;

        public Uri? UltimaUri { get; private set; }

        public string? UltimaChave { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            UltimaUri = request.RequestUri;
            UltimaChave = request.Headers.TryGetValues("api-key", out var valores) ? valores.Single() : null;
            UltimoCorpo = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return responder(request);
        }
    }
}
