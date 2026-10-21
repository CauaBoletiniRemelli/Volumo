using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Volumo.Infrastructure.Email;

public sealed record ConfiguracaoBrevo(string ApiKey, string EmailDoRemetente, string NomeDoRemetente);

/// <summary>
/// Envio pela API transacional do Brevo (https://developers.brevo.com/reference/sendtransacemail).
/// O corpo vai em texto e em HTML (links clicáveis). Falha do Brevo vira exceção, sem o conteúdo
/// da mensagem (os links têm tokens): quem chamou decide se o usuário pode pedir o reenvio.
/// </summary>
public sealed partial class EmailBrevo(HttpClient http, ConfiguracaoBrevo configuracao) : IEnvioDeEmail
{
    public static readonly Uri Endpoint = new("https://api.brevo.com/v3/smtp/email");

    public async Task EnviarAsync(MensagemDeEmail mensagem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensagem);

        var corpo = new
        {
            sender = new { email = configuracao.EmailDoRemetente, name = configuracao.NomeDoRemetente },
            to = new[] { new { email = mensagem.Para } },
            subject = mensagem.Assunto,
            textContent = mensagem.Corpo,
            htmlContent = ParaHtml(mensagem.Corpo),
        };

        using var requisicao = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = JsonContent.Create(corpo) };
        requisicao.Headers.Add("api-key", configuracao.ApiKey);
        requisicao.Headers.Accept.ParseAdd("application/json");

        using var resposta = await http.SendAsync(requisicao, cancellationToken);
        if (!resposta.IsSuccessStatusCode)
        {
            var erro = await LerCodigoDeErroAsync(resposta, cancellationToken);
            throw new HttpRequestException($"Brevo recusou o envio do e-mail (HTTP {(int)resposta.StatusCode}, código {erro ?? "desconhecido"}).");
        }
    }

    /// <summary>Texto puro → HTML seguro: escapa tudo, transforma URLs em links e quebras em &lt;br&gt;.</summary>
    public static string ParaHtml(string texto)
    {
        var escapado = WebUtility.HtmlEncode(texto);
        var comLinks = Url().Replace(escapado, m => $"<a href=\"{m.Value}\">{m.Value}</a>");
        var comQuebras = comLinks.ReplaceLineEndings("<br>");
        return $"<div style=\"font-family:Arial,sans-serif;font-size:15px;line-height:1.5;color:#1C1F26\">{comQuebras}</div>";
    }

    private static async Task<string?> LerCodigoDeErroAsync(HttpResponseMessage resposta, CancellationToken cancellationToken)
    {
        try
        {
            return (await resposta.Content.ReadFromJsonAsync<ErroBrevo>(cancellationToken))?.Code;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"https://[^\s<]+")]
    private static partial Regex Url();

    private sealed record ErroBrevo([property: JsonPropertyName("code")] string? Code);
}
