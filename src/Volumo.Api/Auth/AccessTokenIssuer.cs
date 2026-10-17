using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Volumo.Infrastructure.Identity;

namespace Volumo.Api.Auth;

/// <summary>Claims curtas e sem dados pessoais: só o Id, os papéis e o método de autenticação.</summary>
public static class VolumoClaims
{
    public const string Subject = "sub";
    public const string Role = "role";
    public const string TokenId = "jti";

    /// <summary>Authentication Methods References (RFC 8176): "pwd" = senha; "mfa" = entrou também com segundo fator.</summary>
    public const string AuthenticationMethods = "amr";

    public const string MultiFactor = "mfa";

    public static Guid? UsuarioId(this System.Security.Claims.ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst(Subject)?.Value, out var id) ? id : null;
}

/// <summary>Emite o token de acesso (JWT HS256) de vida curta.</summary>
public sealed class AccessTokenIssuer(JwtSettings settings, TimeProvider clock)
{
    private readonly JsonWebTokenHandler _handler = new();

    public (string Token, DateTimeOffset ExpiresAt) Issue(Usuario usuario, IEnumerable<string> roles, bool comDoisFatores)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var now = clock.GetUtcNow();
        var expiresAt = now.AddMinutes(settings.AccessTokenMinutes);
        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(settings.Key, SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [VolumoClaims.Subject] = usuario.Id.ToString(),
                [VolumoClaims.TokenId] = Guid.NewGuid().ToString(),
                [VolumoClaims.Role] = roles.ToArray(),
                [VolumoClaims.AuthenticationMethods] = comDoisFatores ? new[] { "pwd", VolumoClaims.MultiFactor } : new[] { "pwd" },
            },
        });

        return (token, expiresAt);
    }
}
