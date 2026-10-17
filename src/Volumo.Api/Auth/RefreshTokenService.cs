using Microsoft.EntityFrameworkCore;
using Volumo.Infrastructure.Identity;
using Volumo.Infrastructure.Persistencia;

namespace Volumo.Api.Auth;

public sealed record RefreshResult(Guid UsuarioId, Guid FamiliaId, string NewToken, DateTimeOffset ExpiresAt);

/// <summary>
/// Refresh tokens rotativos com detecção de reuso: cada token vale uma vez; apresentar um token já
/// usado ou revogado revoga a família inteira (alguém copiou o cookie). Validade de 30 dias, renovada
/// a cada uso, para o aluno não precisar entrar de novo a cada treino.
/// </summary>
public sealed partial class RefreshTokenService(VolumoDbContext db, TimeProvider clock, ILogger<RefreshTokenService> logger)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    /// <summary>Emite um token novo; sem <paramref name="familiaId"/>, abre uma família (novo login).</summary>
    public async Task<(string Token, DateTimeOffset ExpiresAt)> IssueAsync(Guid usuarioId, Guid? familiaId, CancellationToken ct)
    {
        var (registro, token) = TokenDeAtualizacao.Criar(usuarioId, familiaId ?? Guid.CreateVersion7(), clock.GetUtcNow(), Lifetime);
        db.TokensDeAtualizacao.Add(registro);
        await db.SaveChangesAsync(ct);
        return (token, registro.ExpiraEm);
    }

    /// <summary>Troca o token por um novo da mesma família. Devolve <c>null</c> se o token não serve.</summary>
    public async Task<RefreshResult?> RotateAsync(string token, CancellationToken ct)
    {
        var hash = TokenDeAtualizacao.CalcularHash(token);
        if (hash is null)
        {
            return null;
        }

        var atual = await db.TokensDeAtualizacao.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (atual is null)
        {
            return null;
        }

        var now = clock.GetUtcNow();
        if (atual.UsadoEm is not null || atual.RevogadoEm is not null)
        {
            LogReuse(logger, atual.FamiliaId);
            await RevokeFamilyAsync(atual.FamiliaId, ct);
            return null;
        }

        if (now >= atual.ExpiraEm)
        {
            return null;
        }

        // Marca como usado só se ninguém marcou antes: de duas requisições simultâneas, uma vence
        // e a outra é tratada como reuso.
        var marked = await db.TokensDeAtualizacao
            .Where(t => t.Id == atual.Id && t.UsadoEm == null && t.RevogadoEm == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsadoEm, now), ct);
        if (marked == 0)
        {
            LogReuse(logger, atual.FamiliaId);
            await RevokeFamilyAsync(atual.FamiliaId, ct);
            return null;
        }

        var (novo, expiresAt) = await IssueAsync(atual.UsuarioId, atual.FamiliaId, ct);
        return new RefreshResult(atual.UsuarioId, atual.FamiliaId, novo, expiresAt);
    }

    /// <summary>Logout: revoga a família do token apresentado (encerra só esta sessão/dispositivo).</summary>
    public async Task RevokeFamilyOfAsync(string token, CancellationToken ct)
    {
        var hash = TokenDeAtualizacao.CalcularHash(token);
        if (hash is null)
        {
            return;
        }

        var familiaId = await db.TokensDeAtualizacao.Where(t => t.TokenHash == hash).Select(t => (Guid?)t.FamiliaId).SingleOrDefaultAsync(ct);
        if (familiaId is { } familia)
        {
            await RevokeFamilyAsync(familia, ct);
        }
    }

    public Task RevokeFamilyAsync(Guid familiaId, CancellationToken ct) =>
        db.TokensDeAtualizacao
            .Where(t => t.FamiliaId == familiaId && t.RevogadoEm == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevogadoEm, clock.GetUtcNow()), ct);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh token reutilizado; família {FamiliaId} revogada.")]
    private static partial void LogReuse(ILogger logger, Guid familiaId);
}
