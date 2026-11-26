using Volumo.Domain.Comum;

namespace Volumo.Domain.Sessoes;

/// <summary>
/// Cardio que o aluno fez na sessão: exercício e minutos. Fica separado das séries para não entrar
/// no volume (repetições × carga) nem na análise de estagnação.
/// </summary>
public sealed class RegistroCardio : Entidade
{
    public const int DuracaoMaximaMinutos = 300;

    private RegistroCardio()
    {
    }

    internal RegistroCardio(Guid id, Guid sessaoId, Guid exercicioId, int duracaoMinutos, DateTimeOffset realizadoEm)
        : base(id)
    {
        SessaoId = sessaoId;
        ExercicioId = Guarda.Referencia(exercicioId, "Exercício");
        DuracaoMinutos = Guarda.Intervalo(duracaoMinutos, 1, DuracaoMaximaMinutos, "Duração do cardio (minutos)");
        RealizadoEm = realizadoEm;
    }

    public Guid SessaoId { get; private set; }

    public Guid ExercicioId { get; private set; }

    public int DuracaoMinutos { get; private set; }

    public DateTimeOffset RealizadoEm { get; private set; }
}
