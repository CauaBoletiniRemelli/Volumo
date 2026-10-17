using Microsoft.AspNetCore.Mvc;

namespace Volumo.Api.Tests.Support;

/// <summary>Rotas que só existem nos testes, para exercitar o pipeline da API.</summary>
[ApiController]
[Route("__test")]
public sealed class TestOnlyController : ControllerBase
{
    public const string SecretMessage = "detalhe interno que nunca pode vazar";

    [HttpGet("throw")]
    public IActionResult Throw() => throw new InvalidOperationException(SecretMessage);
}
