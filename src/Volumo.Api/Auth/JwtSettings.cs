using Microsoft.IdentityModel.Tokens;

namespace Volumo.Api.Auth;

/// <summary>
/// Configuração do token de acesso. A chave de assinatura é segredo: User Secrets no desenvolvimento
/// e variável de ambiente <c>Jwt__SigningKey</c> em produção; nunca no appsettings versionado.
/// </summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";
    public const int MinimumKeyBytes = 32;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    /// <summary>Chave HMAC-SHA256 em Base64, com pelo menos 32 bytes.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;

    public SymmetricSecurityKey Key { get; private set; } = null!;

    /// <summary>Lê e valida a configuração. Configuração inválida impede a API de subir.</summary>
    public static JwtSettings Load(IConfiguration configuration)
    {
        var settings = configuration.GetSection(SectionName).Get<JwtSettings>() ?? new JwtSettings();

        if (string.IsNullOrWhiteSpace(settings.Issuer) || string.IsNullOrWhiteSpace(settings.Audience))
        {
            throw new InvalidOperationException("Jwt:Issuer e Jwt:Audience são obrigatórios.");
        }

        if (settings.AccessTokenMinutes is < 1 or > 60)
        {
            throw new InvalidOperationException("Jwt:AccessTokenMinutes precisa estar entre 1 e 60.");
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(settings.SigningKey);
        }
        catch (FormatException)
        {
            key = [];
        }

        if (key.Length < MinimumKeyBytes)
        {
            throw new InvalidOperationException(
                $"Jwt:SigningKey precisa ser Base64 com pelo menos {MinimumKeyBytes} bytes. Em produção, use a variável de ambiente Jwt__SigningKey.");
        }

        settings.Key = new SymmetricSecurityKey(key);
        return settings;
    }
}
