using Volumo.Domain.Comum;

namespace Volumo.Domain.Treinos;

/// <summary>
/// Um exercício dentro de um treino ou template, com as séries prescritas.
/// Alterações passam pelo <see cref="PlanoDeTreino"/> (DefinirSeries, DefinirObservacao).
/// </summary>
public sealed class ItemTreino
{
    public const int MaximoSeries = 20;
    public const int ObservacaoMaxima = 500;

    private readonly List<Serie> _series = [];

    private ItemTreino()
    {
    }

    internal ItemTreino(Guid id, int ordem, Guid exercicioId, IEnumerable<Serie> series, string? observacao)
    {
        Id = Guarda.IdNovo(id, nameof(Id));
        Ordem = ordem;
        ExercicioId = Guarda.Referencia(exercicioId, "Exercício");
        DefinirSeries(series);
        DefinirObservacao(observacao);
    }

    public Guid Id { get; private set; }

    /// <summary>Posição no treino, a partir de 1. Mantida pelo <see cref="PlanoDeTreino"/>.</summary>
    public int Ordem { get; internal set; }

    public Guid ExercicioId { get; private set; }

    public string? Observacao { get; private set; }

    public IReadOnlyList<Serie> Series => [.. _series.OrderBy(s => s.Ordem)];

    internal void DefinirSeries(IEnumerable<Serie> series)
    {
        ArgumentNullException.ThrowIfNull(series);

        var novas = series.Select((serie, indice) => serie.NaPosicao(indice + 1)).ToList();
        if (novas.Count == 0)
        {
            throw new RegraDeNegocioException("Cada exercício precisa de pelo menos uma série.");
        }

        if (novas.Count > MaximoSeries)
        {
            throw new RegraDeNegocioException($"Cada exercício pode ter no máximo {MaximoSeries} séries.");
        }

        _series.Clear();
        _series.AddRange(novas);
    }

    internal void DefinirObservacao(string? observacao) =>
        Observacao = Guarda.TextoOpcional(observacao, "Observação", ObservacaoMaxima);

    internal ItemTreino Copiar() => new(Guid.CreateVersion7(), Ordem, ExercicioId, Series, Observacao);
}
