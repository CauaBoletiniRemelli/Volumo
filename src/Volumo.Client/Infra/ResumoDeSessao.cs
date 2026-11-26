using Volumo.Shared.Sessoes;

namespace Volumo.Client.Infra;

public sealed record SerieNoResumo(decimal CargaKg, int Repeticoes, int? Rir);

/// <param name="UltimaVez">Séries da vez anterior deste treino, quando o aparelho tem (para comparar).</param>
public sealed record ExercicioNoResumo(string Nome, IReadOnlyList<SerieNoResumo> Series, IReadOnlyList<SerieAnterior>? UltimaVez)
{
    public decimal Volume => Series.Sum(s => s.Repeticoes * s.CargaKg);

    public decimal CargaMaxima => Series.Count == 0 ? 0 : Series.Max(s => s.CargaKg);

    /// <summary>Subiu a carga máxima em relação à última vez.</summary>
    public bool SubiuCarga => UltimaVez is { Count: > 0 } antes && CargaMaxima > antes.Max(s => s.CargaKg);

    /// <summary>Diferença de volume para a última vez, em % (nulo sem comparação).</summary>
    public int? VariacaoDeVolume
    {
        get
        {
            var antes = UltimaVez?.Sum(s => s.Repeticoes * s.CargaKg) ?? 0;
            return antes <= 0 ? null : (int)Math.Round((Volume - antes) / antes * 100);
        }
    }
}

public sealed record CardioNoResumo(string Nome, int DuracaoMinutos);

/// <summary>
/// Resumo geral de um treino feito, montado a partir da sessão que acabou de terminar no aparelho
/// ou de uma sessão do histórico (vinda da API).
/// </summary>
/// <param name="ExerciciosPrevistos">Quantos exercícios o treino tinha (nulo quando não se sabe).</param>
public sealed record ResumoDeSessao(
    string NomeDoTreino, DateTimeOffset Inicio, DateTimeOffset? Fim,
    IReadOnlyList<ExercicioNoResumo> Exercicios, IReadOnlyList<CardioNoResumo> Cardios, int? ExerciciosPrevistos = null)
{
    public int Series => Exercicios.Sum(e => e.Series.Count);

    public int Repeticoes => Exercicios.Sum(e => e.Series.Sum(s => s.Repeticoes));

    public decimal Volume => Exercicios.Sum(e => e.Volume);

    public int MinutosDeCardio => Cardios.Sum(c => c.DuracaoMinutos);

    /// <summary>Séries agrupadas por exercício, na ordem em que foram feitas.</summary>
    public static ResumoDeSessao De(SessaoDetalhe sessao)
    {
        ArgumentNullException.ThrowIfNull(sessao);
        return new ResumoDeSessao(
            sessao.NomeDoTreino ?? "Treino livre",
            sessao.IniciadaEm,
            sessao.FinalizadaEm,
            [.. sessao.Series.OrderBy(s => s.RealizadaEm).GroupBy(s => s.ExercicioId)
                .Select(g => new ExercicioNoResumo(g.First().NomeDoExercicio, [.. g.Select(s => new SerieNoResumo(s.CargaKg, s.Repeticoes, s.Rir))], null))],
            [.. (sessao.Cardios ?? []).Select(c => new CardioNoResumo(c.NomeDoExercicio, c.DuracaoMinutos))]);
    }
}
