namespace Volumo.Api.Auth;

/// <summary>
/// Cookie do refresh token: HttpOnly (o JavaScript não lê), Secure (só HTTPS), SameSite=Strict
/// (não vai em requisições vindas de outros sites) e restrito ao caminho /auth.
/// O prefixo __Secure- faz o navegador recusar o cookie se não vier por HTTPS.
/// </summary>
public static class RefreshTokenCookie
{
    public const string Name = "__Secure-volumo-rt";
    public const string Path = "/auth";

    public static void Write(HttpResponse response, string token, DateTimeOffset expiresAt) =>
        response.Cookies.Append(Name, token, Options(expiresAt));

    public static string? Read(HttpRequest request) => request.Cookies[Name];

    public static void Delete(HttpResponse response) => response.Cookies.Delete(Name, Options(expiresAt: null));

    private static CookieOptions Options(DateTimeOffset? expiresAt) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expiresAt,
        IsEssential = true,
    };
}
