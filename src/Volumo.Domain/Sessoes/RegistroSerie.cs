using Volumo.Domain.Comum;

namespace Volumo.Domain.Sessoes;

/// <summary>Série que o aluno de fato executou: repetições, carga e quantas repetições sobraram (RIR).</summary>
public sealed class RegistroSerie : Entidade
{
    public const int RepeticoesLimite = 200;
    public const int RirMaximo = 10;

    private RegistroSerie()
    {
    }

    internal RegistroSerie(Guid id, Guid sessaoId, Guid exercicioId, int repeticoes, decimal cargaKg, int? rir, DateTimeOffset realizadaEm)
        : base(id)
    {
        SessaoId = sessaoId;
        ExercicioId = Guarda.Referencia(exercicioId, "Exercício");
        Repeticoes = Guarda.Intervalo(repeticoes, 0, RepeticoesLimite, "Repetições");
        CargaKg = Guarda.Carga(cargaKg, "Carga");
        Rir = rir is { } valor ? Guarda.Intervalo(valor, 0, RirMaximo, "Repetições de sobra (RIR)") : null;
        RealizadaEm = realizadaEm;
    }

    public Guid SessaoId { get; private set; }

    public Guid ExercicioId { get; private set; }

    public int Repeticoes { get; private set; }

    public decimal CargaKg { get; private set; }

    public int? Rir { get; private set; }

    public DateTimeOffset RealizadaEm { get; private set; }
}
