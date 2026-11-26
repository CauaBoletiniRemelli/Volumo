using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Volumo.Api.Arquivos;

namespace Volumo.Api.Controllers;

/// <summary>
/// Entrega os arquivos do armazenamento em pasta local (desenvolvimento e testes). O token da URL é
/// a autorização: cifrado, assinado e com prazo. Com o R2, a rota não existe (404) e as URLs apontam
/// direto para o R2.
/// </summary>
[ApiController]
[Route(ArmazenamentoEmDisco.Rota)]
[AllowAnonymous]
public sealed class ArquivosController(IServiceProvider services) : ControllerBase
{
    private static readonly FileExtensionContentTypeProvider TiposPorExtensao = new();

    [HttpGet("{token}")]
    [SuppressMessage("Security", "CA3003", Justification = "O caminho vem de um token cifrado e assinado pela própria API, a chave passa por um formato fixo e o caminho final é conferido dentro da pasta configurada (ArmazenamentoEmDisco.Caminho).")]
    public IActionResult Get(string token)
    {
        var caminho = services.GetService<ArmazenamentoEmDisco>()?.CaminhoDoToken(token);
        if (caminho is null || !System.IO.File.Exists(caminho) || !TiposPorExtensao.TryGetContentType(caminho, out var tipo))
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "private, max-age=21600";
        return PhysicalFile(caminho, tipo, enableRangeProcessing: true);
    }
}
