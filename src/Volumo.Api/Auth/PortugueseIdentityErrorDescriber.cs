using System.Globalization;
using Microsoft.AspNetCore.Identity;

namespace Volumo.Api.Auth;

/// <summary>Mensagens do Identity em português (as que podem chegar ao usuário).</summary>
public sealed class PortugueseIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError PasswordTooShort(int length) =>
        Error(nameof(PasswordTooShort), string.Format(CultureInfo.InvariantCulture, "A senha precisa ter pelo menos {0} caracteres.", length));

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Error(nameof(PasswordRequiresUniqueChars), string.Format(CultureInfo.InvariantCulture, "A senha precisa ter pelo menos {0} caracteres diferentes.", uniqueChars));

    public override IdentityError InvalidEmail(string? email) => Error(nameof(InvalidEmail), "E-mail inválido.");

    public override IdentityError InvalidToken() => Error(nameof(InvalidToken), "Link inválido ou expirado.");

    public override IdentityError DuplicateEmail(string email) => Error(nameof(DuplicateEmail), "E-mail já cadastrado.");

    public override IdentityError DuplicateUserName(string userName) => Error(nameof(DuplicateUserName), "E-mail já cadastrado.");

    public override IdentityError DefaultError() => Error(nameof(DefaultError), "Não foi possível concluir a operação.");

    private static IdentityError Error(string code, string description) => new() { Code = code, Description = description };
}
