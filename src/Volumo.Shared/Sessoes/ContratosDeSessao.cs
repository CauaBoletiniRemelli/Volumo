using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Volumo.Shared.Sessoes;

// Sessões de treino registradas no app, inclusive offline. Os Ids (GUID v7) são gerados no
// aparelho: reenviar o mesmo lote não duplica nada.

public sealed record SerieRegistrada(
    [Required] Guid Id,
    [Required] Guid ExercicioId,
    [Range(0, 200)] int Repeticoes,
    [Range(0, 1000)] decimal CargaKg,
    [Range(0, 10)] int? Rir,
    [Required] DateTimeOffset RealizadaEm);

/// <summary>Cardio feito na sessão (fica fora do volume e da estagnação).</summary>
public sealed record CardioRegistrado(
    [Required] Guid Id,
    [Required] Guid ExercicioId,
    [Range(1, 300)] int DuracaoMinutos,
    [Required] DateTimeOffset RealizadoEm);

/// <param name="Cardios">Opcional: versões antigas do app não mandam.</param>
public sealed record SessaoRegistrada(
    [Required] Guid Id,
    Guid? TreinoId,
    [Required] DateTimeOffset IniciadaEm,
    DateTimeOffset? FinalizadaEm,
    [Required, MaxLength(300)] IReadOnlyList<SerieRegistrada> Series,
    [MaxLength(20)] IReadOnlyList<CardioRegistrado>? Cardios = null);

/// <summary>Até 20 sessões por envio; o app manda o que acumulou offline em lotes.</summary>
public sealed record LoteDeSessoes([Required, MinLength(1), MaxLength(20)] IReadOnlyList<SessaoRegistrada> Sessoes);

[JsonConverter(typeof(JsonStringEnumConverter<SituacaoDaSincronizacao>))]
public enum SituacaoDaSincronizacao
{
    /// <summary>Gravada (ou já estava gravada: reenvio).</summary>
    Sincronizada = 1,

    /// <summary>Recusada de vez (dado inválido ou de outra pessoa): o app deve parar de reenviar e avisar.</summary>
    Recusada = 2,
}

public sealed record ResultadoDaSessao(Guid Id, SituacaoDaSincronizacao Situacao, string? Motivo);

public sealed record ResultadoDaSincronizacao(IReadOnlyList<ResultadoDaSessao> Sessoes);

public sealed record SessaoResumo(
    Guid Id, Guid? TreinoId, string? NomeDoTreino, DateTimeOffset IniciadaEm, DateTimeOffset? FinalizadaEm,
    int QuantidadeDeSeries, decimal VolumeKg, int MinutosDeCardio = 0);

public sealed record SerieDaSessao(Guid Id, Guid ExercicioId, string NomeDoExercicio, int Repeticoes, decimal CargaKg, int? Rir, DateTimeOffset RealizadaEm);

public sealed record CardioDaSessao(Guid Id, Guid ExercicioId, string NomeDoExercicio, int DuracaoMinutos, DateTimeOffset RealizadoEm);

public sealed record SessaoDetalhe(
    Guid Id, Guid AlunoId, Guid? TreinoId, string? NomeDoTreino, DateTimeOffset IniciadaEm, DateTimeOffset? FinalizadaEm,
    IReadOnlyList<SerieDaSessao> Series, IReadOnlyList<CardioDaSessao>? Cardios = null);
