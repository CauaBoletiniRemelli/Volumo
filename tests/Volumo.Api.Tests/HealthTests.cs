using System.Net;

namespace Volumo.Api.Tests;

public sealed class HealthTests(VolumoApiFactory factory) : IClassFixture<VolumoApiFactory>
{
    [Fact]
    public async Task Health_responde_ok()
    {
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_responde_503_sem_detalhes_quando_o_banco_nao_esta_acessivel()
    {
        await using var semBanco = new VolumoApiFactory().WithSetting(
            "ConnectionStrings:Volumo",
            @"Server=(localdb)\MSSQLLocalDB;Database=Volumo_NaoExiste;Trusted_Connection=True;Connect Timeout=5");
        using var client = semBanco.CreateHttpsClient();

        using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("Unhealthy");
    }

    [Fact]
    public void Api_nao_sobe_sem_connection_string()
    {
        using var semConnectionString = new VolumoApiFactory().WithSetting("ConnectionStrings:Volumo", "");

        var erro = Should.Throw<InvalidOperationException>(() => semConnectionString.CreateHttpsClient());
        erro.Message.ShouldContain("ConnectionStrings:Volumo");
    }
}
