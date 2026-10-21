using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Volumo.Shared.Painel;

// Painel do personal: quem treinou, quem sumiu e quem estagnou. Limites e regras ficam no domínio
// (validados de novo no servidor).

[JsonConverter(typeof(JsonStringEnumConverter<CriterioDeEstagnacao>))]
public enum CriterioDeEstagnacao
{
    /// <summary>Dias sem subir a carga.</summary>
    Tempo = 1,

    /// <summary>Sessões seguidas sem subir a carga.</summary>
    Sessoes = 2,
}

[JsonConverter(typeof(JsonStringEnumConverter<SituacaoDoAluno>))]
public enum SituacaoDoAluno
{
    EmDia = 1,

    /// <summary>Mais dias sem treinar do que o limite do painel.</summary>
    Sumido = 2,

    /// <summary>Nenhuma sessão registrada desde o vínculo.</summary>
    NuncaTreinou = 3,
}

/// <summary>Como o personal mede a estagnação de um aluno. Limite em dias (Tempo) ou em sessões.</summary>
public sealed record ConfiguracaoDeEstagnacao([Required] CriterioDeEstagnacao Criterio, [Range(1, 90)] int Limite);

public sealed record ExercicioEstagnado(
    Guid ExercicioId, string NomeDoExercicio, decimal MaiorCargaKg, DateTimeOffset UltimoAumentoEm, int SessoesDesdeOAumento);

public sealed record AlunoNoPainel(
    Guid Id,
    string Nome,
    DateTimeOffset VinculadoDesde,
    SituacaoDoAluno Situacao,
    int DiasSemTreinar,
    DateTimeOffset? UltimoTreinoEm,
    string? NomeDoUltimoTreino,
    int TreinosNosUltimos7Dias,
    ConfiguracaoDeEstagnacao Estagnacao,
    IReadOnlyList<ExercicioEstagnado> Estagnados);

/// <summary>Alunos com vínculo ativo, em ordem alfabética.</summary>
public sealed record PainelDoPersonal(DateTimeOffset GeradoEm, int DiasParaSumido, IReadOnlyList<AlunoNoPainel> Alunos);
