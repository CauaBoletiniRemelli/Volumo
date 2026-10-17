using Volumo.Domain.Comum;

namespace Volumo.Domain.Sessoes;

/// <summary>
/// Uma ida à academia. Pode ser registrada offline: os Ids vêm do app e registrar a mesma
/// série duas vezes (reenvio na sincronização) não duplica nada.
/// </summary>
public sealed class SessaoTreino : Entidade
{
    public const int MaximoRegistros = 300;

    private readonly List<RegistroSerie> _registros = [];

    private SessaoTreino()
    {
    }

    private SessaoTreino(Guid id, Guid alunoId, Guid? treinoId, DateTimeOffset iniciadaEm)
        : base(id)
    {
        AlunoId = Guarda.Referencia(alunoId, "Aluno");
        TreinoId = treinoId is { } treino ? Guarda.Referencia(treino, "Treino") : null;
        IniciadaEm = iniciadaEm;
    }

    public Guid AlunoId { get; private set; }

    /// <summary>Treino seguido; nulo em sessão livre ou se o treino foi excluído depois.</summary>
    public Guid? TreinoId { get; private set; }

    public DateTimeOffset IniciadaEm { get; private set; }

    public DateTimeOffset? FinalizadaEm { get; private set; }

    public bool Finalizada => FinalizadaEm is not null;

    public IReadOnlyList<RegistroSerie> Registros => [.. _registros.OrderBy(r => r.RealizadaEm)];

    public static SessaoTreino Iniciar(Guid id, Guid alunoId, Guid? treinoId, DateTimeOffset iniciadaEm) =>
        new(id, alunoId, treinoId, iniciadaEm);

    /// <summary>Registra uma série. Se o Id já existe na sessão, devolve o registro existente (idempotente).</summary>
    public RegistroSerie RegistrarSerie(Guid registroId, Guid exercicioId, int repeticoes, decimal cargaKg, int? rir, DateTimeOffset realizadaEm)
    {
        var existente = _registros.Find(r => r.Id == registroId);
        if (existente is not null)
        {
            return existente;
        }

        if (Finalizada)
        {
            throw new RegraDeNegocioException("Esta sessão já foi finalizada.");
        }

        if (realizadaEm < IniciadaEm)
        {
            throw new RegraDeNegocioException("A série não pode ser anterior ao início da sessão.");
        }

        if (_registros.Count >= MaximoRegistros)
        {
            throw new RegraDeNegocioException($"Uma sessão pode ter no máximo {MaximoRegistros} séries.");
        }

        var registro = new RegistroSerie(registroId, Id, exercicioId, repeticoes, cargaKg, rir, realizadaEm);
        _registros.Add(registro);
        return registro;
    }

    /// <summary>Finaliza a sessão. Finalizar de novo não muda nada (reenvio na sincronização).</summary>
    public void Finalizar(DateTimeOffset finalizadaEm)
    {
        if (Finalizada)
        {
            return;
        }

        if (finalizadaEm < IniciadaEm)
        {
            throw new RegraDeNegocioException("O fim da sessão não pode ser anterior ao início.");
        }

        if (_registros.Exists(r => r.RealizadaEm > finalizadaEm))
        {
            throw new RegraDeNegocioException("O fim da sessão não pode ser anterior às séries registradas.");
        }

        FinalizadaEm = finalizadaEm;
    }
}
