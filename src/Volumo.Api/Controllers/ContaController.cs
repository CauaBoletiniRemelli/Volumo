using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Volumo.Api.Auth;
using Volumo.Infrastructure.Identity;
using Volumo.Shared.Autenticacao;

namespace Volumo.Api.Controllers;

/// <summary>Dados da conta logada. Sem [AllowAnonymous]: a política padrão exige login.</summary>
[ApiController]
[Route("conta")]
public sealed class ContaController(UserManager<Usuario> userManager) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DadosDaConta>> Get()
    {
        var usuario = Guid.TryParse(User.FindFirst(VolumoClaims.Subject)?.Value, out var id)
            ? await userManager.FindByIdAsync(id.ToString())
            : null;

        if (usuario is null || usuario.AnonimizadoEm is not null)
        {
            return Unauthorized();
        }

        return new DadosDaConta(usuario.Id, usuario.Email!, [.. await userManager.GetRolesAsync(usuario)]);
    }
}
