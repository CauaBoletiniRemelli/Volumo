using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Volumo.Api.Auth;
using Volumo.Api.Treinos;
using Volumo.Domain.Exercicios;
using Volumo.Infrastructure.Persistencia;
using Volumo.Shared.Treinos;

namespace Volumo.Api.Controllers;

/// <summary>
/// Catálogo da plataforma + exercícios criados pelo personal. Quem vê o quê é decidido pelo filtro
/// de dono: personal vê o catálogo e os próprios; aluno vê o catálogo e os do seu personal.
/// </summary>
[ApiController]
[Route("exercicios")]
public sealed class ExerciciosController(VolumoDbContext db, TimeProvider clock) : ControllerBase
{
    public const int ListLimit = 500;

    [HttpGet]
    [Authorize(Policy = Policies.ContaAtiva)]
    public async Task<IReadOnlyList<ExercicioResumo>> List(
        [FromQuery] string? busca, [FromQuery] Shared.Treinos.GrupoMuscular? grupo, CancellationToken ct)
    {
        var query = db.Exercicios.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = busca.Trim();
            query = query.Where(e => e.Nome.Contains(termo));
        }

        if (grupo is { } g)
        {
            var domainGroup = PlanMapper.ToDomain(g);
            query = query.Where(e => e.GrupoMuscular == domainGroup);
        }

        var exercicios = await query.OrderBy(e => e.Nome).Take(ListLimit).ToListAsync(ct);
        return [.. exercicios.Select(ToResponse)];
    }

    [HttpPost]
    [Authorize(Policy = Policies.Personal)]
    public async Task<ActionResult<ExercicioResumo>> Create(DadosDoExercicio request, CancellationToken ct)
    {
        var exercicio = Exercicio.CriarPersonalizado(
            Guid.CreateVersion7(), User.UsuarioId()!.Value, request.Nome, PlanMapper.ToDomain(request.GrupoMuscular));
        db.Exercicios.Add(exercicio);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), null, ToResponse(exercicio));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Personal)]
    public async Task<IActionResult> Update(Guid id, DadosDoExercicio request, CancellationToken ct)
    {
        var (exercicio, problem) = await FindOwnAsync(id, ct);
        if (problem is not null)
        {
            return problem;
        }

        exercicio!.Atualizar(request.Nome, PlanMapper.ToDomain(request.GrupoMuscular));
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Some da lista e dos novos treinos; treinos que já usam o exercício continuam mostrando o nome.</summary>
    [HttpPost("{id:guid}/inativar")]
    [Authorize(Policy = Policies.Personal)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var (exercicio, problem) = await FindOwnAsync(id, ct);
        if (problem is not null)
        {
            return problem;
        }

        exercicio!.Inativar(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>De outro personal → 404 (o filtro esconde); do catálogo → 403 (visível, mas não é seu).</summary>
    private async Task<(Exercicio? Exercicio, IActionResult? Problem)> FindOwnAsync(Guid id, CancellationToken ct)
    {
        var exercicio = await db.Exercicios.SingleOrDefaultAsync(e => e.Id == id, ct);
        if (exercicio is null)
        {
            return (null, NotFound());
        }

        return exercicio.PersonalId == User.UsuarioId()
            ? (exercicio, null)
            : (null, Problem(statusCode: StatusCodes.Status403Forbidden, title: "Só exercícios criados por você podem ser alterados."));
    }

    private static ExercicioResumo ToResponse(Exercicio e) =>
        new(e.Id, e.Nome, PlanMapper.ToContract(e.GrupoMuscular), e.DoCatalogo);
}
