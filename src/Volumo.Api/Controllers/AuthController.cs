using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Volumo.Api.Auth;
using Volumo.Api.Security;
using Volumo.Domain.Usuarios;
using Volumo.Infrastructure.Identity;
using Volumo.Infrastructure.Persistencia;
using Volumo.Shared.Autenticacao;

namespace Volumo.Api.Controllers;

[ApiController]
[Route("auth")]
[AllowAnonymous]
public sealed class AuthController(
    UserManager<Usuario> userManager,
    IPasswordHasher<Usuario> passwordHasher,
    VolumoDbContext db,
    AccessTokenIssuer accessTokens,
    RefreshTokenService refreshTokens,
    AccountEmails emails) : ControllerBase
{
    // Hash de uma senha qualquer: no login com e-mail inexistente a API verifica a senha contra ele,
    // para levar o mesmo tempo que um login real e não revelar quais e-mails têm conta.
    private static readonly string DummyPasswordHash =
        new PasswordHasher<Usuario>().HashPassword(new Usuario(), Guid.NewGuid().ToString());

    /// <summary>
    /// Cadastro de personal. Responde 202 tanto para e-mail novo quanto para e-mail já cadastrado
    /// (nesse caso o dono recebe um aviso), para a API não revelar quem tem conta.
    /// </summary>
    [HttpPost("cadastro/personal")]
    [EnableRateLimiting(RateLimitPolicies.Cadastro)]
    public async Task<IActionResult> RegisterPersonal(CadastroDePersonal request, CancellationToken ct)
    {
        var cref = string.IsNullOrWhiteSpace(request.Cref) ? null : Cref.Criar(request.Cref);
        var usuario = new Usuario { UserName = request.Email, Email = request.Email };

        // A senha é validada antes de olhar se o e-mail existe, para a resposta não depender disso.
        foreach (var validator in userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(userManager, usuario, request.Senha);
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(nameof(request.Senha), error.Description);
            }
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var existing = await userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            await emails.SendAlreadyRegisteredAsync(existing, ct);
            return Accepted();
        }

        if (cref is not null && await db.PerfisPersonal.IgnoreQueryFilters([FiltrosDeConsulta.Ativos]).AnyAsync(p => p.Cref == cref, ct))
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Este CREF já está cadastrado.");
        }

        // Conta, papel e perfil numa transação: nunca fica conta sem perfil.
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            var created = await userManager.CreateAsync(usuario, request.Senha);
            if (!created.Succeeded)
            {
                if (created.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
                {
                    return Accepted(); // cadastro simultâneo com o mesmo e-mail
                }

                return IdentityProblem(created);
            }

            var roleAdded = await userManager.AddToRoleAsync(usuario, Papeis.Personal);
            if (!roleAdded.Succeeded)
            {
                throw new InvalidOperationException("Não foi possível atribuir o papel Personal: " + string.Join("; ", roleAdded.Errors.Select(e => e.Code)));
            }

            db.PerfisPersonal.Add(PerfilPersonal.Criar(usuario.Id, request.Nome, cref));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        await emails.SendConfirmationAsync(usuario, ct);
        return Accepted();
    }

    [HttpPost("confirmar-email")]
    public async Task<IActionResult> ConfirmEmail(ConfirmacaoDeEmail request, CancellationToken ct)
    {
        var usuario = await userManager.FindByIdAsync(request.UsuarioId.ToString());
        var token = AccountEmails.DecodeToken(request.Token);
        if (usuario is null || token is null || usuario.AnonimizadoEm is not null)
        {
            return InvalidLink();
        }

        if (usuario.EmailConfirmed)
        {
            return NoContent();
        }

        var result = await userManager.ConfirmEmailAsync(usuario, token);
        return result.Succeeded ? NoContent() : InvalidLink();
    }

    /// <summary>Sempre 202: não revela se o e-mail existe nem se já foi confirmado.</summary>
    [HttpPost("reenviar-confirmacao")]
    [EnableRateLimiting(RateLimitPolicies.Cadastro)]
    public async Task<IActionResult> ResendConfirmation(PedidoDeReenvioDeConfirmacao request, CancellationToken ct)
    {
        var usuario = await userManager.FindByEmailAsync(request.Email);
        if (usuario is { EmailConfirmed: false, AnonimizadoEm: null })
        {
            await emails.SendConfirmationAsync(usuario, ct);
        }

        return Accepted();
    }

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<ActionResult<RespostaDeLogin>> Login(PedidoDeLogin request, CancellationToken ct)
    {
        var usuario = await userManager.FindByEmailAsync(request.Email);
        if (usuario is null)
        {
            passwordHasher.VerifyHashedPassword(new Usuario(), DummyPasswordHash, request.Senha);
            return InvalidCredentials();
        }

        if (await userManager.IsLockedOutAsync(usuario))
        {
            return InvalidCredentials();
        }

        if (!await userManager.CheckPasswordAsync(usuario, request.Senha))
        {
            await userManager.AccessFailedAsync(usuario);
            return InvalidCredentials();
        }

        await userManager.ResetAccessFailedCountAsync(usuario);

        // Só chega aqui quem acertou a senha, então dizer que falta confirmar não revela nada a estranhos.
        if (!usuario.EmailConfirmed)
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Confirme seu e-mail antes de entrar.",
                detail: "Enviamos um link de confirmação quando você se cadastrou. Você pode pedir um novo.");
        }

        return await StartSessionAsync(usuario, familiaId: null, ct);
    }

    /// <summary>Troca o refresh token do cookie por um novo e devolve um token de acesso novo.</summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<RespostaDeLogin>> Refresh(CancellationToken ct)
    {
        var token = RefreshTokenCookie.Read(Request);
        var rotated = token is null ? null : await refreshTokens.RotateAsync(token, ct);
        if (rotated is null)
        {
            return SessionExpired();
        }

        // Bloqueio temporário por senha errada não derruba sessões já abertas (senão um atacante
        // poderia deslogar alguém só errando a senha dele); conta excluída derruba.
        var usuario = await userManager.FindByIdAsync(rotated.UsuarioId.ToString());
        if (usuario is null || usuario.AnonimizadoEm is not null || !usuario.EmailConfirmed)
        {
            await refreshTokens.RevokeFamilyAsync(rotated.FamiliaId, ct);
            return SessionExpired();
        }

        RefreshTokenCookie.Write(Response, rotated.NewToken, rotated.ExpiresAt);
        var (accessToken, expiresAt) = accessTokens.Issue(usuario, await userManager.GetRolesAsync(usuario));
        return new RespostaDeLogin(accessToken, expiresAt);
    }

    /// <summary>Encerra a sessão deste dispositivo. Funciona mesmo com o token de acesso vencido.</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (RefreshTokenCookie.Read(Request) is { } token)
        {
            await refreshTokens.RevokeFamilyOfAsync(token, ct);
        }

        RefreshTokenCookie.Delete(Response);
        return NoContent();
    }

    private async Task<RespostaDeLogin> StartSessionAsync(Usuario usuario, Guid? familiaId, CancellationToken ct)
    {
        var (refreshToken, refreshExpiresAt) = await refreshTokens.IssueAsync(usuario.Id, familiaId, ct);
        RefreshTokenCookie.Write(Response, refreshToken, refreshExpiresAt);

        var (accessToken, expiresAt) = accessTokens.Issue(usuario, await userManager.GetRolesAsync(usuario));
        return new RespostaDeLogin(accessToken, expiresAt);
    }

    private ObjectResult InvalidCredentials() => Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "E-mail ou senha inválidos.",
        detail: "Depois de várias tentativas erradas, o acesso fica bloqueado por alguns minutos.");

    private ObjectResult SessionExpired()
    {
        RefreshTokenCookie.Delete(Response);
        return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sessão expirada. Entre novamente.");
    }

    private ObjectResult InvalidLink() =>
        Problem(statusCode: StatusCodes.Status400BadRequest, title: "Link inválido ou expirado.");

    private ActionResult IdentityProblem(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return ValidationProblem(ModelState);
    }
}
