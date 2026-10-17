namespace Volumo.Api.Security;

public static class CorsSetup
{
    public const string SectionName = "Cors:AllowedOrigins";

    /// <summary>
    /// CORS liberado só para as origens configuradas (a URL do Client). Credenciais permitidas
    /// por causa do cookie do refresh token, então curinga (*) é proibido.
    /// </summary>
    public static IServiceCollection AddVolumoCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection(SectionName).Get<string[]>() ?? [];

        foreach (var origin in origins)
        {
            if (origin.Contains('*', StringComparison.Ordinal)
                || !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                || uri.AbsolutePath != "/"
                || origin.EndsWith('/'))
            {
                throw new InvalidOperationException($"Origem CORS inválida: '{origin}'. Use apenas esquema + host + porta, sem curinga e sem barra final.");
            }
        }

        services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins(origins)
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
            .WithHeaders("Content-Type", "Authorization")
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));

        return services;
    }
}
