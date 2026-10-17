using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Volumo.Shared.Treinos;

// Contratos de exercícios, templates e treinos. Os limites espelham as regras do domínio
// (validadas de novo no servidor).

[JsonConverter(typeof(JsonStringEnumConverter<GrupoMuscular>))]
public enum GrupoMuscular
{
    Peitoral = 1,
    Costas = 2,
    Ombros = 3,
    Biceps = 4,
    Triceps = 5,
    Antebraco = 6,
    Trapezio = 7,
    Abdomen = 8,
    Lombar = 9,
    Quadriceps = 10,
    Posteriores = 11,
    Gluteos = 12,
    Panturrilhas = 13,
    Adutores = 14,
    Abdutores = 15,
    CorpoInteiro = 16,
    Cardio = 17,
}

[JsonConverter(typeof(JsonStringEnumConverter<TipoDeSerie>))]
public enum TipoDeSerie
{
    Aquecimento = 1,
    Trabalho = 2,
}

public sealed record ExercicioResumo(Guid Id, string Nome, GrupoMuscular GrupoMuscular, bool DoCatalogo);

public sealed record DadosDoExercicio(
    [Required, StringLength(100)] string Nome,
    [Required] GrupoMuscular GrupoMuscular);

public sealed record SeriePrescrita(
    TipoDeSerie Tipo,
    [Range(1, 100)] int RepeticoesMinimas,
    [Range(1, 100)] int RepeticoesMaximas,
    [Range(0, 900)] int DescansoSegundos,
    [Range(0, 1000)] decimal? CargaKg);

public sealed record ItemDoPlano(
    [Required] Guid ExercicioId,
    [Required, MinLength(1), MaxLength(20)] IReadOnlyList<SeriePrescrita> Series,
    [StringLength(500)] string? Observacao);

/// <summary>Treino ou template inteiro, como o construtor envia (a gravação substitui tudo).</summary>
public sealed record PlanoDeTreinoEntrada(
    [Required, StringLength(100)] string Nome,
    [Required, MaxLength(40)] IReadOnlyList<ItemDoPlano> Itens);

public sealed record ItemDoPlanoSaida(
    Guid Id, int Ordem, Guid ExercicioId, string NomeDoExercicio, string? Observacao, IReadOnlyList<SeriePrescrita> Series);

public sealed record TemplateResumo(Guid Id, string Nome, int QuantidadeDeExercicios);

public sealed record TemplateDetalhe(Guid Id, string Nome, IReadOnlyList<ItemDoPlanoSaida> Itens);

public sealed record TreinoResumo(Guid Id, string Nome, Guid? TemplateOrigemId, int QuantidadeDeExercicios);

public sealed record TreinoDetalhe(Guid Id, string Nome, Guid AlunoId, Guid? PersonalId, IReadOnlyList<ItemDoPlanoSaida> Itens);

public sealed record AtribuicaoDeTemplate([Required] Guid AlunoId);

public sealed record RecursoCriado(Guid Id);
