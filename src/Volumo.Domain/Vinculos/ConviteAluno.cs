using System.Buffers.Text;
using System.Security.Cryptography;
using Volumo.Domain.Comum;

namespace Volumo.Domain.Vinculos;

/// <summary>
/// Convite do personal para um aluno entrar na plataforma. O token vai por e-mail e só o
/// hash SHA-256 fica no banco: quem ler a tabela não consegue usar os convites.
/// </summary>
public sealed class ConviteAluno : Entidade
{
    public const int TamanhoTokenBytes = 32;
    public const int TamanhoHashBytes = 32;
    public const string EmailAnonimizado = "removido@anonimizado.invalid";
    public static readonly TimeSpan Validade = TimeSpan.FromDays(7);

    private ConviteAluno()
    {
    }

    private ConviteAluno(Guid id, Guid personalId, string email, byte[] tokenHash, DateTimeOffset agora)
        : base(id)
    {
        PersonalId = Guarda.Referencia(personalId, "Personal");
        Email = Guarda.Email(email);
        TokenHash = tokenHash;
        CriadoEm = agora;
        ExpiraEm = agora + Validade;
    }

    public Guid PersonalId { get; private set; }

    public string Email { get; private set; } = null!;

    public byte[] TokenHash { get; private set; } = null!;

    public DateTimeOffset CriadoEm { get; private set; }

    public DateTimeOffset ExpiraEm { get; private set; }

    public DateTimeOffset? AceitoEm { get; private set; }

    public Guid? AlunoId { get; private set; }

    public DateTimeOffset? RevogadoEm { get; private set; }

    /// <summary>Cria o convite e devolve o token em claro, que deve ir só no link do e-mail.</summary>
    public static (ConviteAluno Convite, string Token) Criar(Guid id, Guid personalId, string email, DateTimeOffset agora)
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(TamanhoTokenBytes);
        var convite = new ConviteAluno(id, personalId, email, SHA256.HashData(tokenBytes), agora);
        return (convite, Base64Url.EncodeToString(tokenBytes));
    }

    /// <summary>Hash usado para localizar o convite no banco a partir do token recebido.</summary>
    public static byte[]? CalcularHash(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        Span<byte> tokenBytes = stackalloc byte[TamanhoTokenBytes];
        if (Base64Url.GetMaxDecodedLength(token.Length) < TamanhoTokenBytes
            || !Base64Url.TryDecodeFromChars(token, tokenBytes, out var escritos)
            || escritos != TamanhoTokenBytes)
        {
            return null;
        }

        return SHA256.HashData(tokenBytes);
    }

    public bool PodeSerAceito(DateTimeOffset agora) => SituacaoEm(agora) == SituacaoDoConvite.Pendente;

    public SituacaoDoConvite SituacaoEm(DateTimeOffset agora) =>
        AceitoEm is not null ? SituacaoDoConvite.Aceito
        : RevogadoEm is not null ? SituacaoDoConvite.Revogado
        : agora >= ExpiraEm ? SituacaoDoConvite.Expirado
        : SituacaoDoConvite.Pendente;

    /// <summary>Aceita o convite e devolve o vínculo do aluno com o personal.</summary>
    public VinculoPersonalAluno Aceitar(string token, Guid alunoId, DateTimeOffset agora)
    {
        var hash = CalcularHash(token);
        if (hash is null || !CryptographicOperations.FixedTimeEquals(hash, TokenHash))
        {
            throw new RegraDeNegocioException("Convite inválido.");
        }

        if (!PodeSerAceito(agora))
        {
            throw new RegraDeNegocioException("Este convite expirou ou já foi usado.");
        }

        AlunoId = Guarda.Referencia(alunoId, "Aluno");
        AceitoEm = agora;
        return VinculoPersonalAluno.Criar(Guid.CreateVersion7(), PersonalId, alunoId, agora);
    }

    public void Revogar(DateTimeOffset agora)
    {
        if (AceitoEm is not null)
        {
            throw new RegraDeNegocioException("Um convite já aceito não pode ser revogado.");
        }

        RevogadoEm ??= agora;
    }

    /// <summary>
    /// Exclusão de conta (LGPD) do personal ou do aluno: revoga o convite se ainda estiver
    /// pendente e apaga o e-mail, que é dado pessoal de quem foi convidado.
    /// </summary>
    public void Anonimizar(DateTimeOffset agora)
    {
        if (AceitoEm is null)
        {
            RevogadoEm ??= agora;
        }

        Email = EmailAnonimizado;
    }
}
