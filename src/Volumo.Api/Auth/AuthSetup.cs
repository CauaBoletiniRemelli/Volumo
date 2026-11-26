using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Volumo.Api.Security;
using Volumo.Domain.Usuarios;
using Volumo.Infrastructure.Email;
using Volumo.Infrastructure.Identity;
using Volumo.Infrastructure.Persistencia;

namespace Volumo.Api.Auth;

public static class AuthSetup
{
    public const string EmailProviderKey = "Email:Provider";
    public const string EmailFolderKey = "Email:Folder";

    /// <summary>
    /// Identity (contas, senha, bloqueio, tokens de e-mail), JWT para as requisições e a regra
    /// "toda rota exige login, exceto as marcadas com [AllowAnonymous]".
    /// </summary>
    public static IServiceCollection AddVolumoAuth(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var jwt = JwtSettings.Load(configuration);
        services.AddSingleton(jwt);
        services.AddSingleton(FrontSettings.Load(configuration));

        // Chaves que protegem os tokens de confirmação de e-mail ficam no banco: sobrevivem a
        // reinícios e funcionam com mais de uma instância.
        services.AddDataProtection().SetApplicationName("Volumo").PersistKeysToDbContext<VolumoDbContext>();

        services.AddIdentityCore<Usuario>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = string.Empty; // o nome de usuário é o e-mail, já validado

                // Comprimento em vez de regras de composição (recomendação do NIST SP 800-63B).
                options.Password.RequiredLength = 10;
                options.Password.RequiredUniqueChars = 4;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                options.SignIn.RequireConfirmedEmail = true;
                options.Tokens.PasswordResetTokenProvider = PasswordResetTokenProvider.ProviderName;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<VolumoDbContext>()
            .AddErrorDescriber<PortugueseIdentityErrorDescriber>()
            .AddDefaultTokenProviders()
            .AddTokenProvider<PasswordResetTokenProvider>(PasswordResetTokenProvider.ProviderName);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = jwt.Key,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.FromSeconds(30),
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    NameClaimType = VolumoClaims.Subject,
                    RoleClaimType = VolumoClaims.Role,
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.Personal, policy => policy
                .RequireRole(Papeis.Personal)
                .RequireClaim(VolumoClaims.AuthenticationMethods, VolumoClaims.MultiFactor)
                .RequireAssertion(ActiveAccount))
            .AddPolicy(Policies.Aluno, policy => policy
                .RequireRole(Papeis.Aluno)
                .RequireAssertion(ActiveAccount))
            .AddPolicy(Policies.PersonalOuAluno, policy => policy
                .RequireRole(Papeis.Personal, Papeis.Aluno)
                .RequireAssertion(ActiveAccount))
            .AddPolicy(Policies.ContaAtiva, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(ActiveAccount))
            .AddPolicy(Policies.Admin, policy => policy
                .RequireRole(Papeis.Admin)
                .RequireClaim(VolumoClaims.AuthenticationMethods, VolumoClaims.MultiFactor)
                .RequireAssertion(ActiveAccount))
            .AddPolicy(Policies.MultiFactor, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(VolumoClaims.AuthenticationMethods, VolumoClaims.MultiFactor));

        services.AddHttpContextAccessor();
        services.AddScoped<IContextoDeAcesso, HttpAccessContext>();

        services.AddSingleton<AccessTokenIssuer>();
        services.AddSingleton<TwoFactorChallenge>();
        services.AddSingleton<TrustedDeviceCookie>();
        services.AddScoped<RefreshTokenService>();
        services.AddScoped<SessionIssuer>();
        services.AddScoped<AccountEmails>();

        services.AddSingleton(TurnstileSettings.Load(configuration));
        services.AddHttpClient<IHumanVerifier, TurnstileVerifier>(client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddVolumoEmail(configuration, environment);
        return services;
    }

    private static bool ActiveAccount(AuthorizationHandlerContext context) =>
        !context.User.HasClaim(c => c.Type == VolumoClaims.InactiveAccount);

    /// <summary>Chave da API e remetente vêm de User Secrets/variáveis de ambiente; faltando algo, a API não sobe.</summary>
    public static ConfiguracaoBrevo LoadBrevo(IConfiguration configuration)
    {
        var apiKey = configuration["Email:Brevo:ApiKey"];
        var remetente = configuration["Email:Brevo:SenderEmail"];
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(remetente))
        {
            throw new InvalidOperationException(
                "Email:Provider=Brevo exige Email:Brevo:ApiKey e Email:Brevo:SenderEmail. Em produção, use as variáveis Email__Brevo__ApiKey e Email__Brevo__SenderEmail.");
        }

        var nome = configuration["Email:Brevo:SenderName"] is { Length: > 0 } configurado ? configurado : "Volumo";
        return new ConfiguracaoBrevo(apiKey, remetente, nome);
    }

    private static void AddVolumoEmail(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var provider = configuration[EmailProviderKey];
        switch (provider)
        {
            case "File" when environment.IsProduction():
                throw new InvalidOperationException("Email:Provider=File grava links com tokens em disco e não pode ser usado em produção.");
            case "File":
                var folder = configuration[EmailFolderKey] is { Length: > 0 } configured
                    ? configured
                    : Path.Combine(Path.GetTempPath(), "volumo-emails");
                services.AddSingleton<IEnvioDeEmail>(new EmailEmArquivo(folder));
                break;
            case "Brevo":
                services.AddSingleton(LoadBrevo(configuration));
                services.AddHttpClient<IEnvioDeEmail, EmailBrevo>(client => client.Timeout = TimeSpan.FromSeconds(15));
                break;
            default:
                throw new InvalidOperationException($"{EmailProviderKey} não configurado. Valores aceitos: Brevo, File (só desenvolvimento).");
        }
    }
}
