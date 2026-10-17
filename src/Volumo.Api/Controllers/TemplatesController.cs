using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Volumo.Api.Auth;
using Volumo.Api.Treinos;
using Volumo.Domain.Treinos;
using Volumo.Infrastructure.Persistencia;
using Volumo.Shared.Treinos;

namespace Volumo.Api.Controllers;

/// <summary>Modelos de treino do personal. Template de outro personal → 404 (filtro de dono).</summary>
[ApiController]
[Route("templates")]
[Authorize(Policy = Policies.Personal)]
public sealed class TemplatesController(VolumoDbContext db, PlanMapper mapper, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<TemplateResumo>> List(CancellationToken ct)
    {
        var templates = await db.TemplatesTreino.AsNoTracking().OrderBy(t => t.Nome).ToListAsync(ct);
        return [.. templates.Select(t => new TemplateResumo(t.Id, t.Nome, t.Itens.Count))];
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TemplateDetalhe>> Get(Guid id, CancellationToken ct)
    {
        var template = await db.TemplatesTreino.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, ct);
        return template is null ? NotFound() : new TemplateDetalhe(template.Id, template.Nome, await mapper.ToOutputAsync(template, ct));
    }

    [HttpPost]
    public async Task<ActionResult<RecursoCriado>> Create(PlanoDeTreinoEntrada request, CancellationToken ct)
    {
        var itens = await mapper.ToDefinitionsAsync(request, ct);
        if (itens is null)
        {
            return InvalidExercise();
        }

        var template = TemplateTreino.Criar(Guid.CreateVersion7(), User.UsuarioId()!.Value, request.Nome);
        template.SubstituirItens(itens);
        db.TemplatesTreino.Add(template);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = template.Id }, new RecursoCriado(template.Id));
    }

    /// <summary>Substitui nome e exercícios. Treinos já gerados a partir dele não mudam.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, PlanoDeTreinoEntrada request, CancellationToken ct)
    {
        var template = await db.TemplatesTreino.SingleOrDefaultAsync(t => t.Id == id, ct);
        if (template is null)
        {
            return NotFound();
        }

        var itens = await mapper.ToDefinitionsAsync(request, ct);
        if (itens is null)
        {
            return InvalidExercise();
        }

        template.Renomear(request.Nome);
        template.SubstituirItens(itens);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/inativar")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var template = await db.TemplatesTreino.SingleOrDefaultAsync(t => t.Id == id, ct);
        if (template is null)
        {
            return NotFound();
        }

        template.Inativar(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Gera um treino para um aluno com vínculo ativo (cópia independente do template).</summary>
    [HttpPost("{id:guid}/atribuir")]
    public async Task<ActionResult<RecursoCriado>> Assign(Guid id, AtribuicaoDeTemplate request, CancellationToken ct)
    {
        var template = await db.TemplatesTreino.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, ct);
        var personalId = User.UsuarioId()!.Value;
        var alunoAtivo = await db.VinculosPersonalAluno.AnyAsync(
            v => v.PersonalId == personalId && v.AlunoId == request.AlunoId && v.EncerradoEm == null, ct);
        if (template is null || !alunoAtivo)
        {
            return NotFound();
        }

        var treino = template.GerarTreinoPara(Guid.CreateVersion7(), request.AlunoId);
        db.Treinos.Add(treino);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(TreinosController.Get), "Treinos", new { id = treino.Id }, new RecursoCriado(treino.Id));
    }

    private ObjectResult InvalidExercise() =>
        Problem(statusCode: StatusCodes.Status400BadRequest, title: "Algum exercício não existe ou não está disponível para você.");
}
