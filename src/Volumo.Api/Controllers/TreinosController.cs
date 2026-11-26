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
/// com vínculo ativo. Alterar: o treino prescrito, só o personal que prescreveu (com 2FA), enquanto o
/// vínculo durar; o montado pelo aluno, só o próprio aluno (inclusive o personal nos próprios treinos).
/// </summary>
[ApiController]
[Route("treinos")]
public sealed class TreinosController(
    VolumoDbContext db,
    PlanMapper mapper,
    SessionQueries queries,
    StudentNotes notes,
    WorkoutExporter exporter,
    IAuthorizationService authorization,
    TimeProvider clock) : ControllerBase
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

    /// <summary>Treino montado pelo próprio aluno (sem personal). Entra no fim da lista dele.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.Aluno)]
    public async Task<ActionResult<RecursoCriado>> CreateMine(PlanoDeTreinoEntrada request, CancellationToken ct)
    {
        var plano = await mapper.ToDefinitionsAsync(request, ct);
        if (plano is null)
        {
            return InvalidExercise();
        }

        var treino = Treino.CriarPeloAluno(Guid.CreateVersion7(), User.UsuarioId()!.Value, request.Nome);
        plano.ApplyTo(treino);
        await PlaceAtEndAsync(db, treino, ct);
        db.Treinos.Add(treino);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = treino.Id }, new RecursoCriado(treino.Id));
    }

    /// <summary>
    /// Ordem dos treinos que o próprio aluno montou: a lista traz cada um deles uma vez, na nova ordem.
    /// Eles trocam de lugar entre si; os prescritos pelo personal ficam nas posições que o personal escolheu.
    /// </summary>
    [HttpPut("ordem")]
    [Authorize(Policy = Policies.Aluno)]
    public async Task<IActionResult> ReorderMine(OrdemDosTreinos request, CancellationToken ct)
    {
        var alunoId = User.UsuarioId()!.Value;
        var lista = await db.Treinos.Where(t => t.AlunoId == alunoId).OrderBy(t => t.Ordem).ThenBy(t => t.Nome).ToListAsync(ct);
        var proprios = lista.Where(t => t.PersonalId is null).ToList();
        var mesmosTreinos = request.TreinoIds.Count == proprios.Count
            && request.TreinoIds.Distinct().Count() == proprios.Count
            && request.TreinoIds.All(id => proprios.Exists(t => t.Id == id));
        if (!mesmosTreinos)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "A lista mudou enquanto você reordenava.",
                detail: "Atualize a página e tente de novo: a nova ordem precisa ter cada treino montado por você uma vez.");
        }

        // Os próprios ocupam as mesmas vagas, na nova ordem; depois a lista inteira é numerada de 1 a N
        // (corrige também treinos antigos com ordem 0).
        var novaOrdem = new Queue<Treino>(request.TreinoIds.Select(id => proprios.Find(p => p.Id == id)!));
        var resultado = lista.Select(t => t.PersonalId is null ? novaOrdem.Dequeue() : t).ToList();
        for (var i = 0; i < resultado.Count; i++)
        {
            resultado[i].Posicionar(i + 1);
        }

        await db.SaveChangesAsync(ct);
        return NoContent();
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
    [Authorize(Policy = Policies.PersonalOuAluno)]
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
            return InvalidExercise();
        }

        treino!.Renomear(request.Nome);
        plano.ApplyTo(treino);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Dias da semana do treino (Treino A na segunda e na quinta). Quem altera o treino marca os dias: o
    /// personal nos prescritos, o aluno nos que ele montou.
    /// </summary>
    [HttpPut("{id:guid}/dias")]
    [Authorize(Policy = Policies.PersonalOuAluno)]
    public async Task<IActionResult> SetDays(Guid id, DiasDoTreino request, CancellationToken ct)
    {
        if (!request.Dias.All(Enum.IsDefined))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Dia da semana inválido.");
        }

        var (treino, problem) = await FindEditableAsync(id, ct);
        if (problem is not null)
        {
            return problem;
        }

        treino!.MarcarDias(request.Dias.ParaDias());
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Arquiva o treino: some da lista do aluno; o histórico de sessões continua.</summary>
    [HttpPost("{id:guid}/inativar")]
    [Authorize(Policy = Policies.PersonalOuAluno)]
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

    internal static TreinoResumo ToSummary(Treino t) => new(t.Id, t.Nome, t.TemplateOrigemId, t.Itens.Count, t.PersonalId is null, t.Dias.ParaLista());

    /// <summary>
    /// Invisível → 404. Visível, mas de outro dono → 403: o montado pelo aluno só ele altera; o prescrito,
    /// só o personal que prescreveu e com 2FA (a mesma exigência das outras rotas de personal).
    /// </summary>
    private async Task<(Treino? Treino, IActionResult? Problem)> FindEditableAsync(Guid id, CancellationToken ct)
    {
        var treino = await db.Treinos.SingleOrDefaultAsync(t => t.Id == id, ct);
        if (treino is null)
        {
            return (null, NotFound());
        }

        var usuarioId = User.UsuarioId();
        if (treino.PersonalId is null)
        {
            return treino.AlunoId == usuarioId
                ? (treino, null)
                : (null, Problem(statusCode: StatusCodes.Status403Forbidden, title: "Este treino foi montado pelo aluno: só ele pode alterá-lo."));
        }

        if (treino.PersonalId != usuarioId)
        {
            return (null, Problem(statusCode: StatusCodes.Status403Forbidden, title: "Só quem prescreveu o treino pode alterá-lo."));
        }

        return (await authorization.AuthorizeAsync(User, Policies.Personal)).Succeeded
            ? (treino, null)
            : (null, Problem(statusCode: StatusCodes.Status403Forbidden, title: "Ative a verificação em duas etapas para alterar os treinos dos alunos."));
    }

    private ObjectResult InvalidExercise() =>
        Problem(statusCode: StatusCodes.Status400BadRequest, title: "Algum exercício não existe ou não está disponível para você.");
}
