using Volumo.Domain.Comum;

namespace Volumo.Domain.Treinos;

/// <summary>Série prescrita: faixa de repetições, carga sugerida e descanso. Imutável.</summary>
public sealed class Serie
{
    public const int RepeticoesLimite = 100;
    public const int DescansoMaximoSegundos = 900;

    private Serie()
    {
    }

    private Serie(TipoSerie tipo, int repeticoesMinimas, int repeticoesMaximas, int descansoSegundos, decimal? cargaKg, int ordem)
    {
        Tipo = tipo;
        RepeticoesMinimas = repeticoesMinimas;
        RepeticoesMaximas = repeticoesMaximas;
        DescansoSegundos = descansoSegundos;
        CargaKg = cargaKg;
        Ordem = ordem;
    }

    /// <summary>Posição dentro do exercício, a partir de 1. Definida pelo <see cref="ItemTreino"/>.</summary>
    public int Ordem { get; private set; }

    public TipoSerie Tipo { get; private set; }

    public int RepeticoesMinimas { get; private set; }

    public int RepeticoesMaximas { get; private set; }

    public int DescansoSegundos { get; private set; }

    /// <summary>Carga sugerida; nula quando o aluno escolhe (ou peso corporal).</summary>
    public decimal? CargaKg { get; private set; }

    public static Serie Criar(TipoSerie tipo, int repeticoesMinimas, int repeticoesMaximas, int descansoSegundos, decimal? cargaKg = null)
    {
        Guarda.EnumValido(tipo, "Tipo da série");
        Guarda.Intervalo(repeticoesMinimas, 1, RepeticoesLimite, "Repetições mínimas");
        Guarda.Intervalo(repeticoesMaximas, repeticoesMinimas, RepeticoesLimite, "Repetições máximas");
        Guarda.Intervalo(descansoSegundos, 0, DescansoMaximoSegundos, "Descanso (segundos)");
        if (cargaKg is { } carga)
        {
            Guarda.Carga(carga, "Carga");
        }

        return new Serie(tipo, repeticoesMinimas, repeticoesMaximas, descansoSegundos, cargaKg, ordem: 0);
    }

    /// <summary>Cópia na posição indicada; cada item guarda as próprias instâncias.</summary>
    internal Serie NaPosicao(int ordem) =>
        new(Tipo, RepeticoesMinimas, RepeticoesMaximas, DescansoSegundos, CargaKg, ordem);
}
