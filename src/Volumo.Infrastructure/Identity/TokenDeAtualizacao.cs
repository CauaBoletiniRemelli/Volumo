using System.Buffers.Text;
using System.Security.Cryptography;

namespace Volumo.Infrastructure.Identity;

/// <summary>
/// Refresh token rotativo. Cada login abre uma "família"; cada renovação marca o token atual como
/// usado e emite o próximo da mesma família. Se um token já usado aparecer de novo (roubo), a
/// família inteira é revogada. Só o hash SHA-256 fica no banco.
/// </summary>
public sealed class TokenDeAtualizacao
{
    public const int TamanhoTokenBytes = 32;
    public const int TamanhoHashBytes = 32;

    private TokenDeAtualizacao()
    {
    }

    private TokenDeAtualizacao(Guid usuarioId, Guid familiaId, byte[] tokenHash, DateTimeOffset criadoEm, DateTimeOffset expiraEm)
    {
        Id = Guid.CreateVersion7();
        UsuarioId = usuarioId;
        FamiliaId = familiaId;
        TokenHash = tokenHash;
        CriadoEm = criadoEm;
        ExpiraEm = expiraEm;
    }

    public Guid Id { get; private set; }

    public Guid UsuarioId { get; private set; }

    public Guid FamiliaId { get; private set; }

    public byte[] TokenHash { get; private set; } = null!;

    public DateTimeOffset CriadoEm { get; private set; }

    public DateTimeOffset ExpiraEm { get; private set; }

    public DateTimeOffset? UsadoEm { get; private set; }

    public DateTimeOffset? RevogadoEm { get; private set; }

    /// <summary>Cria o registro e devolve o token em claro, que vai só para o cookie.</summary>
    public static (TokenDeAtualizacao Registro, string Token) Criar(
        Guid usuarioId, Guid familiaId, DateTimeOffset agora, TimeSpan validade)
    {
        var bytes = RandomNumberGenerator.GetBytes(TamanhoTokenBytes);
        var registro = new TokenDeAtualizacao(usuarioId, familiaId, SHA256.HashData(bytes), agora, agora + validade);
        return (registro, Base64Url.EncodeToString(bytes));
    }

    public static byte[]? CalcularHash(string? token)
    {
        if (string.IsNullOrEmpty(token) || Base64Url.GetMaxDecodedLength(token.Length) < TamanhoTokenBytes)
        {
            return null;
        }

        Span<byte> bytes = stackalloc byte[TamanhoTokenBytes];
        return Base64Url.TryDecodeFromChars(token, bytes, out var escritos) && escritos == TamanhoTokenBytes
            ? SHA256.HashData(bytes)
            : null;
    }

    public void Revogar(DateTimeOffset agora) => RevogadoEm ??= agora;
}
