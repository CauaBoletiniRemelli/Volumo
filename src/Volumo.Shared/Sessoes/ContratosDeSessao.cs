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

/// <summary>
/// Observação do aluno num exercício do treino, escrita durante a sessão (texto vazio limpa).
/// Vale a mais nova por <see cref="AtualizadaEm"/>.
/// </summary>
public sealed record AnotacaoRegistrada(
    [Required] Guid ExercicioId,
    [StringLength(500)] string? Texto,
    [Required] DateTimeOffset AtualizadaEm);

/// <param name="Cardios">Opcional: versões antigas do app não mandam.</param>
/// <param name="Anotacoes">Opcional: observações do aluno feitas na sessão (só valem em sessão de um treino).</param>
public sealed record SessaoRegistrada(
    [Required] Guid Id,
    Guid? TreinoId,
    [Required] DateTimeOffset IniciadaEm,
    DateTimeOffset? FinalizadaEm,
    [Required, MaxLength(300)] IReadOnlyList<SerieRegistrada> Series,
    [MaxLength(20)] IReadOnlyList<CardioRegistrado>? Cardios = null,
    [MaxLength(50)] IReadOnlyList<AnotacaoRegistrada>? Anotacoes = null);

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

/// <summary>Formato do arquivo exportado (treino ou sessão).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FormatoDeExportacao>))]
public enum FormatoDeExportacao
{
    Xlsx = 1,
    Pdf = 2,
}

/// <summary>
/// Um dia de treino na evolução de um exercício: a série mais pesada (carga e repetições dela), o
/// total de repetições e o volume (repetições × carga) do dia.
/// </summary>
public sealed record PontoDeEvolucao(
    Guid SessaoId, DateTimeOffset Data, decimal CargaMaximaKg, int RepeticoesNaCargaMaxima, int Repeticoes, decimal VolumeKg, int Series);

public sealed record EvolucaoDoExercicio(Guid ExercicioId, string NomeDoExercicio, IReadOnlyList<PontoDeEvolucao> Pontos);

/// <summary>Evolução de cada exercício nas últimas sessões de um treino, do dia mais antigo ao mais novo.</summary>
public sealed record EvolucaoDoTreino(Guid TreinoId, IReadOnlyList<EvolucaoDoExercicio> Exercicios);
