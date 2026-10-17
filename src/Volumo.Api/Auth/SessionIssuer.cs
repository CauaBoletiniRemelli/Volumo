using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Volumo.Infrastructure.Identity;
using Volumo.Infrastructure.Persistencia;
using Volumo.Shared.Autenticacao;

namespace Volumo.Api.Auth;

/// <summary>Abre sessões: refresh token no cookie + token de acesso no corpo da resposta.</summary>
public sealed class SessionIssuer(
    UserManager<Usuario> userManager,
    VolumoDbContext db,
    AccessTokenIssuer accessTokens,
    RefreshTokenService refreshTokens)
{
    /// <summary>Abre uma sessão nova (nova família de refresh tokens).</summary>
    public async Task<RespostaDeLogin> StartAsync(HttpResponse response, Usuario usuario, bool comDoisFatores, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(response);
        var (refreshToken, refreshExpiresAt) = await refreshTokens.IssueAsync(usuario.Id, familiaId: null, comDoisFatores, ct);
        RefreshTokenCookie.Write(response, refreshToken, refreshExpiresAt);
        return await AccessTokenAsync(usuario, comDoisFatores, ct);
    }

    /// <summary>Token de acesso com o estado atual da conta (papéis e se está inativa).</summary>
    public async Task<RespostaDeLogin> AccessTokenAsync(Usuario usuario, bool comDoisFatores, CancellationToken ct)
    {
        var roles = await userManager.GetRolesAsync(usuario);
        var (token, expiresAt) = accessTokens.Issue(usuario, roles, comDoisFatores, await IsInactiveAsync(usuario.Id, ct));
        return new RespostaDeLogin(token, expiresAt);
    }

    /// <summary>Inativa quando a conta tem perfil e todos os perfis estão inativos.</summary>
    public async Task<bool> IsInactiveAsync(Guid usuarioId, CancellationToken ct)
    {
        var personalAtivo = await db.PerfisPersonal.IgnoreQueryFilters([FiltrosDeConsulta.Ativos])
            .Where(p => p.Id == usuarioId).Select(p => (bool?)(p.InativadoEm == null)).SingleOrDefaultAsync(ct);
        var alunoAtivo = await db.PerfisAluno.IgnoreQueryFilters([FiltrosDeConsulta.Ativos])
            .Where(a => a.Id == usuarioId).Select(a => (bool?)(a.InativadoEm == null)).SingleOrDefaultAsync(ct);

        bool?[] perfis = [personalAtivo, alunoAtivo];
        return perfis.Any(p => p.HasValue) && perfis.All(p => p != true);
    }
}
