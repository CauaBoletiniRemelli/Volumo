using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Volumo.Api.Auth;
using Volumo.Domain.Usuarios;
using Volumo.Infrastructure.Identity;
using Volumo.Infrastructure.Persistencia;
using Volumo.Shared.Autenticacao;

namespace Volumo.Api.Controllers;

/// <summary>A própria conta. Sem [AllowAnonymous]: a política padrão exige login.</summary>
[ApiController]
[Route("conta")]
public sealed class ContaController(
    UserManager<Usuario> userManager,
    VolumoDbContext db,
    SessionIssuer sessions,
    RefreshTokenService refreshTokens,
    TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DadosDaConta>> Get(CancellationToken ct)
    {
        var usuario = await CurrentUserAsync();
        if (usuario is null)
        {
            return Unauthorized();
        }

        var papeis = await userManager.GetRolesAsync(usuario);
        return new DadosDaConta(
            usuario.Id,
            usuario.Email!,
            [.. papeis],
            usuario.TwoFactorEnabled,
            PrecisaConfigurarDoisFatores: papeis.Contains(Papeis.Personal) && !usuario.TwoFactorEnabled,
            ContaInativa: await sessions.IsInactiveAsync(usuario.Id, ct));
    }

    /// <summary>
    /// Pausa a conta (nada é apagado). Pede a senha de novo e encerra todas as sessões. Para voltar,
    /// basta entrar e chamar <c>/conta/reativar</c>. Os vínculos com personal/alunos continuam.
    /// </summary>
    [HttpPost("inativar")]
    public async Task<IActionResult> Deactivate(ConfirmacaoDeSenha request, CancellationToken ct)
    {
        var usuario = await CurrentUserAsync();
        if (usuario is null)
        {
            return Unauthorized();
        }

        if (!await userManager.CheckPasswordAsync(usuario, request.Senha))
        {
            await userManager.AccessFailedAsync(usuario);
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Senha incorreta.");
        }

        var now = clock.GetUtcNow();
        foreach (var perfil in await ProfilesAsync(usuario.Id, ct))
        {
            perfil.Inativar(now);
        }

        await db.SaveChangesAsync(ct);
        await refreshTokens.RevokeAllForUserAsync(usuario.Id, ct);
        return NoContent();
    }

    /// <summary>Reativa a conta pausada e devolve um token de acesso já sem a marca de conta inativa.</summary>
    [HttpPost("reativar")]
    public async Task<ActionResult<RespostaDeLogin>> Reactivate(CancellationToken ct)
    {
        var usuario = await CurrentUserAsync();
        if (usuario is null)
        {
            return Unauthorized();
        }

        foreach (var perfil in await ProfilesAsync(usuario.Id, ct))
        {
            perfil.Reativar();
        }

        await db.SaveChangesAsync(ct);
        var comDoisFatores = User.HasClaim(VolumoClaims.AuthenticationMethods, VolumoClaims.MultiFactor);
        return await sessions.AccessTokenAsync(usuario, comDoisFatores, ct);
    }

    private async Task<Usuario?> CurrentUserAsync()
    {
        var usuario = User.UsuarioId() is { } id ? await userManager.FindByIdAsync(id.ToString()) : null;
        return usuario is { AnonimizadoEm: null, Suspenso: false } ? usuario : null;
    }

    private async Task<List<Perfil>> ProfilesAsync(Guid usuarioId, CancellationToken ct)
    {
        var perfis = new List<Perfil>();
        perfis.AddRange(await db.PerfisPersonal.IgnoreQueryFilters([FiltrosDeConsulta.Ativos]).Where(p => p.Id == usuarioId).ToListAsync(ct));
        perfis.AddRange(await db.PerfisAluno.IgnoreQueryFilters([FiltrosDeConsulta.Ativos]).Where(a => a.Id == usuarioId).ToListAsync(ct));
        return perfis;
    }
}
