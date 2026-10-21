using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Volumo.Infrastructure.Identity;

namespace Volumo.Api.Auth;

/// <summary>
/// "Lembrar este aparelho": depois do código do 2FA, a pessoa pode pedir para o aparelho não pedir
/// o código de novo por 30 dias. A senha continua sendo exigida em todo login. O cookie é HttpOnly,
/// Secure, SameSite=Strict e restrito a /auth; o conteúdo é cifrado e assinado pelo Data Protection e
/// amarrado ao SecurityStamp, então trocar a senha, refazer o 2FA ou suspender a conta invalida todos
/// os aparelhos lembrados.
/// </summary>
public sealed class TrustedDeviceCookie(IDataProtectionProvider dataProtection, TimeProvider clock)
{
    public const string Name = "__Secure-volumo-dc";
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    private readonly ITimeLimitedDataProtector _protector =
        dataProtection.CreateProtector("Volumo.Auth.AparelhoConfiavel").ToTimeLimitedDataProtector();

    public void Write(HttpResponse response, Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(usuario);
        var expiresAt = clock.GetUtcNow() + Lifetime;
        response.Cookies.Append(Name, _protector.Protect($"{usuario.Id:N}|{usuario.SecurityStamp}", expiresAt), new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = RefreshTokenCookie.Path,
            Expires = expiresAt,
            IsEssential = true,
        });
    }

    /// <summary>Se o cookie do pedido foi emitido para este usuário e a conta não mudou desde então.</summary>
    public bool IsTrusted(HttpRequest request, Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(usuario);
        if (request.Cookies[Name] is not { Length: > 0 } valor)
        {
            return false;
        }

        try
        {
            var partes = _protector.Unprotect(valor).Split('|', 2);
            return partes.Length == 2
                && Guid.TryParseExact(partes[0], "N", out var id)
                && id == usuario.Id
                && usuario.SecurityStamp is not null
                && CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(partes[1]), System.Text.Encoding.UTF8.GetBytes(usuario.SecurityStamp));
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
