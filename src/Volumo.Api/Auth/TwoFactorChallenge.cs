using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Volumo.Infrastructure.Identity;

namespace Volumo.Api.Auth;

/// <summary>
/// "Desafio" entregue depois da senha certa quando a conta tem 2FA: prova, por 5 minutos, que a
/// primeira etapa passou. É cifrado e assinado pelo Data Protection e amarrado ao SecurityStamp do
/// usuário, então troca de senha ou de configuração do 2FA invalida desafios pendentes.
/// </summary>
public sealed class TwoFactorChallenge(IDataProtectionProvider dataProtection, TimeProvider clock)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly ITimeLimitedDataProtector _protector =
        dataProtection.CreateProtector("Volumo.Auth.DesafioDoisFatores").ToTimeLimitedDataProtector();

    public string Create(Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        return _protector.Protect($"{usuario.Id:N}|{usuario.SecurityStamp}", clock.GetUtcNow() + Lifetime);
    }

    /// <summary>Devolve o Id e o SecurityStamp do desafio, ou <c>null</c> se for inválido ou vencido.</summary>
    public (Guid UsuarioId, string SecurityStamp)? Read(string? desafio)
    {
        if (string.IsNullOrEmpty(desafio))
        {
            return null;
        }

        try
        {
            var partes = _protector.Unprotect(desafio).Split('|', 2);
            return partes.Length == 2 && Guid.TryParseExact(partes[0], "N", out var id) ? (id, partes[1]) : null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
