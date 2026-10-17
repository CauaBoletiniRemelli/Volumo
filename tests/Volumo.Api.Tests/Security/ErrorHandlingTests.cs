using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Volumo.Api.Tests.Support;

namespace Volumo.Api.Tests.Security;

public sealed class ErrorHandlingTests(VolumoApiFactory factory) : IClassFixture<VolumoApiFactory>
{
    [Fact]
    public async Task Excecao_vira_problem_details_sem_vazar_detalhes_internos()
    {
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync("/__test/throw", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        body.ShouldNotContain(TestOnlyController.SecretMessage);
        body.ShouldNotContain(nameof(InvalidOperationException));
        body.ShouldNotContain("   at ");
        body.ShouldContain("traceId");
    }

    [Fact]
    public async Task Rota_inexistente_vira_problem_details_404()
    {
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync("/rota-que-nao-existe", TestContext.Current.CancellationToken);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        problem.ShouldNotBeNull().Status.ShouldBe(404);
    }
}
