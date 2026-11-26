using Microsoft.AspNetCore.Identity;

namespace Volumo.Infrastructure.Identity;

/// <summary>
/// Conta de acesso (ASP.NET Core Identity). Fica na Infrastructure para o Domain não depender do Identity;
/// os perfis do domínio usam o mesmo Id. O Id é GUID v7, como o resto do sistema.
/// </summary>
public sealed class Usuario : IdentityUser<Guid>
{
    public Usuario() => Id = Guid.CreateVersion7();

    public const int MotivoDaSuspensaoMaximo = 500;

    public DateTimeOffset? AnonimizadoEm { get; private set; }

    /// <summary>
    /// Suspensão pela plataforma (abuso, fraude, CREF falso). Diferente da inativação: a própria
    /// pessoa não consegue desfazer; só um Admin.
    /// </summary>
    public DateTimeOffset? SuspensoEm { get; private set; }

    public string? MotivoDaSuspensao { get; private set; }

    public bool Suspenso => SuspensoEm is not null;

    public void Suspender(string motivo, DateTimeOffset agora)
    {
        var limpo = motivo?.Trim();
        if (string.IsNullOrEmpty(limpo) || limpo.Length > MotivoDaSuspensaoMaximo)
        {
            throw new ArgumentException($"O motivo é obrigatório e tem no máximo {MotivoDaSuspensaoMaximo} caracteres.", nameof(motivo));
        }

        SuspensoEm = agora;
        MotivoDaSuspensao = limpo;
    }

    public void RemoverSuspensao()
    {
        SuspensoEm = null;
        MotivoDaSuspensao = null;
    }

    /// <summary>
    /// Exclusão de conta (LGPD): apaga e-mail, telefone e senha, invalida sessões e bloqueia o login
    /// para sempre. A linha continua no banco porque os perfis e o histórico apontam para ela.
    /// </summary>
    public void Anonimizar(DateTimeOffset agora)
    {
        var nomeAnonimo = $"removido-{Id:N}";
        UserName = nomeAnonimo;
        NormalizedUserName = nomeAnonimo.ToUpperInvariant();
        Email = null;
        NormalizedEmail = null;
        EmailConfirmed = false;
        PhoneNumber = null;
        PhoneNumberConfirmed = false;
        PasswordHash = null;
        TwoFactorEnabled = false;
        SecurityStamp = Guid.NewGuid().ToString();
        ConcurrencyStamp = Guid.NewGuid().ToString();
        LockoutEnabled = true;
        LockoutEnd = DateTimeOffset.MaxValue;
        MotivoDaSuspensao = null; // texto livre pode citar a pessoa
        AnonimizadoEm ??= agora;
    }
}
