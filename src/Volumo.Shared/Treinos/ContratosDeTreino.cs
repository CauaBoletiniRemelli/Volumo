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

    /// <summary>Aproximação antes da primeira série de trabalho (carga perto da de trabalho, poucas repetições).</summary>
    Preparatoria = 3,
}

[JsonConverter(typeof(JsonStringEnumConverter<Equipamento>))]
public enum Equipamento
{
    Barra = 1,
    Halteres = 2,
    Maquina = 3,
    Polia = 4,
    PesoCorporal = 5,
    Kettlebell = 6,
    Elastico = 7,
    Anilha = 8,
    Suspensao = 9,
    Outro = 10,
}

/// <param name="FotoUrl">URL com prazo (algumas horas); nula quando o exercício não tem foto.</param>
public sealed record ExercicioResumo(Guid Id, string Nome, GrupoMuscular GrupoMuscular, bool DoCatalogo, string? FotoUrl = null);

/// <summary>
/// Exercício com o pacote de informações do catálogo: músculos, equipamento, instruções em passos,
/// foto e vídeo curto de execução. As URLs têm prazo de algumas horas.
/// </summary>
public sealed record ExercicioDetalhe(
    Guid Id,
    string Nome,
    GrupoMuscular GrupoMuscular,
    bool DoCatalogo,
    IReadOnlyList<GrupoMuscular> MusculosSecundarios,
    Equipamento? Equipamento,
    IReadOnlyList<string> Instrucoes,
    string? FotoUrl,
    string? VideoUrl,
    bool Ativo = true);

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

/// <summary>Quando fazer o cardio. O aluno vê a recomendação do personal e pode trocar na hora.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MomentoDoCardio>))]
public enum MomentoDoCardio
{
    Antes = 1,
    Depois = 2,
    AlunoEscolhe = 3,
}

/// <summary>Cardio da aba "Cardio" do construtor: exercício do grupo Cardio, minutos e intensidade.</summary>
public sealed record CardioPrescrito(
    [Required] Guid ExercicioId,
    [Range(1, 180)] int DuracaoMinutos,
    [StringLength(500)] string? Observacao);

/// <summary>Treino ou template inteiro, como o construtor envia (a gravação substitui tudo).</summary>
/// <param name="Cardios">Opcional: sem a lista, o treino fica sem cardio.</param>
public sealed record PlanoDeTreinoEntrada(
    [Required, StringLength(100)] string Nome,
    [Required, MaxLength(40)] IReadOnlyList<ItemDoPlano> Itens,
    [MaxLength(10)] IReadOnlyList<CardioPrescrito>? Cardios = null,
    MomentoDoCardio MomentoDoCardio = MomentoDoCardio.Depois);

/// <param name="Observacao">Do personal (o aluno só lê).</param>
/// <param name="ObservacaoDoAluno">Escrita pelo aluno; só em treino (em template é sempre nula).</param>
/// <param name="FotoUrl">Foto do exercício, URL com prazo (algumas horas); nula quando não tem foto.</param>
public sealed record ItemDoPlanoSaida(
    Guid Id, int Ordem, Guid ExercicioId, string NomeDoExercicio, string? Observacao, IReadOnlyList<SeriePrescrita> Series,
    string? ObservacaoDoAluno = null, string? FotoUrl = null);

public sealed record CardioDoPlanoSaida(
    Guid Id, int Ordem, Guid ExercicioId, string NomeDoExercicio, int DuracaoMinutos, string? Observacao, string? ObservacaoDoAluno = null,
    string? FotoUrl = null);

/// <summary>Observação do aluno num exercício do treino, gravada direto (com internet). Vazio limpa.</summary>
public sealed record ObservacaoDoAlunoEntrada([StringLength(500)] string? Texto);

public sealed record TemplateResumo(Guid Id, string Nome, int QuantidadeDeExercicios);

/// <param name="Cardios">Nulo só em cópias antigas guardadas no aparelho; use <see cref="CardiosOuVazio"/>.</param>
public sealed record TemplateDetalhe(
    Guid Id, string Nome, IReadOnlyList<ItemDoPlanoSaida> Itens,
    IReadOnlyList<CardioDoPlanoSaida>? Cardios = null, MomentoDoCardio MomentoDoCardio = MomentoDoCardio.Depois)
{
    [JsonIgnore]
    public IReadOnlyList<CardioDoPlanoSaida> CardiosOuVazio => Cardios ?? [];
}

public sealed record TreinoResumo(Guid Id, string Nome, Guid? TemplateOrigemId, int QuantidadeDeExercicios);

/// <param name="Cardios">Nulo só em cópias antigas guardadas no aparelho; use <see cref="CardiosOuVazio"/>.</param>
public sealed record TreinoDetalhe(
    Guid Id, string Nome, Guid AlunoId, Guid? PersonalId, IReadOnlyList<ItemDoPlanoSaida> Itens,
    IReadOnlyList<CardioDoPlanoSaida>? Cardios = null, MomentoDoCardio MomentoDoCardio = MomentoDoCardio.Depois)
{
    [JsonIgnore]
    public IReadOnlyList<CardioDoPlanoSaida> CardiosOuVazio => Cardios ?? [];
}

public sealed record AtribuicaoDeTemplate([Required] Guid AlunoId);

/// <summary>Todos os treinos ativos do aluno, na ordem em que devem aparecer para ele.</summary>
public sealed record OrdemDosTreinos([Required, MaxLength(100)] IReadOnlyList<Guid> TreinoIds);

public sealed record RecursoCriado(Guid Id);
