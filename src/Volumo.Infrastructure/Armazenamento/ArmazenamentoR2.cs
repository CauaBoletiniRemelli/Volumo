using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace Volumo.Infrastructure.Armazenamento;

/// <summary>Credenciais do R2. Vêm de variáveis de ambiente (produção) ou User Secrets, nunca do appsettings.</summary>
public sealed record ConfiguracaoR2(string AccountId, string AccessKeyId, string SecretAccessKey, string BucketCatalogo, string BucketPrivado);

/// <summary>
/// Cloudflare R2 pela API compatível com S3. Ajustes que o R2 exige do SDK da AWS: endereço da
/// conta, região "auto", caminho no estilo path e somas de verificação só quando obrigatórias
/// (o R2 não aceita todas as que o SDK manda por padrão).
/// </summary>
public sealed class ArmazenamentoR2 : IArmazenamentoDeArquivos, IDisposable
{
    private readonly AmazonS3Client _cliente;
    private readonly ConfiguracaoR2 _configuracao;

    public ArmazenamentoR2(ConfiguracaoR2 configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        _configuracao = configuracao;
        _cliente = new AmazonS3Client(
            new BasicAWSCredentials(configuracao.AccessKeyId, configuracao.SecretAccessKey),
            new AmazonS3Config
            {
                ServiceURL = $"https://{configuracao.AccountId}.r2.cloudflarestorage.com",
                AuthenticationRegion = "auto",
                ForcePathStyle = true,
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            });
    }

    public async Task GravarAsync(AreaDeArquivos area, string chave, Stream conteudo, string tipoDeConteudo, CancellationToken ct)
    {
        await _cliente.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = Bucket(area),
                Key = chave,
                InputStream = conteudo,
                ContentType = tipoDeConteudo,

                // A chave muda a cada envio, então o conteúdo de uma chave nunca muda: cache longo.
                Headers = { CacheControl = "private, max-age=31536000, immutable" },
                DisablePayloadSigning = true,
                DisableDefaultChecksumValidation = true,
            },
            ct);
    }

    public async Task ApagarAsync(AreaDeArquivos area, string chave, CancellationToken ct) =>
        await _cliente.DeleteObjectAsync(new DeleteObjectRequest { BucketName = Bucket(area), Key = chave }, ct);

    public Uri UrlDeLeitura(AreaDeArquivos area, string chave, TimeSpan validade) =>
        new(_cliente.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = Bucket(area),
            Key = chave,
            Verb = HttpVerb.GET,
            Protocol = Protocol.HTTPS,
            Expires = DateTime.UtcNow.Add(validade),
        }));

    public void Dispose() => _cliente.Dispose();

    private string Bucket(AreaDeArquivos area) => area switch
    {
        AreaDeArquivos.Catalogo => _configuracao.BucketCatalogo,
        AreaDeArquivos.Privado => _configuracao.BucketPrivado,
        _ => throw new ArgumentOutOfRangeException(nameof(area)),
    };
}
