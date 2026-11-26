using Volumo.Domain.Comum;
using Volumo.Domain.Sessoes;

namespace Volumo.Domain.Vinculos;

/// <summary>
/// Liga um aluno ao personal. Cada aluno tem no máximo um vínculo ativo; o banco garante
/// isso com um índice único filtrado. Vínculos encerrados ficam como histórico.
/// </summary>
public sealed class VinculoPersonalAluno : Entidade
{
    private VinculoPersonalAluno()
    {
    }

    private VinculoPersonalAluno(Guid id, Guid personalId, Guid alunoId, DateTimeOffset iniciadoEm)
        : base(id)
    {
        PersonalId = Guarda.Referencia(personalId, "Personal");
        AlunoId = Guarda.Referencia(alunoId, "Aluno");
        IniciadoEm = iniciadoEm;
    }

    public Guid PersonalId { get; private set; }

    public Guid AlunoId { get; private set; }

    public DateTimeOffset IniciadoEm { get; private set; }

    public DateTimeOffset? EncerradoEm { get; private set; }

    public bool Ativo => EncerradoEm is null;

    /// <summary>Como este personal mede a estagnação deste aluno (padrão: 15 dias sem subir peso).</summary>
    public CriterioDeEstagnacao CriterioDeEstagnacao { get; private set; } = AnaliseDeEstagnacao.CriterioPadrao;

    /// <summary>Dias ou sessões, conforme <see cref="CriterioDeEstagnacao"/>.</summary>
    public int LimiteDeEstagnacao { get; private set; } = AnaliseDeEstagnacao.LimitePadrao;

    public void ConfigurarEstagnacao(CriterioDeEstagnacao criterio, int limite)
    {
        if (!Ativo)
        {
            throw new RegraDeNegocioException("Este vínculo já foi encerrado.");
        }

        LimiteDeEstagnacao = AnaliseDeEstagnacao.ValidarLimite(criterio, limite);
        CriterioDeEstagnacao = criterio;
    }

    public static VinculoPersonalAluno Criar(Guid id, Guid personalId, Guid alunoId, DateTimeOffset agora) =>
        new(id, personalId, alunoId, agora);

    public void Encerrar(DateTimeOffset agora)
    {
        if (!Ativo)
        {
            throw new RegraDeNegocioException("Este vínculo já foi encerrado.");
        }

        if (agora < IniciadoEm)
        {
            throw new RegraDeNegocioException("O encerramento não pode ser anterior ao início do vínculo.");
        }

        EncerradoEm = agora;
    }
}
