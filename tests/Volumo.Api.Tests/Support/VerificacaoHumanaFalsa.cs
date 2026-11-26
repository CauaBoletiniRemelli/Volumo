using Volumo.Api.Security;

namespace Volumo.Api.Tests.Support;

/// <summary>Substitui o Turnstile nos testes (sem rede): aprova qualquer token, menos <see cref="TokenReprovado"/>.</summary>
public sealed class VerificacaoHumanaFalsa : IHumanVerifier
{
    public const string TokenAprovado = "token-turnstile-aprovado";
    public const string TokenReprovado = "token-turnstile-reprovado";

    public Task<bool> VerifyAsync(string token, string? remoteIp, CancellationToken cancellationToken) =>
        Task.FromResult(token != TokenReprovado);
}
