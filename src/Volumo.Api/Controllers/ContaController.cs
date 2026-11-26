using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Volumo.Api.Auth;
using Volumo.Domain.Usuarios;
using Volumo.Infrastructure.Identity;
using Volumo.Infrastructure.Persistencia;
using Volumo.Shared.Autenticacao;
using CorpoDoDesenho = Volumo.Shared.Autenticacao.CorpoDoDesenho;
using DomainBody = Volumo.Domain.Usuarios.CorpoDoDesenho;

namespace Volumo.Api.Controllers;

/// <summary>A própria conta. Qualquer conta logada, inclusive inativa (para poder reativar).</summary>
[ApiController]
[Route("conta")]
[Authorize]
public sealed class ContaController(
    UserManager<Usuario> userManager,
    VolumoDbContext db,
    SessionIssuer sessions,
    RefreshTokenService refreshTokens,
    TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DadosDaConta>> Get(CancellationToken ct)
    {
        var usuario = await CurrentUserAsync();
        if (usuario is null)
        {
            return Unauthorized();
        }

        var papeis = await userManager.GetRolesAsync(usuario);
        return new DadosDaConta(
            usuario.Id,
            usuario.Email!,
            [.. papeis],
            usuario.TwoFactorEnabled,
            PrecisaConfigurarDoisFatores: papeis.Contains(Papeis.Personal) && !usuario.TwoFactorEnabled,
            ContaInativa: await sessions.IsInactiveAsync(usuario.Id, ct),
            CorpoDoDesenho: await ProfileAsync(usuario.Id, ct) is { } perfil ? Enum.Parse<CorpoDoDesenho>(perfil.CorpoDoDesenho.ToString()) : null);
    }

    /// <summary>Escolhe o desenho do corpo (masculino ou feminino) que mostra os músculos dos exercícios.</summary>
    [HttpPut("desenho-do-corpo")]
    [Authorize(Policy = Policies.ContaAtiva)]
    public async Task<IActionResult> SetBodyDrawing(PreferenciaDeDesenho request, CancellationToken ct)
    {
        // Personal tem também o perfil de aluno (para os próprios treinos): a preferência vale nos dois.
        var perfis = await ProfilesAsync(User.UsuarioId()!.Value, ct);
        if (perfis.Count == 0)
        {
            return NotFound();
        }

        var corpo = Enum.IsDefined(request.CorpoDoDesenho) ? Enum.Parse<DomainBody>(request.CorpoDoDesenho.ToString()) : (DomainBody)0;
        foreach (var perfil in perfis)
        {
            perfil.DefinirCorpoDoDesenho(corpo);
        }

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Perfil de aluno ou de personal da conta, inclusive pausado (o "GET /conta" funciona com a conta pausada).</summary>
    private async Task<Perfil?> ProfileAsync(Guid usuarioId, CancellationToken ct) =>
        (Perfil?)await db.PerfisAluno.IgnoreQueryFilters([FiltrosDeConsulta.Ativos]).SingleOrDefaultAsync(p => p.Id == usuarioId, ct)
        ?? await db.PerfisPersonal.IgnoreQueryFilters([FiltrosDeConsulta.Ativos]).SingleOrDefaultAsync(p => p.Id == usuarioId, ct);

    /// <summary>
    /// Pausa a conta (nada é apagado). Pede a senha de novo e encerra todas as sessões. Para voltar,
    /// basta entrar e chamar <c>/conta/reativar</c>. Os vínculos com personal/alunos continuam.
    /// </summary>
    [HttpPost("inativar")]
    public async Task<IActionResult> Deactivate(ConfirmacaoDeSenha request, CancellationToken ct)
    {
        var usuario = await CurrentUserAsync();
        if (usuario is null)
        {
            return Unauthorized();
        }

        if (!await userManager.CheckPasswordAsync(usuario, request.Senha))
        {
            await userManager.AccessFailedAsync(usuario);
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Senha incorreta.");
        }

        var now = clock.GetUtcNow();
        foreach (var perfil in await ProfilesAsync(usuario.Id, ct))
        {
            perfil.Inativar(now);
        }

        await db.SaveChangesAsync(ct);
        await refreshTokens.RevokeAllForUserAsync(usuario.Id, ct);
        return NoContent();
    }

    /// <summary>Reativa a conta pausada e devolve um token de acesso já sem a marca de conta inativa.</summary>
    [HttpPost("reativar")]
    public async Task<ActionResult<RespostaDeLogin>> Reactivate(CancellationToken ct)
    {
        var usuario = await CurrentUserAsync();
        if (usuario is null)
        {
            return Unauthorized();
        }

        foreach (var perfil in await ProfilesAsync(usuario.Id, ct))
        {
            perfil.Reativar();
        }

        await db.SaveChangesAsync(ct);
        var comDoisFatores = User.HasClaim(VolumoClaims.AuthenticationMethods, VolumoClaims.MultiFactor);
        return await sessions.AccessTokenAsync(usuario, comDoisFatores, ct);
    }

    /// <summary>
    /// O aluno encerra o acompanhamento com o personal atual (por exemplo, para aceitar o convite de
    /// outro). Os treinos e o histórico continuam com o aluno.
    /// </summary>
    [HttpPost("vinculo/encerrar")]
    [Authorize(Policy = Policies.Aluno)]
    public async Task<IActionResult> EndLink(CancellationToken ct)
    {
        var alunoId = User.UsuarioId()!.Value;
        var vinculo = await db.VinculosPersonalAluno.SingleOrDefaultAsync(v => v.AlunoId == alunoId && v.EncerradoEm == null, ct);
        if (vinculo is null)
        {
            return NotFound();
        }

        vinculo.Encerrar(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<Usuario?> CurrentUserAsync()
    {
        var usuario = User.UsuarioId() is { } id ? await userManager.FindByIdAsync(id.ToString()) : null;
        return usuario is { AnonimizadoEm: null, Suspenso: false } ? usuario : null;
    }

    private async Task<List<Perfil>> ProfilesAsync(Guid usuarioId, CancellationToken ct)
    {
        var perfis = new List<Perfil>();
        perfis.AddRange(await db.PerfisPersonal.IgnoreQueryFilters([FiltrosDeConsulta.Ativos]).Where(p => p.Id == usuarioId).ToListAsync(ct));
        perfis.AddRange(await db.PerfisAluno.IgnoreQueryFilters([FiltrosDeConsulta.Ativos]).Where(a => a.Id == usuarioId).ToListAsync(ct));
        return perfis;
    }
}
