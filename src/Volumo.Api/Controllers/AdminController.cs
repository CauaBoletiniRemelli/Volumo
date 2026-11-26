using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Volumo.Api.Auth;
using Volumo.Infrastructure.Identity;
using Volumo.Shared.Admin;

namespace Volumo.Api.Controllers;

/// <summary>Ações da plataforma sobre contas. Só Admin com 2FA.</summary>
[ApiController]
[Route("admin/usuarios")]
[Authorize(Policy = Policies.Admin)]
public sealed class AdminController(
    UserManager<Usuario> userManager,
    RefreshTokenService refreshTokens,
    TimeProvider clock) : ControllerBase
{
    /// <summary>Suspende a conta e encerra todas as sessões dela. Só um Admin desfaz.</summary>
    [HttpPost("{id:guid}/suspender")]
    public async Task<IActionResult> Suspend(Guid id, Suspensao request, CancellationToken ct)
    {
        if (id == User.UsuarioId())
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Você não pode suspender a própria conta.");
        }

        var usuario = await userManager.FindByIdAsync(id.ToString());
        if (usuario is null || usuario.AnonimizadoEm is not null)
        {
            return NotFound();
        }

        usuario.Suspender(request.Motivo, clock.GetUtcNow());
        await userManager.UpdateSecurityStampAsync(usuario); // também invalida desafios de 2FA e links pendentes
        await refreshTokens.RevokeAllForUserAsync(usuario.Id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/remover-suspensao")]
    public async Task<IActionResult> Unsuspend(Guid id)
    {
        var usuario = await userManager.FindByIdAsync(id.ToString());
        if (usuario is null || usuario.AnonimizadoEm is not null)
        {
            return NotFound();
        }

        usuario.RemoverSuspensao();
        await userManager.UpdateAsync(usuario);
        return NoContent();
    }
}
