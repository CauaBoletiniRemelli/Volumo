using Volumo.Domain.Comum;

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
