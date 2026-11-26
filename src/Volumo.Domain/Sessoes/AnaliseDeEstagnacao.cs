using Volumo.Domain.Comum;

namespace Volumo.Domain.Sessoes;

/// <summary>Como o personal quer medir a estagnação de um aluno. Gravado como texto; não renomear.</summary>
public enum CriterioDeEstagnacao
{
    /// <summary>Dias sem subir a carga do exercício.</summary>
    Tempo = 1,

    /// <summary>Sessões seguidas do exercício sem subir a carga.</summary>
    Sessoes = 2,
}

/// <summary>Maior carga usada num exercício em uma sessão finalizada.</summary>
public sealed record CargaNaSessao(DateTimeOffset Data, decimal MaiorCargaKg);

/// <summary>Exercício parado: maior carga até agora, desde quando não sobe e quantas sessões vieram depois.</summary>
public sealed record Estagnacao(decimal MaiorCargaKg, DateTimeOffset UltimoAumentoEm, int SessoesDesdeOAumento);

/// <summary>
/// Regra de estagnação do painel do personal. "Subir peso" é uma sessão cuja maior carga supera a de
/// todas as sessões anteriores do exercício (a primeira sessão é a base). Exercícios só com peso
/// corporal (carga 0) não entram: não há peso para subir.
/// </summary>
public static class AnaliseDeEstagnacao
{
    public const CriterioDeEstagnacao CriterioPadrao = CriterioDeEstagnacao.Tempo;
    public const int LimitePadrao = 15;
    public const int DiasMinimo = 7;
    public const int DiasMaximo = 90;
    public const int SessoesMinimo = 2;
    public const int SessoesMaximo = 10;

    /// <summary>Valida o limite conforme o critério (dias ou sessões).</summary>
    public static int ValidarLimite(CriterioDeEstagnacao criterio, int limite) =>
        Guarda.EnumValido(criterio, "Critério de estagnação") == CriterioDeEstagnacao.Tempo
            ? Guarda.Intervalo(limite, DiasMinimo, DiasMaximo, "Dias sem subir peso")
            : Guarda.Intervalo(limite, SessoesMinimo, SessoesMaximo, "Sessões sem subir peso");

    /// <summary>Estagnação do exercício, ou nulo se ele está progredindo (ou não há dados para dizer).</summary>
    public static Estagnacao? Avaliar(IEnumerable<CargaNaSessao> sessoes, CriterioDeEstagnacao criterio, int limite, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(sessoes);
        var ordenadas = sessoes.OrderBy(s => s.Data).ToList();
        if (ordenadas.Count < 2 || ordenadas.TrueForAll(s => s.MaiorCargaKg <= 0))
        {
            return null;
        }

        var maior = ordenadas[0].MaiorCargaKg;
        var ultimoAumentoEm = ordenadas[0].Data;
        var depois = 0;
        foreach (var sessao in ordenadas.Skip(1))
        {
            if (sessao.MaiorCargaKg > maior)
            {
                maior = sessao.MaiorCargaKg;
                ultimoAumentoEm = sessao.Data;
                depois = 0;
            }
            else
            {
                depois++;
            }
        }

        var estagnado = criterio == CriterioDeEstagnacao.Tempo
            ? depois > 0 && agora - ultimoAumentoEm >= TimeSpan.FromDays(limite) && agora - ordenadas[^1].Data <= TimeSpan.FromDays(limite)
            : depois >= limite;

        return estagnado ? new Estagnacao(maior, ultimoAumentoEm, depois) : null;
    }
}
