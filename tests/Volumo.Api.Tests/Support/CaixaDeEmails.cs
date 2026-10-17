using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Volumo.Infrastructure.Email;

namespace Volumo.Api.Tests.Support;

/// <summary>Substitui o envio de e-mail nos testes: guarda as mensagens para os testes lerem os links.</summary>
public sealed partial class CaixaDeEmails : IEnvioDeEmail
{
    private readonly ConcurrentQueue<MensagemDeEmail> _mensagens = new();

    public Task EnviarAsync(MensagemDeEmail mensagem, CancellationToken cancellationToken)
    {
        _mensagens.Enqueue(mensagem);
        return Task.CompletedTask;
    }

    public IReadOnlyList<MensagemDeEmail> Para(string email) =>
        [.. _mensagens.Where(m => string.Equals(m.Para, email, StringComparison.OrdinalIgnoreCase))];

    /// <summary>Extrai usuário e token do link de confirmação do último e-mail enviado para o endereço.</summary>
    public (Guid UsuarioId, string Token) LinkDeConfirmacao(string email)
    {
        var mensagem = Para(email).Last(m => m.Corpo.Contains("/confirmar-email?", StringComparison.Ordinal));
        var link = new Uri(LinkRegex().Match(mensagem.Corpo).Value);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(link.Query);
        return (Guid.Parse(query["usuario"]!), query["token"]!);
    }

    [GeneratedRegex(@"https://\S+")]
    private static partial Regex LinkRegex();
}
