using Volumo.Domain.Comum;

namespace Volumo.Domain.Treinos;

/// <summary>
/// Cardio prescrito dentro de um treino ou template, separado da musculação: um exercício de cardio,
/// a duração e uma observação (intensidade, velocidade, inclinação...). A ordem é mantida pelo
/// <see cref="PlanoDeTreino"/>.
/// </summary>
public sealed class ItemCardio
{
    public const int DuracaoMaximaMinutos = 180;
    public const int ObservacaoMaxima = 500;

    private ItemCardio()
    {
    }

    internal ItemCardio(Guid id, int ordem, Guid exercicioId, int duracaoMinutos, string? observacao)
    {
        Id = Guarda.IdNovo(id, nameof(Id));
        Ordem = ordem;
        ExercicioId = Guarda.Referencia(exercicioId, "Exercício");
        DuracaoMinutos = Guarda.Intervalo(duracaoMinutos, 1, DuracaoMaximaMinutos, "Duração do cardio (minutos)");
        Observacao = Guarda.TextoOpcional(observacao, "Observação do cardio", ObservacaoMaxima);
    }

    public Guid Id { get; private set; }

    /// <summary>Posição dentro do cardio do treino, a partir de 1.</summary>
    public int Ordem { get; private set; }

    public Guid ExercicioId { get; private set; }

    public int DuracaoMinutos { get; private set; }

    public string? Observacao { get; private set; }

    internal ItemCardio Copiar() => new(Guid.CreateVersion7(), Ordem, ExercicioId, DuracaoMinutos, Observacao);
}

/// <summary>Um cardio como o construtor de treino envia.</summary>
public sealed record DefinicaoDeCardio(Guid ExercicioId, int DuracaoMinutos, string? Observacao = null);

/// <summary>Quando o personal recomenda fazer o cardio. O aluno vê a recomendação e pode trocar.</summary>
public enum MomentoDoCardio
{
    Antes = 1,
    Depois = 2,
    AlunoEscolhe = 3,
}
