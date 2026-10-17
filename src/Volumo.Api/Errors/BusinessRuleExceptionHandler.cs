using Microsoft.AspNetCore.Diagnostics;
using Volumo.Domain.Comum;

namespace Volumo.Api.Errors;

/// <summary>
/// Regra de negócio violada vira 400 com a mensagem do domínio, que é escrita para o usuário final.
/// Qualquer outra exceção continua virando 500 genérico.
/// </summary>
public sealed class BusinessRuleExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not RegraDeNegocioException regra)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Requisição inválida.",
                Detail = regra.Message,
            },
        });
    }
}
