using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Volumo.Infrastructure.Armazenamento;

namespace Volumo.Api.Arquivos;

/// <summary>
/// Arquivos numa pasta local, só para desenvolvimento e testes (a API recusa em produção). Para
/// imitar o R2, a leitura também é por URL com prazo: um token cifrado e assinado pelo Data
/// Protection, que a própria API confere ao servir o arquivo em <c>/arquivos/{token}</c>.
/// </summary>
public sealed partial class ArmazenamentoEmDisco(string pasta, Uri urlDaApi, IDataProtectionProvider dataProtection, TimeProvider clock)
    : IArmazenamentoDeArquivos
{
    public const string Rota = "arquivos";

    private readonly ITimeLimitedDataProtector _protector =
        dataProtection.CreateProtector("Volumo.Arquivos.LeituraEmDisco").ToTimeLimitedDataProtector();

    public async Task GravarAsync(AreaDeArquivos area, string chave, Stream conteudo, string tipoDeConteudo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        var caminho = Caminho(area, chave);
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        await using var arquivo = File.Create(caminho);
        await conteudo.CopyToAsync(arquivo, ct);
    }

    public Task ApagarAsync(AreaDeArquivos area, string chave, CancellationToken ct)
    {
        File.Delete(Caminho(area, chave));
        return Task.CompletedTask;
    }

    public Uri UrlDeLeitura(AreaDeArquivos area, string chave, TimeSpan validade)
    {
        Caminho(area, chave); // valida a chave
        var token = _protector.Protect($"{area}|{chave}", clock.GetUtcNow() + validade);
        return new Uri(urlDaApi, $"{Rota}/{token}");
    }

    /// <summary>Arquivo do token, se o token é válido e não venceu.</summary>
    public string? CaminhoDoToken(string token)
    {
        try
        {
            var partes = _protector.Unprotect(token).Split('|', 2);
            return partes.Length == 2 && Enum.TryParse<AreaDeArquivos>(partes[0], out var area) && Enum.IsDefined(area)
                ? Caminho(area, partes[1])
                : null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    /// <summary>Só chaves no formato que a API gera; o caminho final nunca sai da pasta configurada.</summary>
    private string Caminho(AreaDeArquivos area, string chave)
    {
        if (!FormatoDaChave().IsMatch(chave))
        {
            throw new ArgumentException("Chave de arquivo inválida.", nameof(chave));
        }

        var raiz = Path.GetFullPath(Path.Combine(pasta, area.ToString().ToLowerInvariant()));
        var caminho = Path.GetFullPath(Path.Combine(raiz, chave));
        return caminho.StartsWith(raiz + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? caminho
            : throw new ArgumentException("Chave de arquivo inválida.", nameof(chave));
    }

    [GeneratedRegex(@"^[a-z0-9-]+(/[a-z0-9-]+)*\.[a-z0-9]{2,5}$")]
    private static partial Regex FormatoDaChave();
}
