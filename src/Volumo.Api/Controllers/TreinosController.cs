using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Volumo.Api.Auth;
using Volumo.Api.Exportacao;
using Volumo.Api.Sessoes;
using Volumo.Api.Treinos;
using Volumo.Domain.Treinos;
using Volumo.Infrastructure.Persistencia;
using Volumo.Shared.Sessoes;
using Volumo.Shared.Treinos;

namespace Volumo.Api.Controllers;

/// <summary>
/// Treinos dos alunos. O filtro de dono deixa ver: o aluno, os próprios; o personal, os dos alunos
/// com vínculo ativo. Alterar: só o personal que prescreveu, enquanto o vínculo durar.
/// </summary>
[ApiController]
[Route("treinos")]
public sealed class TreinosController(
    VolumoDbContext db, PlanMapper mapper, SessionQueries queries, StudentNotes notes, WorkoutExporter exporter, TimeProvider clock) : ControllerBase
{
    /// <summary>Tela principal do aluno: os treinos ativos dele.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.Aluno)]
    public async Task<IReadOnlyList<TreinoResumo>> ListMine(CancellationToken ct)
    {
        var alunoId = User.UsuarioId()!.Value;
        var treinos = await db.Treinos.AsNoTracking().Where(t => t.AlunoId == alunoId).OrderBy(t => t.Ordem).ThenBy(t => t.Nome).ToListAsync(ct);
        return [.. treinos.Select(ToSummary)];
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.ContaAtiva)]
    public async Task<ActionResult<TreinoDetalhe>> Get(Guid id, CancellationToken ct)
    {
        var treino = await db.Treinos.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, ct);
        return treino is null
            ? NotFound()
            : await mapper.ToDetailAsync(treino, ct);
    }

    /// <summary>Evolução de cada exercício nas últimas sessões do treino (gráfico de carga e repetições).</summary>
    [HttpGet("{id:guid}/evolucao")]
    [Authorize(Policy = Policies.ContaAtiva)]
    public async Task<ActionResult<EvolucaoDoTreino>> Evolution(Guid id, CancellationToken ct)
    {
        var treino = await db.Treinos.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, ct);
        return treino is null ? NotFound() : await queries.EvolutionAsync(treino, ct);
    }

    /// <summary>
    /// Treino em Excel ou PDF: o plano (com as observações) e a evolução das últimas sessões.
    /// <paramref name="fuso"/>: diferença do horário de quem pede para o UTC, em minutos (Brasília = −180).
    /// </summary>
    [HttpGet("{id:guid}/exportar")]
    [Authorize(Policy = Policies.ContaAtiva)]
    public async Task<IActionResult> Export(Guid id, [FromQuery] FormatoDeExportacao formato, [FromQuery] int? fuso, CancellationToken ct)
    {
        var treino = await db.Treinos.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, ct);
        if (treino is null)
        {
            return NotFound();
        }

        var arquivo = exporter.Workout(
            await mapper.ToDetailAsync(treino, ct), await queries.EvolutionAsync(treino, ct), formato, WorkoutExportTimeZone.From(fuso));
        return File(arquivo.Content, arquivo.ContentType, arquivo.FileName);
    }

    /// <summary>
    /// Observação do próprio aluno num exercício do treino (a do personal continua intacta). Texto
    /// vazio limpa. Offline, o app manda junto com a sessão.
    /// </summary>
    [HttpPut("{id:guid}/exercicios/{exercicioId:guid}/observacao-do-aluno")]
    [Authorize(Policy = Policies.Aluno)]
    public async Task<IActionResult> SetStudentNote(Guid id, Guid exercicioId, ObservacaoDoAlunoEntrada request, CancellationToken ct)
    {
        var treino = await db.Treinos.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id && t.AlunoId == User.UsuarioId(), ct);
        if (treino is null || !treino.Itens.Any(i => i.ExercicioId == exercicioId) && !treino.Cardios.Any(c => c.ExercicioId == exercicioId))
        {
            return NotFound();
        }

        await notes.ApplyAsync(treino.AlunoId, id, [new StudentNote(exercicioId, request.Texto, clock.GetUtcNow())], ct);
        await db.SaveChangesAsync(ct);
        return NoContent();
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

        var plano = await mapper.ToDefinitionsAsync(request, ct);
        if (plano is null)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Algum exercício não existe ou não está disponível para você.");
        }

        treino!.Renomear(request.Nome);
        plano.ApplyTo(treino);
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

    /// <summary>Treino novo entra no fim da lista do aluno.</summary>
    internal static async Task PlaceAtEndAsync(VolumoDbContext db, Treino treino, CancellationToken ct)
    {
        var ultima = await db.Treinos.Where(t => t.AlunoId == treino.AlunoId).MaxAsync(t => (int?)t.Ordem, ct);
        treino.Posicionar((ultima ?? 0) + 1);
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
