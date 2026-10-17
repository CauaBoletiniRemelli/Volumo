using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volumo.Api.Security;

namespace Volumo.Api.Tests.Security;

public sealed class CorsTests(VolumoApiFactory factory) : IClassFixture<VolumoApiFactory>
{
    [Fact]
    public async Task Origem_configurada_recebe_liberacao_com_credenciais()
    {
        using var response = await Preflight(VolumoApiFactory.AllowedOrigin);

        response.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe([VolumoApiFactory.AllowedOrigin]);
        response.Headers.GetValues("Access-Control-Allow-Credentials").ShouldBe(["true"]);
    }

    [Theory]
    [InlineData("https://site-malicioso.test")]
    [InlineData("https://app.volumo.test.site-malicioso.test")]
    [InlineData("http://app.volumo.test")]
    public async Task Origem_desconhecida_nao_recebe_liberacao(string origin)
    {
        using var response = await Preflight(origin);

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    [Theory]
    [InlineData("*")]
    [InlineData("https://*.volumo.test")]
    [InlineData("https://app.volumo.test/")]
    [InlineData("https://app.volumo.test/caminho")]
    [InlineData("nao-e-url")]
    public void Origem_invalida_impede_a_api_de_subir(string origin)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = origin })
            .Build();

        Should.Throw<InvalidOperationException>(() => new ServiceCollection().AddVolumoCors(configuration));
    }

    private async Task<HttpResponseMessage> Preflight(string origin)
    {
        using var client = factory.CreateHttpsClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/health");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
