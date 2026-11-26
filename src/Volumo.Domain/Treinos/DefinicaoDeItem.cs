namespace Volumo.Domain.Treinos;

/// <summary>Um exercício como o construtor de treino envia: qual exercício, as séries e a observação.</summary>
public sealed record DefinicaoDeItem(Guid ExercicioId, IReadOnlyList<Serie> Series, string? Observacao = null);
