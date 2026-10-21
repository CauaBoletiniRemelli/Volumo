namespace Volumo.Infrastructure.Email;

public sealed record MensagemDeEmail(string Para, string Assunto, string Corpo);

/// <summary>
/// Envio de e-mail transacional (confirmação de conta, convites, recuperação de senha).
/// Produção: <see cref="EmailBrevo"/>; desenvolvimento: <see cref="EmailEmArquivo"/>.
/// </summary>
public interface IEnvioDeEmail
{
    Task EnviarAsync(MensagemDeEmail mensagem, CancellationToken cancellationToken);
}
