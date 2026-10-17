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
}
