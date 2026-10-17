using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Volumo.Infrastructure.Identity;

namespace Volumo.Api.Auth;

/// <summary>
/// Token de redefinição de senha com validade própria (1 hora), mais curta que a do link de
/// confirmação de e-mail. Também deixa de valer assim que a senha muda (SecurityStamp).
/// </summary>
public sealed class PasswordResetTokenProvider(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<PasswordResetTokenProviderOptions> options,
    ILogger<DataProtectorTokenProvider<Usuario>> logger)
    : DataProtectorTokenProvider<Usuario>(dataProtectionProvider, options, logger)
{
    public const string ProviderName = "VolumoPasswordReset";
}

public sealed class PasswordResetTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public PasswordResetTokenProviderOptions()
    {
        Name = PasswordResetTokenProvider.ProviderName;
        TokenLifespan = TimeSpan.FromHours(1);
    }
}
