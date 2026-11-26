using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Volumo.Api.Auth;
using Volumo.Api.Painel;
using Volumo.Api.Sessoes;
using Volumo.Api.Treinos;
using Volumo.Domain.Treinos;
using Volumo.Infrastructure.Persistencia;
using Volumo.Shared.Alunos;
using Volumo.Shared.Painel;
using Volumo.Shared.Sessoes;
using Volumo.Shared.Treinos;

namespace Volumo.Api.Controllers;

/// <summary>Alunos do personal (vínculo ativo). Aluno de outro personal ou ex-aluno → 404.</summary>
[ApiController]
[Route("alunos")]
[Authorize(Policy = Policies.Personal)]
public sealed class AlunosController(VolumoDbContext db, PlanMapper mapper, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<AlunoResumo>> List(CancellationToken ct)
    {
        var personalId = User.UsuarioId()!.Value;
        return await (
                from v in db.VinculosPersonalAluno
                join a in db.PerfisAluno on v.AlunoId equals a.Id
                where v.PersonalId == personalId && v.EncerradoEm == null
                orderby a.Nome
                select new AlunoResumo(a.Id, a.Nome, v.IniciadoEm))
            .ToListAsync(ct);
    }

    [HttpGet("{alunoId:guid}/treinos")]
    public async Task<ActionResult<IReadOnlyList<TreinoResumo>>> ListTreinos(Guid alunoId, CancellationToken ct)
    {
        if (!await IsActiveStudentAsync(alunoId, ct))
        {
            return NotFound();
        }

        var treinos = await db.Treinos.AsNoTracking().Where(t => t.AlunoId == alunoId).OrderBy(t => t.Ordem).ThenBy(t => t.Nome).ToListAsync(ct);
        return treinos.Select(TreinosController.ToSummary).ToList();
    }

    /// <summary>
    /// Ordem dos treinos na lista do aluno (Treino A, B, C...). A lista precisa ter cada treino ativo
    /// do aluno exatamente uma vez; arquivados ficam de fora.
    /// </summary>
    [HttpPut("{alunoId:guid}/treinos/ordem")]
    public async Task<IActionResult> ReorderTreinos(Guid alunoId, OrdemDosTreinos request, CancellationToken ct)
    {
        if (!await IsActiveStudentAsync(alunoId, ct))
        {
            return NotFound();
        }

        var treinos = await db.Treinos.Where(t => t.AlunoId == alunoId).ToListAsync(ct);
        var mesmosTreinos = request.TreinoIds.Count == treinos.Count
            && request.TreinoIds.Distinct().Count() == treinos.Count
            && request.TreinoIds.All(id => treinos.Exists(t => t.Id == id));
        if (!mesmosTreinos)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "A lista mudou enquanto você reordenava.",
                detail: "Atualize a página e tente de novo: a nova ordem precisa ter cada treino ativo do aluno uma vez.");
        }

        for (var i = 0; i < request.TreinoIds.Count; i++)
        {
            treinos.Find(t => t.Id == request.TreinoIds[i])!.Posicionar(i + 1);
        }

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Histórico de sessões do aluno, mais recentes primeiro.</summary>
    [HttpGet("{alunoId:guid}/sessoes")]
    public async Task<ActionResult<IReadOnlyList<SessaoResumo>>> ListSessoes(Guid alunoId, [FromServices] SessionQueries queries, CancellationToken ct)
    {
        if (!await IsActiveStudentAsync(alunoId, ct))
        {
            return NotFound();
        }

        return Ok(await queries.SummariesAsync(db.SessoesTreino.Where(s => s.AlunoId == alunoId), ct));
    }

    /// <summary>Monta um treino direto para o aluno, sem template.</summary>
    [HttpPost("{alunoId:guid}/treinos")]
    public async Task<ActionResult<RecursoCriado>> CreateTreino(Guid alunoId, PlanoDeTreinoEntrada request, CancellationToken ct)
    {
        if (!await IsActiveStudentAsync(alunoId, ct))
        {
            return NotFound();
        }

        var plano = await mapper.ToDefinitionsAsync(request, ct);
        if (plano is null)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Algum exercício não existe ou não está disponível para você.");
        }

        var treino = Treino.CriarPeloPersonal(Guid.CreateVersion7(), User.UsuarioId()!.Value, alunoId, request.Nome);
        plano.ApplyTo(treino);
        await TreinosController.PlaceAtEndAsync(db, treino, ct);
        db.Treinos.Add(treino);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(TreinosController.Get), "Treinos", new { id = treino.Id }, new RecursoCriado(treino.Id));
    }

    /// <summary>
    /// Encerra o acompanhamento. O aluno continua com os treinos e o histórico; o personal deixa de
    /// enxergar os dados dele.
    /// </summary>
    [HttpPost("{alunoId:guid}/encerrar-vinculo")]
    public async Task<IActionResult> EndLink(Guid alunoId, CancellationToken ct)
    {
        var personalId = User.UsuarioId()!.Value;
        var vinculo = await db.VinculosPersonalAluno.SingleOrDefaultAsync(
            v => v.PersonalId == personalId && v.AlunoId == alunoId && v.EncerradoEm == null, ct);
        if (vinculo is null)
        {
            return NotFound();
        }

        vinculo.Encerrar(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Como o painel mede a estagnação deste aluno: dias ou sessões sem subir peso.</summary>
    [HttpPut("{alunoId:guid}/estagnacao")]
    public async Task<IActionResult> ConfigureStagnation(Guid alunoId, ConfiguracaoDeEstagnacao request, CancellationToken ct)
    {
        var personalId = User.UsuarioId()!.Value;
        var vinculo = await db.VinculosPersonalAluno.SingleOrDefaultAsync(
            v => v.PersonalId == personalId && v.AlunoId == alunoId && v.EncerradoEm == null, ct);
        if (vinculo is null)
        {
            return NotFound();
        }

        vinculo.ConfigurarEstagnacao(PainelQueries.ToDomain(request.Criterio), request.Limite);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private Task<bool> IsActiveStudentAsync(Guid alunoId, CancellationToken ct)
    {
        var personalId = User.UsuarioId()!.Value;
        return db.VinculosPersonalAluno.AnyAsync(v => v.PersonalId == personalId && v.AlunoId == alunoId && v.EncerradoEm == null, ct);
    }
}
