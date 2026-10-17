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
    SessionIssuer sessions,
    RefreshTokenService refreshTokens,
    TwoFactorChallenge challenges,
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
    [RequireHumanVerification]
    public async Task<IActionResult> RegisterPersonal(CadastroDePersonal request, CancellationToken ct)
    {
        var cref = string.IsNullOrWhiteSpace(request.Cref) ? null : Cref.Criar(request.Cref);
        var usuario = new Usuario { UserName = request.Email, Email = request.Email };

        // A senha é validada antes de olhar se o e-mail existe, para a resposta não depender disso.
        await AddPasswordErrorsAsync(usuario, request.Senha, nameof(request.Senha));
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
    public async Task<IActionResult> ConfirmEmail(ConfirmacaoDeEmail request)
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
    [RequireHumanVerification]
    public async Task<IActionResult> ResendConfirmation(PedidoDeReenvioDeConfirmacao request, CancellationToken ct)
    {
        var usuario = await userManager.FindByEmailAsync(request.Email);
        if (usuario is { EmailConfirmed: false, AnonimizadoEm: null })
        {
            await emails.SendConfirmationAsync(usuario, ct);
        }

        return Accepted();
    }

    /// <summary>
    /// Primeira etapa: e-mail e senha. Sem 2FA, já abre a sessão. Com 2FA, devolve só um desafio
    /// de 5 minutos para a segunda etapa.
    /// </summary>
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

        // Com 2FA, a contagem de erros só zera depois do código certo: senha certa + código chutado
        // continua contando para o bloqueio.
        if (usuario.TwoFactorEnabled)
        {
            if (EmailNotConfirmed(usuario) is { } naoConfirmado)
            {
                return naoConfirmado;
            }

            return new RespostaDeLogin(null, null, challenges.Create(usuario));
        }

        await userManager.ResetAccessFailedCountAsync(usuario);
        if (EmailNotConfirmed(usuario) is { } pendente)
        {
            return pendente;
        }

        return await sessions.StartAsync(Response, usuario, comDoisFatores: false, ct);
    }

    /// <summary>Segunda etapa: código de 6 dígitos do aplicativo autenticador.</summary>
    [HttpPost("login/2fa")]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<ActionResult<RespostaDeLogin>> LoginTwoFactor(SegundaEtapaDoLogin request, CancellationToken ct)
    {
        var usuario = await ResolveChallengeAsync(request.Desafio);
        if (usuario is null)
        {
            return InvalidSecondFactor();
        }

        var code = NormalizeCode(request.Codigo);
        if (!await userManager.VerifyTwoFactorTokenAsync(usuario, userManager.Options.Tokens.AuthenticatorTokenProvider, code))
        {
            await userManager.AccessFailedAsync(usuario);
            return InvalidSecondFactor();
        }

        await userManager.ResetAccessFailedCountAsync(usuario);
        return await sessions.StartAsync(Response, usuario, comDoisFatores: true, ct);
    }

    /// <summary>Alternativa à segunda etapa quando a pessoa perdeu o celular. Cada código vale uma vez.</summary>
    [HttpPost("login/recuperacao")]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<ActionResult<RespostaDeLogin>> LoginWithRecoveryCode(LoginComCodigoDeRecuperacao request, CancellationToken ct)
    {
        var usuario = await ResolveChallengeAsync(request.Desafio);
        if (usuario is null)
        {
            return InvalidSecondFactor();
        }

        var redeemed = await userManager.RedeemTwoFactorRecoveryCodeAsync(usuario, NormalizeRecoveryCode(request.CodigoDeRecuperacao));
        if (!redeemed.Succeeded)
        {
            await userManager.AccessFailedAsync(usuario);
            return InvalidSecondFactor();
        }

        await userManager.ResetAccessFailedCountAsync(usuario);
        return await sessions.StartAsync(Response, usuario, comDoisFatores: true, ct);
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
        return await sessions.AccessTokenAsync(usuario, rotated.ComDoisFatores);
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

    /// <summary>Sempre 202: não revela se o e-mail tem conta.</summary>
    [HttpPost("esqueci-senha")]
    [EnableRateLimiting(RateLimitPolicies.Cadastro)]
    [RequireHumanVerification]
    public async Task<IActionResult> ForgotPassword(PedidoDeRecuperacaoDeSenha request, CancellationToken ct)
    {
        var usuario = await userManager.FindByEmailAsync(request.Email);
        if (usuario is { AnonimizadoEm: null })
        {
            await emails.SendPasswordResetAsync(usuario, ct);
        }

        return Accepted();
    }

    /// <summary>
    /// Nova senha pelo link do e-mail. Encerra todas as sessões, desbloqueia a conta e avisa por e-mail.
    /// O 2FA continua valendo no próximo login.
    /// </summary>
    [HttpPost("redefinir-senha")]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<IActionResult> ResetPassword(RedefinicaoDeSenha request, CancellationToken ct)
    {
        var usuario = await userManager.FindByIdAsync(request.UsuarioId.ToString());
        var token = AccountEmails.DecodeToken(request.Token);
        if (usuario is null || token is null || usuario.AnonimizadoEm is not null)
        {
            return InvalidLink();
        }

        var result = await userManager.ResetPasswordAsync(usuario, token, request.NovaSenha);
        if (!result.Succeeded)
        {
            return result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken))
                ? InvalidLink()
                : IdentityProblem(result, nameof(request.NovaSenha));
        }

        // Quem abriu o link do e-mail provou que é dono dele.
        if (!usuario.EmailConfirmed)
        {
            usuario.EmailConfirmed = true;
            await userManager.UpdateAsync(usuario);
        }

        await userManager.SetLockoutEndDateAsync(usuario, null);
        await userManager.ResetAccessFailedCountAsync(usuario);
        await refreshTokens.RevokeAllForUserAsync(usuario.Id, ct);
        await emails.SendPasswordChangedAsync(usuario, ct);
        return NoContent();
    }

    /// <summary>Usuário do desafio, se o desafio é válido e nada mudou na conta desde a primeira etapa.</summary>
    private async Task<Usuario?> ResolveChallengeAsync(string desafio)
    {
        if (challenges.Read(desafio) is not { } dados)
        {
            return null;
        }

        var usuario = await userManager.FindByIdAsync(dados.UsuarioId.ToString());
        var valid = usuario is { TwoFactorEnabled: true, EmailConfirmed: true, AnonimizadoEm: null }
            && usuario.SecurityStamp == dados.SecurityStamp
            && !await userManager.IsLockedOutAsync(usuario);
        return valid ? usuario : null;
    }

    private async Task AddPasswordErrorsAsync(Usuario usuario, string password, string field)
    {
        foreach (var validator in userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(userManager, usuario, password);
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(field, error.Description);
            }
        }
    }

    private static string NormalizeCode(string code) =>
        code.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);

    /// <summary>
    /// O Identity guarda os códigos de recuperação como "XXXXX-XXXXX" e compara o texto exato; aceita
    /// também a forma digitada sem hífen, com espaços ou em minúsculas.
    /// </summary>
    private static string NormalizeRecoveryCode(string code)
    {
        var compact = NormalizeCode(code).ToUpperInvariant();
        return compact.Length == 10 ? $"{compact[..5]}-{compact[5..]}" : compact;
    }

    /// <summary>Só é chamado depois da senha certa, então dizer que falta confirmar não revela nada a estranhos.</summary>
    private ObjectResult? EmailNotConfirmed(Usuario usuario) => usuario.EmailConfirmed
        ? null
        : Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Confirme seu e-mail antes de entrar.",
            detail: "Enviamos um link de confirmação quando você se cadastrou. Você pode pedir um novo.");

    private ObjectResult InvalidCredentials() => Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "E-mail ou senha inválidos.",
        detail: "Depois de várias tentativas erradas, o acesso fica bloqueado por alguns minutos.");

    private ObjectResult InvalidSecondFactor() => Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Código inválido ou expirado.",
        detail: "Confira o código no aplicativo autenticador ou entre de novo com e-mail e senha.");

    private ObjectResult SessionExpired()
    {
        RefreshTokenCookie.Delete(Response);
        return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sessão expirada. Entre novamente.");
    }

    private ObjectResult InvalidLink() =>
        Problem(statusCode: StatusCodes.Status400BadRequest, title: "Link inválido ou expirado.");

    private ActionResult IdentityProblem(IdentityResult result, string field = "")
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(field, error.Description);
        }

        return ValidationProblem(ModelState);
    }
}
