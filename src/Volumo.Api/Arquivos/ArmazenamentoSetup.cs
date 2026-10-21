using Microsoft.AspNetCore.DataProtection;
using Volumo.Infrastructure.Armazenamento;

namespace Volumo.Api.Arquivos;

/// <summary>
/// Escolhe o armazenamento de fotos e vídeos. Produção: Cloudflare R2 (credenciais por variáveis de
/// ambiente). Desenvolvimento e testes: pasta local, recusada em produção. Faltando algo, a API não sobe.
/// </summary>
public static class ArmazenamentoSetup
{
    public const string ProviderKey = "Armazenamento:Provider";

    public static IServiceCollection AddVolumoArmazenamento(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        switch (configuration[ProviderKey])
        {
            case "Disco" when environment.IsProduction():
                throw new InvalidOperationException("Armazenamento:Provider=Disco é só para desenvolvimento. Em produção, use R2.");
            case "Disco":
                var pasta = configuration["Armazenamento:Disco:Pasta"] is { Length: > 0 } configurada
                    ? configurada
                    : Path.Combine(Path.GetTempPath(), "volumo-arquivos");
                var urlDaApi = configuration["Armazenamento:Disco:UrlDaApi"] is { Length: > 0 } url
                    ? new Uri(url.EndsWith('/') ? url : url + "/")
                    : throw new InvalidOperationException("Armazenamento:Provider=Disco exige Armazenamento:Disco:UrlDaApi (endereço desta API).");
                services.AddSingleton(sp => new ArmazenamentoEmDisco(
                    pasta, urlDaApi, sp.GetRequiredService<IDataProtectionProvider>(), sp.GetRequiredService<TimeProvider>()));
                services.AddSingleton<IArmazenamentoDeArquivos>(sp => sp.GetRequiredService<ArmazenamentoEmDisco>());
                break;
            case "R2":
                services.AddSingleton(LoadR2(configuration));
                services.AddSingleton<IArmazenamentoDeArquivos, ArmazenamentoR2>();
                break;
            default:
                throw new InvalidOperationException($"{ProviderKey} não configurado. Valores aceitos: R2, Disco (só desenvolvimento).");
        }

        services.AddSingleton<MidiaDoExercicio>();
        return services;
    }

    public static ConfiguracaoR2 LoadR2(IConfiguration configuration)
    {
        string Obrigatorio(string nome) => configuration[$"Armazenamento:R2:{nome}"] is { Length: > 0 } valor
            ? valor
            : throw new InvalidOperationException(
                $"Armazenamento:Provider=R2 exige Armazenamento:R2:{nome}. Em produção, use a variável Armazenamento__R2__{nome}.");

        return new ConfiguracaoR2(
            Obrigatorio("AccountId"), Obrigatorio("AccessKeyId"), Obrigatorio("SecretAccessKey"), Obrigatorio("BucketCatalogo"), Obrigatorio("BucketPrivado"));
    }
}
