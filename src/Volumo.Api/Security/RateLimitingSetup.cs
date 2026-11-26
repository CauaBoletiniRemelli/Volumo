using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace Volumo.Api.Security;

/// <summary>Nomes das políticas para usar com [EnableRateLimiting(...)].</summary>
public static class RateLimitPolicies
{
    public const string Login = "login";
    public const string Cadastro = "cadastro";
}

public sealed class FixedWindowSettings
{
    public int PermitLimit { get; set; }
    public int WindowSeconds { get; set; }
}

public sealed class RateLimitSettings
{
    public const string SectionName = "RateLimiting";

    /// <summary>Limite geral por IP, aplicado a todas as rotas.</summary>
    public FixedWindowSettings Global { get; set; } = new() { PermitLimit = 100, WindowSeconds = 60 };

    public FixedWindowSettings Login { get; set; } = new() { PermitLimit = 5, WindowSeconds = 60 };

    public FixedWindowSettings Cadastro { get; set; } = new() { PermitLimit = 3, WindowSeconds = 600 };
}

public static class RateLimitingSetup
{
    public static IServiceCollection AddVolumoRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimitSettings>(configuration.GetSection(RateLimitSettings.SectionName));

        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                FixedWindowByIp(context, Settings(context).Global));

            options.AddPolicy(RateLimitPolicies.Login, context => FixedWindowByIp(context, Settings(context).Login));
            options.AddPolicy(RateLimitPolicies.Cadastro, context => FixedWindowByIp(context, Settings(context).Cadastro));

            options.OnRejected = async (context, _) =>
            {
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                var problemDetails = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problemDetails.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails =
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Muitas requisições. Tente novamente em instantes.",
                    },
                });
            };
        });

        return services;
    }

    private static RateLimitSettings Settings(HttpContext context) =>
        context.RequestServices.GetRequiredService<IOptions<RateLimitSettings>>().Value;

    private static RateLimitPartition<string> FixedWindowByIp(HttpContext context, FixedWindowSettings settings) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = settings.PermitLimit,
                Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                QueueLimit = 0,
            });
}
