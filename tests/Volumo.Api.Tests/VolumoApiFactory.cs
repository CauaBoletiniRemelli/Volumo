using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

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

    private readonly Dictionary<string, string?> _settings = new()
    {
        ["Cors:AllowedOrigins:0"] = AllowedOrigin,
        ["RateLimiting:Global:PermitLimit"] = "1000",
        ["Serilog:MinimumLevel:Default"] = "Warning",
        ["https_port"] = "443",
    };

    public VolumoApiFactory WithSetting(string key, string value)
    {
        _settings[key] = value;
        return this;
    }

    public HttpClient CreateHttpsClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = BaseAddress, AllowAutoRedirect = false });

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
            services.AddControllers().AddApplicationPart(typeof(VolumoApiFactory).Assembly));
    }
}
