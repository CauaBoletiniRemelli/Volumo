using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Volumo.Api.Arquivos;
using Volumo.Api.Auth;
using Volumo.Api.Treinos;
using Volumo.Domain.Exercicios;
using Volumo.Infrastructure.Persistencia;
using Volumo.Shared.Admin;
using Volumo.Shared.Treinos;

namespace Volumo.Api.Controllers;

/// <summary>
/// Catálogo de exercícios da plataforma, mantido pela TI: dados, instruções, foto e vídeo de
/// execução. Só Admin com 2FA. Exercício do catálogo nunca é apagado, só inativado (treinos antigos
/// continuam mostrando o nome). Exercícios de personal não aparecem aqui.
/// </summary>
[ApiController]
[Route("admin/exercicios")]
[Authorize(Policy = Policies.Admin)]
public sealed class AdminExerciciosController(
    VolumoDbContext db,
    ExerciseMapper mapper,
    MidiaDoExercicio midia,
    TimeProvider clock,
    ILogger<AdminExerciciosController> logger) : ControllerBase
{
    /// <summary>Limite do corpo do envio: o vídeo máximo mais a folga do formulário.</summary>
    private const long CorpoMaximo = MidiaDoExercicio.VideoMaximo + (64 * 1024);

    [HttpGet]
    public async Task<IReadOnlyList<ExercicioDoCatalogoResumo>> List(CancellationToken ct)
    {
        var exercicios = await Catalogo().AsNoTracking().OrderBy(e => e.Nome).ToListAsync(ct);
        return [.. exercicios.Select(e => new ExercicioDoCatalogoResumo(
            e.Id,
            e.Nome,
            PlanMapper.ToContract(e.GrupoMuscular),
            e.Equipamento is { } equipamento ? ExerciseMapper.ToContract(equipamento) : null,
            e.FotoChave is not null,
            e.VideoChave is not null,
            e.Ativo))];
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ExercicioDetalhe>> Get(Guid id, CancellationToken ct)
    {
        var exercicio = await Catalogo().AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, ct);
        return exercicio is null ? NotFound() : mapper.ToDetail(exercicio);
    }

    [HttpPost]
    public async Task<ActionResult<RecursoCriado>> Create(DadosDoExercicioDoCatalogo request, CancellationToken ct)
    {
        var exercicio = Exercicio.CriarDoCatalogo(Guid.CreateVersion7(), request.Nome, PlanMapper.ToDomain(request.GrupoMuscular));
        DefinirDetalhes(exercicio, request);
        db.Exercicios.Add(exercicio);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = exercicio.Id }, new RecursoCriado(exercicio.Id));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, DadosDoExercicioDoCatalogo request, CancellationToken ct)
    {
        var exercicio = await Catalogo().SingleOrDefaultAsync(e => e.Id == id, ct);
        if (exercicio is null)
        {
            return NotFound();
        }

        exercicio.Atualizar(request.Nome, PlanMapper.ToDomain(request.GrupoMuscular));
        DefinirDetalhes(exercicio, request);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/inativar")]
    public Task<IActionResult> Deactivate(Guid id, CancellationToken ct) => ChangeAsync(id, e => e.Inativar(clock.GetUtcNow()), ct);

    [HttpPost("{id:guid}/reativar")]
    public Task<IActionResult> Reactivate(Guid id, CancellationToken ct) => ChangeAsync(id, e => e.Reativar(), ct);

    /// <summary>Foto: JPEG, PNG ou WebP até 2 MB, no campo "arquivo".</summary>
    [HttpPost("{id:guid}/foto")]
    [RequestSizeLimit(CorpoMaximo)]
    [RequestFormLimits(MultipartBodyLengthLimit = CorpoMaximo)]
    public Task<IActionResult> UploadPhoto(Guid id, IFormFile arquivo, CancellationToken ct) => UploadAsync(id, arquivo, TipoDeMidia.Foto, ct);

    /// <summary>Vídeo curto de execução: MP4 ou WebM até 10 MB, no campo "arquivo".</summary>
    [HttpPost("{id:guid}/video")]
    [RequestSizeLimit(CorpoMaximo)]
    [RequestFormLimits(MultipartBodyLengthLimit = CorpoMaximo)]
    public Task<IActionResult> UploadVideo(Guid id, IFormFile arquivo, CancellationToken ct) => UploadAsync(id, arquivo, TipoDeMidia.Video, ct);

    [HttpPost("{id:guid}/foto/remover")]
    public Task<IActionResult> RemovePhoto(Guid id, CancellationToken ct) => RemoveAsync(id, TipoDeMidia.Foto, ct);

    [HttpPost("{id:guid}/video/remover")]
    public Task<IActionResult> RemoveVideo(Guid id, CancellationToken ct) => RemoveAsync(id, TipoDeMidia.Video, ct);

    /// <summary>Exercícios do catálogo, inclusive inativos.</summary>
    private IQueryable<Exercicio> Catalogo() =>
        db.Exercicios.IgnoreQueryFilters([FiltrosDeConsulta.Ativos]).Where(e => e.PersonalId == null);

    private static void DefinirDetalhes(Exercicio exercicio, DadosDoExercicioDoCatalogo request) =>
        exercicio.DefinirDetalhes(
            [.. request.MusculosSecundarios.Select(PlanMapper.ToDomain)],
            request.Equipamento is { } equipamento ? ExerciseMapper.ToDomain(equipamento) : null,
            request.Instrucoes);

    private async Task<IActionResult> ChangeAsync(Guid id, Action<Exercicio> change, CancellationToken ct)
    {
        var exercicio = await Catalogo().SingleOrDefaultAsync(e => e.Id == id, ct);
        if (exercicio is null)
        {
            return NotFound();
        }

        change(exercicio);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<IActionResult> UploadAsync(Guid id, IFormFile arquivo, TipoDeMidia tipo, CancellationToken ct)
    {
        var exercicio = await Catalogo().SingleOrDefaultAsync(e => e.Id == id, ct);
        if (exercicio is null)
        {
            return NotFound();
        }

        if (arquivo.Length == 0 || arquivo.Length > MidiaDoExercicio.TamanhoMaximo(tipo))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: tipo == TipoDeMidia.Foto ? "A foto precisa ter até 2 MB." : "O vídeo precisa ter até 10 MB.");
        }

        var chave = await midia.GravarAsync(exercicio.Id, arquivo, tipo, ct);
        if (chave is null)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: tipo == TipoDeMidia.Foto ? "Envie a foto em JPEG, PNG ou WebP." : "Envie o vídeo em MP4 ou WebM.");
        }

        var anterior = tipo == TipoDeMidia.Foto ? exercicio.DefinirFoto(chave) : exercicio.DefinirVideo(chave);
        await db.SaveChangesAsync(ct);
        await midia.ApagarAsync(anterior, logger, ct);
        return Ok(mapper.ToDetail(exercicio));
    }

    private async Task<IActionResult> RemoveAsync(Guid id, TipoDeMidia tipo, CancellationToken ct)
    {
        var exercicio = await Catalogo().SingleOrDefaultAsync(e => e.Id == id, ct);
        if (exercicio is null)
        {
            return NotFound();
        }

        var anterior = tipo == TipoDeMidia.Foto ? exercicio.DefinirFoto(null) : exercicio.DefinirVideo(null);
        await db.SaveChangesAsync(ct);
        await midia.ApagarAsync(anterior, logger, ct);
        return NoContent();
    }
}
