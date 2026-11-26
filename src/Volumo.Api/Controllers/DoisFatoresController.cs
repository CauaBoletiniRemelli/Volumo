using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Volumo.Api.Auth;
using Volumo.Infrastructure.Identity;
using Volumo.Shared.Autenticacao;

namespace Volumo.Api.Controllers;

/// <summary>
/// Configuração do 2FA (aplicativo autenticador, TOTP). Fica sob /auth para receber o cookie do
/// refresh token: ao ativar, a sessão atual é trocada por uma sessão com segundo fator.
/// Exige login (política padrão).
/// </summary>
[ApiController]
[Route("auth/2fa")]
[Authorize]
public sealed class DoisFatoresController(
    UserManager<Usuario> userManager,
    SessionIssuer sessions,
    RefreshTokenService refreshTokens) : ControllerBase
{
    public const int RecoveryCodeCount = 10;
    private const string Issuer = "Volumo";

    /// <summary>Gera uma chave nova para o aplicativo autenticador. Não ativa nada ainda.</summary>
    [HttpPost("iniciar")]
    public async Task<ActionResult<ConfiguracaoDeDoisFatores>> Start()
    {
        var usuario = await CurrentUserAsync();
        if (usuario is null)
        {
            return Unauthorized();
        }

        if (usuario.TwoFactorEnabled)
        {
            return AlreadyEnabled();
        }

        await userManager.ResetAuthenticatorKeyAsync(usuario);
        var key = (await userManager.GetAuthenticatorKeyAsync(usuario))!;
        var uri = $"otpauth://totp/{Uri.EscapeDataString(Issuer)}:{Uri.EscapeDataString(usuario.Email!)}"
            + $"?secret={key}&issuer={Uri.EscapeDataString(Issuer)}&digits=6";

        return new ConfiguracaoDeDoisFatores(FormatKey(key), uri);
    }

    /// <summary>
    /// Confirma com um código do aplicativo e ativa o 2FA. Devolve os códigos de recuperação (só
    /// desta vez) e troca a sessão atual por uma sessão com segundo fator.
    /// </summary>
    [HttpPost("ativar")]
    public async Task<ActionResult<RespostaDeAtivacaoDeDoisFatores>> Enable(AtivacaoDeDoisFatores request, CancellationToken ct)
    {
        var usuario = await CurrentUserAsync();
        if (usuario is null)
        {
            return Unauthorized();
        }

        if (usuario.TwoFactorEnabled)
        {
            return AlreadyEnabled();
        }

        if (await userManager.GetAuthenticatorKeyAsync(usuario) is null)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Inicie a configuração do 2FA antes de ativar.");
        }

        var code = request.Codigo.Replace(" ", string.Empty, StringComparison.Ordinal);
        if (!await userManager.VerifyTwoFactorTokenAsync(usuario, userManager.Options.Tokens.AuthenticatorTokenProvider, code))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Código inválido. Confira o horário do celular e tente de novo.");
        }

        await userManager.SetTwoFactorEnabledAsync(usuario, true);
        var recoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(usuario, RecoveryCodeCount);

        if (RefreshTokenCookie.Read(Request) is { } currentSession)
        {
            await refreshTokens.RevokeFamilyOfAsync(currentSession, ct);
        }

        var session = await sessions.StartAsync(Response, usuario, comDoisFatores: true, ct);
        return new RespostaDeAtivacaoDeDoisFatores([.. recoveryCodes!], session);
    }

    /// <summary>Gera novos códigos de recuperação (os antigos deixam de valer). Exige sessão com 2FA.</summary>
    [HttpPost("codigos-de-recuperacao")]
    [Authorize(Policy = Policies.MultiFactor)]
    public async Task<ActionResult<CodigosDeRecuperacao>> RegenerateRecoveryCodes()
    {
        var usuario = await CurrentUserAsync();
        if (usuario is not { TwoFactorEnabled: true })
        {
            return Unauthorized();
        }

        var codes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(usuario, RecoveryCodeCount);
        return new CodigosDeRecuperacao([.. codes!]);
    }

    private async Task<Usuario?> CurrentUserAsync()
    {
        var usuario = User.UsuarioId() is { } id ? await userManager.FindByIdAsync(id.ToString()) : null;
        return usuario is { AnonimizadoEm: null } ? usuario : null;
    }

    private ObjectResult AlreadyEnabled() =>
        Problem(statusCode: StatusCodes.Status409Conflict, title: "O 2FA já está ativo nesta conta.");

    /// <summary>Chave em grupos de 4 letras, mais fácil de digitar no aplicativo.</summary>
    private static string FormatKey(string key)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < key.Length; i += 4)
        {
            builder.Append(key.AsSpan(i, Math.Min(4, key.Length - i))).Append(' ');
        }

        return builder.ToString().TrimEnd().ToLowerInvariant();
    }
}
