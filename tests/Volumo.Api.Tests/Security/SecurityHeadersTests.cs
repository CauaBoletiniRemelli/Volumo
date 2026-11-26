using System.Net;

namespace Volumo.Api.Tests.Security;

public sealed class SecurityHeadersTests(VolumoApiFactory factory) : IClassFixture<VolumoApiFactory>
{
    public static TheoryData<string> Paths => ["/health", "/rota-que-nao-existe", "/__test/throw"];

    [Theory]
    [MemberData(nameof(Paths))]
    public async Task Respostas_sempre_trazem_cabecalhos_de_seguranca(string path)
    {
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Header(response, "X-Content-Type-Options").ShouldBe("nosniff");
        Header(response, "X-Frame-Options").ShouldBe("DENY");
        Header(response, "Content-Security-Policy").ShouldBe("default-src 'none'; frame-ancestors 'none'");
        Header(response, "Referrer-Policy").ShouldBe("no-referrer");
        Header(response, "Permissions-Policy").ShouldNotBeNull();
        response.Headers.CacheControl?.NoStore.ShouldBeTrue();
    }

    [Fact]
    public async Task Hsts_e_enviado_fora_do_ambiente_de_desenvolvimento()
    {
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Header(response, "Strict-Transport-Security").ShouldBe("max-age=31536000; includeSubDomains");
    }

    [Fact]
    public async Task Http_e_redirecionado_para_https()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("http://api.volumo.test"), AllowAutoRedirect = false });

        using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.TemporaryRedirect);
        response.Headers.Location?.Scheme.ShouldBe("https");
    }

    [Fact]
    public async Task Cabecalho_server_nao_e_enviado()
    {
        using var client = factory.CreateHttpsClient();

        using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.Headers.Server.ShouldBeEmpty();
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) || response.Content.Headers.TryGetValues(name, out values)
            ? string.Join(", ", values)
            : null;
}
