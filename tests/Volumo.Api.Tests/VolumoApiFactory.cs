using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volumo.Api.Tests.Support;
using Volumo.Infrastructure.Email;

namespace Volumo.Api.Tests;

/// <summary>
/// Sobe a API em memória no ambiente "Testing" (não Development), para os testes verem
/// o mesmo comportamento de produção: HSTS ligado e erros sem detalhes internos.
/// </summary>
public class VolumoApiFactory : WebApplicationFactory<Program>
{
    public const string AllowedOrigin = "https://app.volumo.test";

    /// <summary>HTTPS e host que não é localhost, senão o HSTS não é enviado.</summary>
    public static readonly Uri BaseAddress = new("https://api.volumo.test");

    /// <summary>Chave só de teste (não é segredo): 32 bytes em Base64.</summary>
    public const string TestSigningKey = "dm9sdW1vLXRlc3Rlcy1jaGF2ZS1qd3QtMzItYnl0ZXMhIQ==";

    private readonly Dictionary<string, string?> _settings = new()
    {
        ["Cors:AllowedOrigins:0"] = AllowedOrigin,
        ["RateLimiting:Global:PermitLimit"] = "1000",
        ["RateLimiting:Login:PermitLimit"] = "1000",
        ["RateLimiting:Cadastro:PermitLimit"] = "1000",
        ["Serilog:MinimumLevel:Default"] = "Warning",
        ["https_port"] = "443",
        ["ConnectionStrings:Volumo"] = BancoDeTestes.ConnectionString,
        ["Jwt:Issuer"] = "https://api.volumo.test",
        ["Jwt:Audience"] = AllowedOrigin,
        ["Jwt:SigningKey"] = TestSigningKey,
        ["Front:BaseUrl"] = AllowedOrigin,
        ["Email:Provider"] = "File",
    };

    /// <summary>E-mails "enviados" pela API desta fábrica.</summary>
    public CaixaDeEmails Emails { get; } = new();

    public VolumoApiFactory WithSetting(string key, string value)
    {
        _settings[key] = value;
        return this;
    }

    /// <param name="handleCookies">Falso para controlar o cookie do refresh token manualmente.</param>
    public HttpClient CreateHttpsClient(bool handleCookies = true) =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = BaseAddress,
            AllowAutoRedirect = false,
            HandleCookies = handleCookies,
        });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // UseSetting (e não ConfigureAppConfiguration) porque o Program.cs lê parte da
        // configuração antes do Build(), quando os ajustes de ConfigureAppConfiguration ainda não valem.
        foreach (var (key, value) in _settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(VolumoApiFactory).Assembly);
            services.RemoveAll<IEnvioDeEmail>();
            services.AddSingleton<IEnvioDeEmail>(Emails);
        });
    }
}
