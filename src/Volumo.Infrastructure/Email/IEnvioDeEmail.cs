namespace Volumo.Infrastructure.Email;

public sealed record MensagemDeEmail(string Para, string Assunto, string Corpo);

/// <summary>
/// Envio de e-mail transacional (confirmação de conta, convites, recuperação de senha).
/// O provedor de produção ainda não foi escolhido (ver seção 13 do CLAUDE.md).
/// </summary>
public interface IEnvioDeEmail
{
    Task EnviarAsync(MensagemDeEmail mensagem, CancellationToken cancellationToken);
}
