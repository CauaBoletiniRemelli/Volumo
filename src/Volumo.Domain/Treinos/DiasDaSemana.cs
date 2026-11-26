namespace Volumo.Domain.Treinos;

/// <summary>
/// Dias da semana em que o treino está marcado (Treino A na segunda e na quinta...). Cada dia é o bit
/// de <see cref="DayOfWeek"/> (domingo = 1, segunda = 2...), então dá para combinar vários.
/// </summary>
[Flags]
public enum DiasDaSemana
{
    Nenhum = 0,
    Domingo = 1 << DayOfWeek.Sunday,
    Segunda = 1 << DayOfWeek.Monday,
    Terca = 1 << DayOfWeek.Tuesday,
    Quarta = 1 << DayOfWeek.Wednesday,
    Quinta = 1 << DayOfWeek.Thursday,
    Sexta = 1 << DayOfWeek.Friday,
    Sabado = 1 << DayOfWeek.Saturday,
    Todos = Domingo | Segunda | Terca | Quarta | Quinta | Sexta | Sabado,
}

public static class DiasDaSemanaExtensoes
{
    public static DiasDaSemana ParaDias(this IEnumerable<DayOfWeek> dias) =>
        dias.Aggregate(DiasDaSemana.Nenhum, (soma, dia) => soma | (DiasDaSemana)(1 << (int)dia));

    /// <summary>Os dias marcados, de domingo a sábado.</summary>
    public static IReadOnlyList<DayOfWeek> ParaLista(this DiasDaSemana dias) =>
        [.. Enum.GetValues<DayOfWeek>().Where(d => dias.HasFlag((DiasDaSemana)(1 << (int)d)))];
}
