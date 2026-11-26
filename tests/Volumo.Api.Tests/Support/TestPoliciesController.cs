using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volumo.Api.Auth;

namespace Volumo.Api.Tests.Support;

/// <summary>Rotas só de teste para exercitar as políticas de autorização antes de existirem rotas reais.</summary>
[ApiController]
[Route("__test/politicas")]
public sealed class TestPoliciesController : ControllerBase
{
    [HttpGet("personal")]
    [Authorize(Policy = Policies.Personal)]
    public IActionResult Personal() => Ok();

    [HttpGet("aluno")]
    [Authorize(Policy = Policies.Aluno)]
    public IActionResult Aluno() => Ok();
}
