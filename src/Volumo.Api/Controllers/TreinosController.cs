using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Volumo.Api.Auth;
using Volumo.Api.Treinos;
using Volumo.Domain.Treinos;
using Volumo.Infrastructure.Persistencia;
using Volumo.Shared.Treinos;

namespace Volumo.Api.Controllers;

/// <summary>
/// Treinos dos alunos. O filtro de dono deixa ver: o aluno, os próprios; o personal, os dos alunos
/// com vínculo ativo. Alterar: só o personal que prescreveu, enquanto o vínculo durar.
/// </summary>
[ApiController]
[Route("treinos")]
public sealed class TreinosController(VolumoDbContext db, PlanMapper mapper, TimeProvider clock) : ControllerBase
{
    /// <summary>Tela principal do aluno: os treinos ativos dele.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.Aluno)]
    public async Task<IReadOnlyList<TreinoResumo>> ListMine(CancellationToken ct)
    {
        var alunoId = User.UsuarioId()!.Value;
        var treinos = await db.Treinos.AsNoTracking().Where(t => t.AlunoId == alunoId).OrderBy(t => t.Nome).ToListAsync(ct);
        return [.. treinos.Select(ToSummary)];
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.ContaAtiva)]
    public async Task<ActionResult<TreinoDetalhe>> Get(Guid id, CancellationToken ct)
    {
        var treino = await db.Treinos.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, ct);
        return treino is null
            ? NotFound()
            : new TreinoDetalhe(treino.Id, treino.Nome, treino.AlunoId, treino.PersonalId, await mapper.ToOutputAsync(treino, ct));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Personal)]
    public async Task<IActionResult> Update(Guid id, PlanoDeTreinoEntrada request, CancellationToken ct)
    {
        var (treino, problem) = await FindEditableAsync(id, ct);
        if (problem is not null)
        {
            return problem;
        }

        var itens = await mapper.ToDefinitionsAsync(request, ct);
        if (itens is null)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Algum exercício não existe ou não está disponível para você.");
        }

        treino!.Renomear(request.Nome);
        treino.SubstituirItens(itens);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Arquiva o treino: some da lista do aluno; o histórico de sessões continua.</summary>
    [HttpPost("{id:guid}/inativar")]
    [Authorize(Policy = Policies.Personal)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var (treino, problem) = await FindEditableAsync(id, ct);
        if (problem is not null)
        {
            return problem;
        }

        treino!.Inativar(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    internal static TreinoResumo ToSummary(Treino t) => new(t.Id, t.Nome, t.TemplateOrigemId, t.Itens.Count);

    /// <summary>Invisível → 404; visível mas prescrito por outro (ex.: montado pelo aluno) → 403.</summary>
    private async Task<(Treino? Treino, IActionResult? Problem)> FindEditableAsync(Guid id, CancellationToken ct)
    {
        var treino = await db.Treinos.SingleOrDefaultAsync(t => t.Id == id, ct);
        if (treino is null)
        {
            return (null, NotFound());
        }

        return treino.PersonalId == User.UsuarioId()
            ? (treino, null)
            : (null, Problem(statusCode: StatusCodes.Status403Forbidden, title: "Só quem prescreveu o treino pode alterá-lo."));
    }
}
