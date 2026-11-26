using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Volumo.Api.Auth;
using Volumo.Domain.Treinos;
using Volumo.Infrastructure.Persistencia;
using Volumo.Shared.Treinos;

namespace Volumo.Api.Controllers;

/// <summary>
/// Presets de séries: os prontos da plataforma (no código, só leitura) e os próprios de cada conta.
/// Preset de outra conta → 404 (filtro de dono); pronto → 403 ao tentar alterar.
/// </summary>
[ApiController]
[Route("presets")]
[Authorize(Policy = Policies.PersonalOuAluno)]
public sealed class PresetsController(VolumoDbContext db, TimeProvider clock) : ControllerBase
{
    public const int MaximoPorConta = 50;

    /// <summary>Prontos primeiro, na ordem da plataforma; depois os próprios, por nome.</summary>
    [HttpGet]
    public async Task<IReadOnlyList<PresetDeSeriesSaida>> List(CancellationToken ct)
    {
        var proprios = await db.PresetsDeSeries.AsNoTracking().OrderBy(p => p.Nome).ToListAsync(ct);
        return [.. PresetDeSeries.Prontos.Concat(proprios).Select(ToOutput)];
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PresetDeSeriesSaida>> Get(Guid id, CancellationToken ct)
    {
        var preset = PresetDeSeries.Prontos.FirstOrDefault(p => p.Id == id)
            ?? await db.PresetsDeSeries.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct);
        return preset is null ? NotFound() : ToOutput(preset);
    }

    [HttpPost]
    public async Task<ActionResult<RecursoCriado>> Create(PresetDeSeriesEntrada request, CancellationToken ct)
    {
        if (await db.PresetsDeSeries.CountAsync(ct) >= MaximoPorConta)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: $"Você pode ter até {MaximoPorConta} presets. Arquive algum para criar outro.");
        }

        var preset = PresetDeSeries.Criar(Guid.CreateVersion7(), User.UsuarioId()!.Value, request.Nome, ToDomain(request.Series));
        db.PresetsDeSeries.Add(preset);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = preset.Id }, new RecursoCriado(preset.Id));
    }

    /// <summary>Substitui nome e séries. Treinos em que ele já foi aplicado não mudam.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, PresetDeSeriesEntrada request, CancellationToken ct)
    {
        var (preset, problem) = await FindEditableAsync(id, ct);
        if (problem is not null)
        {
            return problem;
        }

        preset!.Renomear(request.Nome);
        preset.DefinirSeries(ToDomain(request.Series));
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/inativar")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var (preset, problem) = await FindEditableAsync(id, ct);
        if (problem is not null)
        {
            return problem;
        }

        preset!.Inativar(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<(PresetDeSeries? Preset, IActionResult? Problem)> FindEditableAsync(Guid id, CancellationToken ct)
    {
        if (PresetDeSeries.Prontos.Any(p => p.Id == id))
        {
            return (null, Problem(statusCode: StatusCodes.Status403Forbidden, title: "Os presets prontos não podem ser alterados. Crie um seu a partir dele."));
        }

        var preset = await db.PresetsDeSeries.SingleOrDefaultAsync(p => p.Id == id, ct);
        return preset is null ? (null, NotFound()) : (preset, null);
    }

    private static List<Serie> ToDomain(IEnumerable<SerieDoPreset> series) =>
        [.. series.Select(s => Serie.Criar(Enum.Parse<TipoSerie>(s.Tipo.ToString()), s.RepeticoesMinimas, s.RepeticoesMaximas, s.DescansoSegundos))];

    private static PresetDeSeriesSaida ToOutput(PresetDeSeries preset) => new(
        preset.Id,
        preset.Nome,
        preset.Pronto,
        [.. preset.Series.Select(s => new SerieDoPreset(Enum.Parse<TipoDeSerie>(s.Tipo.ToString()), s.RepeticoesMinimas, s.RepeticoesMaximas, s.DescansoSegundos))]);
}
