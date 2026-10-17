using System.Globalization;

namespace Volumo.Infrastructure.Email;

/// <summary>
/// Só para desenvolvimento: grava cada e-mail como arquivo de texto numa pasta local, para abrir
/// os links de confirmação sem servidor de e-mail. Nunca usar em produção (os links contêm tokens).
/// </summary>
public sealed class EmailEmArquivo(string pasta) : IEnvioDeEmail
{
    public async Task EnviarAsync(MensagemDeEmail mensagem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensagem);
        Directory.CreateDirectory(pasta);

        var nome = string.Create(CultureInfo.InvariantCulture, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.txt");
        var conteudo = $"Para: {mensagem.Para}\nAssunto: {mensagem.Assunto}\n\n{mensagem.Corpo}\n";
        await File.WriteAllTextAsync(Path.Combine(pasta, nome), conteudo, cancellationToken);
    }
}
