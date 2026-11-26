using System.Net;

namespace Volumo.Api.Tests.Security;

public sealed class RateLimitingTests
{
    [Fact]
    public async Task Excesso_de_requisicoes_recebe_429_com_retry_after()
    {
        // Fábrica própria: o limitador guarda estado e não pode afetar as outras classes de teste.
        await using var factory = new VolumoApiFactory().WithSetting("RateLimiting:Global:PermitLimit", "3");
        using var client = factory.CreateHttpsClient();
        var ct = TestContext.Current.CancellationToken;

        for (var i = 0; i < 3; i++)
        {
            using var permitted = await client.GetAsync("/health", ct);
            permitted.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var rejected = await client.GetAsync("/health", ct);

        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        rejected.Headers.RetryAfter.ShouldNotBeNull();
        rejected.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }
}
