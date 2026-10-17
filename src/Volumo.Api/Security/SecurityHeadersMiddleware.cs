namespace Volumo.Api.Security;

/// <summary>
/// Adiciona os cabeçalhos de segurança em todas as respostas, inclusive nas de erro.
/// Usa OnStarting porque o UseExceptionHandler limpa os cabeçalhos antes de escrever o erro.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            headers["Cross-Origin-Resource-Policy"] = "same-site";

            // A API devolve dados pessoais: nada deve ficar em cache de navegador ou proxy.
            if (string.IsNullOrEmpty(headers.CacheControl))
            {
                headers.CacheControl = "no-store";
            }

            return Task.CompletedTask;
        });

        return next(context);
    }
}
