using System.Net;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using static Volumo.Api.Tests.Support.ContasDeTeste;

namespace Volumo.Api.Tests.Auth;

/// <summary>Toda rota exige login por padrão, e só tokens íntegros, da API e dentro da validade servem.</summary>
public sealed class TokenDeAcessoTests(VolumoApiFactory factory) : IClassFixture<VolumoApiFactory>
{
    [Fact]
    public async Task Rota_sem_AllowAnonymous_exige_login()
    {
        using var client = factory.CreateHttpsClient();

        using var response = await GetContaAsync(client, tokenDeAcesso: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_adulterado_e_recusado()
    {
        using var client = factory.CreateHttpsClient();
        var email = await PersonalConfirmadoAsync(client, factory);
        var (token, _) = await EntrarAsync(client, email);
        var partes = token.Split('.');
        var payload = Base64UrlEncoder.Decode(partes[1]).Replace("\"Personal\"", "\"Admin\"", StringComparison.Ordinal);
        var adulterado = $"{partes[0]}.{Base64UrlEncoder.Encode(payload)}.{partes[2]}";

        (await GetContaAsync(client, adulterado)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_assinado_com_outra_chave_e_recusado()
    {
        using var client = factory.CreateHttpsClient();

        var token = CriarToken(Encoding.UTF8.GetBytes("outra-chave-de-32-bytes-no-minimo!!"), expira: DateTime.UtcNow.AddMinutes(5));

        (await GetContaAsync(client, token)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_vencido_e_recusado()
    {
        using var client = factory.CreateHttpsClient();

        var token = CriarToken(Convert.FromBase64String(VolumoApiFactory.TestSigningKey), expira: DateTime.UtcNow.AddMinutes(-2));

        (await GetContaAsync(client, token)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_sem_assinatura_e_recusado()
    {
        using var client = factory.CreateHttpsClient();
        var header = Base64UrlEncoder.Encode("""{"alg":"none","typ":"JWT"}""");
        var payload = Base64UrlEncoder.Encode(
            $$"""{"sub":"{{Guid.CreateVersion7()}}","iss":"https://api.volumo.test","aud":"{{VolumoApiFactory.AllowedOrigin}}","exp":{{DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()}}}""");

        (await GetContaAsync(client, $"{header}.{payload}.")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static string CriarToken(byte[] chave, DateTime expira) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "https://api.volumo.test",
            Audience = VolumoApiFactory.AllowedOrigin,
            NotBefore = expira.AddMinutes(-10),
            IssuedAt = expira.AddMinutes(-10),
            Expires = expira,
            Claims = new Dictionary<string, object> { ["sub"] = Guid.CreateVersion7().ToString(), ["role"] = new[] { "Admin" } },
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(chave), SecurityAlgorithms.HmacSha256),
        });
}
