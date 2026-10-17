using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Volumo.Infrastructure.Email;
using Volumo.Infrastructure.Identity;

namespace Volumo.Api.Auth;

/// <summary>E-mails do ciclo de vida da conta. Os links apontam para o front, que chama a API.</summary>
public sealed class AccountEmails(UserManager<Usuario> userManager, IEnvioDeEmail email, FrontSettings front)
{
    public const string ConfirmEmailPath = "/confirmar-email";
    public const string ResetPasswordPath = "/redefinir-senha";

    public async Task SendConfirmationAsync(Usuario usuario, CancellationToken ct)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(usuario);
        var link = front.Link(ConfirmEmailPath, new Dictionary<string, string>
        {
            ["usuario"] = usuario.Id.ToString(),
            ["token"] = EncodeToken(token),
        });

        await email.EnviarAsync(
            new MensagemDeEmail(
                usuario.Email!,
                "Confirme seu e-mail no Volumo",
                $"Olá! Para ativar sua conta no Volumo, abra o link abaixo:\n\n{link}\n\nSe você não criou uma conta, ignore este e-mail."),
            ct);
    }

    /// <summary>
    /// Enviado quando alguém tenta se cadastrar com um e-mail que já tem conta. A API responde igual
    /// nos dois casos, para não revelar quais e-mails estão cadastrados; só o dono do e-mail fica sabendo.
    /// </summary>
    public Task SendAlreadyRegisteredAsync(Usuario usuario, CancellationToken ct) =>
        email.EnviarAsync(
            new MensagemDeEmail(
                usuario.Email!,
                "Tentativa de cadastro no Volumo",
                "Alguém tentou criar uma conta no Volumo com este e-mail, mas você já tem uma conta.\n\n"
                + "Se foi você, é só entrar com sua senha. Se não foi, pode ignorar este e-mail."),
            ct);

    public async Task SendPasswordResetAsync(Usuario usuario, CancellationToken ct)
    {
        var token = await userManager.GeneratePasswordResetTokenAsync(usuario);
        var link = front.Link(ResetPasswordPath, new Dictionary<string, string>
        {
            ["usuario"] = usuario.Id.ToString(),
            ["token"] = EncodeToken(token),
        });

        await email.EnviarAsync(
            new MensagemDeEmail(
                usuario.Email!,
                "Redefinir sua senha do Volumo",
                $"Recebemos um pedido para redefinir sua senha. O link abaixo vale por 1 hora:\n\n{link}\n\n"
                + "Se não foi você, ignore este e-mail: sua senha continua a mesma."),
            ct);
    }

    /// <summary>Aviso de segurança: se não foi a pessoa, ela fica sabendo na hora.</summary>
    public Task SendPasswordChangedAsync(Usuario usuario, CancellationToken ct) =>
        email.EnviarAsync(
            new MensagemDeEmail(
                usuario.Email!,
                "Sua senha do Volumo foi alterada",
                "A senha da sua conta acabou de ser redefinida e todas as sessões abertas foram encerradas.\n\n"
                + "Se não foi você, redefina a senha de novo imediatamente e fale com o suporte."),
            ct);

    /// <summary>Tokens do Identity têm caracteres que quebram URLs; vão em Base64Url.</summary>
    public static string EncodeToken(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    public static string? DecodeToken(string encoded)
    {
        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encoded));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
