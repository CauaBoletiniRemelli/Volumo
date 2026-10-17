using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Volumo.Shared.Autenticacao;

namespace Volumo.Api.Security;

/// <summary>Verificação de que quem envia o formulário é uma pessoa (anti-robô).</summary>
public interface IHumanVerifier
{
    Task<bool> VerifyAsync(string token, string? remoteIp, CancellationToken cancellationToken);
}

public sealed class TurnstileSettings
{
    public const string SectionName = "Turnstile";

    public string SecretKey { get; private init; } = string.Empty;

    /// <summary>A chave secreta vem de User Secrets/variável de ambiente; sem ela a API não sobe.</summary>
    public static TurnstileSettings Load(IConfiguration configuration)
    {
        var secret = configuration[$"{SectionName}:SecretKey"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("Turnstile:SecretKey não configurada. Em produção, use a variável de ambiente Turnstile__SecretKey.");
        }

        return new TurnstileSettings { SecretKey = secret };
    }
}

/// <summary>
/// Cloudflare Turnstile: valida no servidor o token que o widget gerou no navegador. Em caso de
/// falha de rede ou resposta inesperada, recusa (falha fechada).
/// </summary>
public sealed partial class TurnstileVerifier(HttpClient http, TurnstileSettings settings, ILogger<TurnstileVerifier> logger) : IHumanVerifier
{
    public const int MaxTokenLength = 2048;
    public static readonly Uri Endpoint = new("https://challenges.cloudflare.com/turnstile/v0/siteverify");

    public async Task<bool> VerifyAsync(string token, string? remoteIp, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > MaxTokenLength)
        {
            return false;
        }

        var form = new Dictionary<string, string> { ["secret"] = settings.SecretKey, ["response"] = token };
        if (remoteIp is not null)
        {
            form["remoteip"] = remoteIp;
        }

        try
        {
            using var content = new FormUrlEncodedContent(form);
            using var response = await http.PostAsync(Endpoint, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                LogUnavailable(logger, (int)response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<SiteverifyResponse>(cancellationToken);
            return result?.Success == true;
        }
        catch (Exception exception) when (exception is HttpRequestException or System.Text.Json.JsonException
                                              or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            LogUnavailable(logger, statusCode: null);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Turnstile indisponível ou resposta inválida (HTTP {StatusCode}); verificação recusada.")]
    private static partial void LogUnavailable(ILogger logger, int? statusCode);

    private sealed record SiteverifyResponse(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("error-codes")] string[]? ErrorCodes);
}

/// <summary>
/// Exige verificação humana na ação: o corpo da requisição precisa implementar
/// <see cref="IComVerificacaoHumana"/> com um token válido do Turnstile.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequireHumanVerificationAttribute() : TypeFilterAttribute(typeof(HumanVerificationFilter));

public sealed class HumanVerificationFilter(IHumanVerifier verifier) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.ActionArguments.Values.OfType<IComVerificacaoHumana>().SingleOrDefault();
        var remoteIp = context.HttpContext.Connection.RemoteIpAddress?.ToString();
        if (request is null || !await verifier.VerifyAsync(request.TokenTurnstile, remoteIp, context.HttpContext.RequestAborted))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Não foi possível confirmar que você não é um robô. Tente novamente.",
            })
            { StatusCode = StatusCodes.Status400BadRequest };
            return;
        }

        await next();
    }
}
