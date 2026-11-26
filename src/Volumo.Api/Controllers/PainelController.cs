using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volumo.Api.Auth;
using Volumo.Api.Painel;
using Volumo.Shared.Painel;

namespace Volumo.Api.Controllers;

/// <summary>Painel do personal: quem treinou, quem sumiu e quem estagnou, entre os alunos com vínculo ativo.</summary>
[ApiController]
[Route("painel")]
[Authorize(Policy = Policies.Personal)]
public sealed class PainelController(PainelQueries queries) : ControllerBase
{
    [HttpGet]
    public Task<PainelDoPersonal> Get(CancellationToken ct) => queries.BuildAsync(User.UsuarioId()!.Value, ct);
}
