using Volumo.Infrastructure.Persistencia;

namespace Volumo.Api.Auth;

/// <summary>Usuário autenticado da requisição atual, lido do token a cada consulta.</summary>
public sealed class HttpAccessContext(IHttpContextAccessor httpContextAccessor) : IContextoDeAcesso
{
    public Guid? UsuarioId =>
        httpContextAccessor.HttpContext?.User is { Identity.IsAuthenticated: true } user ? user.UsuarioId() : null;
}
