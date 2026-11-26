using Volumo.Infrastructure.Armazenamento;

namespace Volumo.Api.Arquivos;

public enum TipoDeMidia
{
    Foto = 1,
    Video = 2,
}

/// <summary>Formato confirmado pelos primeiros bytes do arquivo.</summary>
public sealed record FormatoDeMidia(string Extensao, string TipoDeConteudo);

/// <summary>
/// Foto e vídeo dos exercícios do catálogo. O envio passa pela API, que confere o conteúdo real
/// (assinatura do formato nos primeiros bytes, não a extensão nem o tipo que o navegador informou),
/// limita o tamanho e dá um nome novo ao arquivo. A leitura é por URL com prazo.
/// </summary>
public sealed partial class MidiaDoExercicio(IArmazenamentoDeArquivos armazenamento)
{
    public const long FotoMaxima = 2 * 1024 * 1024;
    public const long VideoMaximo = 10 * 1024 * 1024;

    /// <summary>
    /// Prazo das URLs de leitura. Maior que o cache das telas (5 min na memória, 15 min no aparelho),
    /// para a URL guardada ainda valer quando a tela é aberta de novo.
    /// </summary>
    public static readonly TimeSpan Validade = TimeSpan.FromHours(6);

    public string? Url(string? chave) =>
        chave is null ? null : armazenamento.UrlDeLeitura(AreaDeArquivos.Catalogo, chave, Validade).ToString();

    public static long TamanhoMaximo(TipoDeMidia tipo) => tipo == TipoDeMidia.Foto ? FotoMaxima : VideoMaximo;

    /// <summary>Grava e devolve a chave nova, ou <c>null</c> se o conteúdo não é um formato aceito.</summary>
    public async Task<string?> GravarAsync(Guid exercicioId, IFormFile arquivo, TipoDeMidia tipo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(arquivo);
        await using var conteudo = arquivo.OpenReadStream();
        var inicio = new byte[16];
        var lidos = await conteudo.ReadAtLeastAsync(inicio, inicio.Length, throwOnEndOfStream: false, ct);
        if (Identificar(inicio.AsSpan(0, lidos), tipo) is not { } formato)
        {
            return null;
        }

        conteudo.Position = 0;
        var chave = $"exercicios/{exercicioId:N}/{(tipo == TipoDeMidia.Foto ? "foto" : "video")}-{Guid.CreateVersion7():N}.{formato.Extensao}";
        await armazenamento.GravarAsync(AreaDeArquivos.Catalogo, chave, conteudo, formato.TipoDeConteudo, ct);
        return chave;
    }

    /// <summary>Remove o arquivo antigo depois da troca. Falhar aqui não desfaz a troca (sobra um arquivo órfão).</summary>
    public async Task ApagarAsync(string? chave, ILogger logger, CancellationToken ct)
    {
        if (chave is null)
        {
            return;
        }

        try
        {
            await armazenamento.ApagarAsync(AreaDeArquivos.Catalogo, chave, ct);
        }
#pragma warning disable CA1031 // qualquer falha do armazenamento só gera aviso
        catch (Exception e)
#pragma warning restore CA1031
        {
            LogFalhaAoApagar(logger, e, chave);
        }
    }

    /// <summary>Foto: JPEG, PNG ou WebP. Vídeo: MP4 ou WebM.</summary>
    public static FormatoDeMidia? Identificar(ReadOnlySpan<byte> inicio, TipoDeMidia tipo) => tipo switch
    {
        TipoDeMidia.Foto when inicio.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]) => new("jpg", "image/jpeg"),
        TipoDeMidia.Foto when inicio.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]) => new("png", "image/png"),
        TipoDeMidia.Foto when inicio.Length >= 12 && inicio[..4].SequenceEqual("RIFF"u8) && inicio[8..12].SequenceEqual("WEBP"u8) => new("webp", "image/webp"),
        TipoDeMidia.Video when inicio.Length >= 8 && inicio[4..8].SequenceEqual("ftyp"u8) => new("mp4", "video/mp4"),
        TipoDeMidia.Video when inicio.StartsWith((ReadOnlySpan<byte>)[0x1A, 0x45, 0xDF, 0xA3]) => new("webm", "video/webm"),
        _ => null,
    };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Não foi possível apagar o arquivo antigo {Chave}.")]
    private static partial void LogFalhaAoApagar(ILogger logger, Exception exception, string chave);
}
