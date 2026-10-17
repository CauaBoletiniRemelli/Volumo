using Microsoft.AspNetCore.Identity;
using Volumo.Infrastructure.Identity;
using Volumo.Shared.Autenticacao;

namespace Volumo.Api.Auth;

/// <summary>Abre sessões: refresh token no cookie + token de acesso no corpo da resposta.</summary>
public sealed class SessionIssuer(UserManager<Usuario> userManager, AccessTokenIssuer accessTokens, RefreshTokenService refreshTokens)
{
    /// <summary>Abre uma sessão nova (nova família de refresh tokens).</summary>
    public async Task<RespostaDeLogin> StartAsync(HttpResponse response, Usuario usuario, bool comDoisFatores, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(response);
        var (refreshToken, refreshExpiresAt) = await refreshTokens.IssueAsync(usuario.Id, familiaId: null, comDoisFatores, ct);
        RefreshTokenCookie.Write(response, refreshToken, refreshExpiresAt);
        return await AccessTokenAsync(usuario, comDoisFatores);
    }

    public async Task<RespostaDeLogin> AccessTokenAsync(Usuario usuario, bool comDoisFatores)
    {
        var (token, expiresAt) = accessTokens.Issue(usuario, await userManager.GetRolesAsync(usuario), comDoisFatores);
        return new RespostaDeLogin(token, expiresAt);
    }
}
