using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Volumo.Api.Auth;
using Volumo.Domain.Vinculos;
using Volumo.Infrastructure.Identity;
using Volumo.Infrastructure.Persistencia;
using Volumo.Shared.Convites;

namespace Volumo.Api.Controllers;

/// <summary>
/// Convites de personal para aluno. Regra de dono: o personal só enxerga e mexe nos próprios
/// convites; convite de outro personal responde 404 (não revela que existe).
/// </summary>
[ApiController]
[Route("convites")]
public sealed class ConvitesController(
    VolumoDbContext db,
    UserManager<Usuario> userManager,
    AccountEmails emails,
    TimeProvider clock) : ControllerBase
{
    public const int ListLimit = 200;

    /// <summary>Convida um aluno por e-mail. Um convite pendente para o mesmo e-mail é substituído.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.Personal)]
    public async Task<ActionResult<ConviteEnviado>> Create(NovoConvite request, CancellationToken ct)
    {
        var personalId = User.UsuarioId()!.Value;
        var personal = await db.PerfisPersonal.SingleAsync(p => p.Id == personalId, ct);
        var now = clock.GetUtcNow();

        if (await IsAlreadyMyStudentAsync(personalId, request.Email, ct))
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Esta pessoa já é sua aluna.");
        }

        var (convite, token) = ConviteAluno.Criar(Guid.CreateVersion7(), personalId, request.Email, now);
        var pendentes = await db.ConvitesAluno
            .Where(c => c.PersonalId == personalId && c.Email == convite.Email && c.AceitoEm == null && c.RevogadoEm == null)
            .ToListAsync(ct);
        foreach (var anterior in pendentes)
        {
            anterior.Revogar(now);
        }

        db.ConvitesAluno.Add(convite);
        await db.SaveChangesAsync(ct);
        await emails.SendInviteAsync(convite.Email, personal.Nome, token, convite.ExpiraEm, ct);

        return CreatedAtAction(nameof(List), null, ToResponse(convite, now));
    }

    [HttpGet]
    [Authorize(Policy = Policies.Personal)]
    public async Task<IReadOnlyList<ConviteEnviado>> List(CancellationToken ct)
    {
        var personalId = User.UsuarioId()!.Value;
        var now = clock.GetUtcNow();
        var convites = await db.ConvitesAluno.AsNoTracking()
            .Where(c => c.PersonalId == personalId)
            .OrderByDescending(c => c.CriadoEm)
            .Take(ListLimit)
            .ToListAsync(ct);
        return [.. convites.Select(c => ToResponse(c, now))];
    }

    [HttpPost("{id:guid}/revogar")]
    [Authorize(Policy = Policies.Personal)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        var personalId = User.UsuarioId()!.Value;
        var convite = await db.ConvitesAluno.SingleOrDefaultAsync(c => c.Id == id && c.PersonalId == personalId, ct);
        if (convite is null)
        {
            return NotFound();
        }

        convite.Revogar(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Tela de aceite: quem convidou e se o e-mail já tem conta. Só quem tem o link consegue.</summary>
    [HttpPost("consultar")]
    [AllowAnonymous]
    public async Task<ActionResult<ResumoDoConvite>> Preview(TokenDeConvite request, CancellationToken ct)
    {
        var convite = await FindAcceptableAsync(request.Token, ct);
        if (convite is null)
        {
            return InvalidInvite();
        }

        var personal = await db.PerfisPersonal.SingleOrDefaultAsync(p => p.Id == convite.PersonalId, ct);
        if (personal is null)
        {
            return InvalidInvite();
        }

        var contaExistente = await userManager.FindByEmailAsync(convite.Email) is not null;
        return new ResumoDoConvite(personal.Nome, convite.Email, convite.ExpiraEm, contaExistente);
    }

    /// <summary>
    /// Aceite por quem já tem conta de aluno. O convite precisa ter sido enviado para o e-mail da
    /// conta logada. Se o aluno já tem personal ativo, precisa encerrar o vínculo antes; se o personal
    /// atual está inativo, o vínculo antigo é encerrado automaticamente.
    /// </summary>
    [HttpPost("aceitar")]
    [Authorize(Policy = Policies.Aluno)]
    public async Task<IActionResult> Accept(TokenDeConvite request, CancellationToken ct)
    {
        var convite = await FindAcceptableAsync(request.Token, ct);
        if (convite is null || !await db.PerfisPersonal.AnyAsync(p => p.Id == convite.PersonalId, ct))
        {
            return InvalidInvite();
        }

        var alunoId = User.UsuarioId()!.Value;
        var usuario = await userManager.FindByIdAsync(alunoId.ToString());
        if (!string.Equals(usuario?.NormalizedEmail, convite.Email.ToUpperInvariant(), StringComparison.Ordinal))
        {
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Este convite foi enviado para outro e-mail.");
        }

        var aluno = await db.PerfisAluno.SingleAsync(a => a.Id == alunoId, ct);
        var vinculoAtivo = await db.VinculosPersonalAluno.SingleOrDefaultAsync(v => v.AlunoId == alunoId && v.EncerradoEm == null, ct);
        var personalAtual = vinculoAtivo is null
            ? null
            : await db.PerfisPersonal.IgnoreQueryFilters([FiltrosDeConsulta.Ativos]).SingleAsync(p => p.Id == vinculoAtivo.PersonalId, ct);

        db.VinculosPersonalAluno.Add(AceiteDeConvite.Aceitar(convite, request.Token, aluno, vinculoAtivo, personalAtual, clock.GetUtcNow()));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Dois aceites simultâneos: o índice único de vínculo ativo barra o segundo.
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Você já tem um personal.");
        }

        return NoContent();
    }

    private async Task<ConviteAluno?> FindAcceptableAsync(string token, CancellationToken ct)
    {
        var hash = ConviteAluno.CalcularHash(token);
        if (hash is null)
        {
            return null;
        }

        var convite = await db.ConvitesAluno.SingleOrDefaultAsync(c => c.TokenHash == hash, ct);
        return convite is not null && convite.PodeSerAceito(clock.GetUtcNow()) ? convite : null;
    }

    private async Task<bool> IsAlreadyMyStudentAsync(Guid personalId, string email, CancellationToken ct)
    {
        var usuario = await userManager.FindByEmailAsync(email);
        return usuario is not null
            && await db.VinculosPersonalAluno.AnyAsync(v => v.PersonalId == personalId && v.AlunoId == usuario.Id && v.EncerradoEm == null, ct);
    }

    private static ConviteEnviado ToResponse(ConviteAluno convite, DateTimeOffset now) =>
        new(convite.Id, convite.Email, convite.SituacaoEm(now).ToString(), convite.CriadoEm, convite.ExpiraEm);

    private ObjectResult InvalidInvite() =>
        Problem(statusCode: StatusCodes.Status400BadRequest, title: "Convite inválido, expirado ou já usado.");
}
