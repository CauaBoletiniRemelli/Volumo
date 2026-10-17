using Volumo.Domain.Comum;

namespace Volumo.Domain.Treinos;

/// <summary>Modelo reutilizável do personal. Atribuir a um aluno gera uma cópia independente.</summary>
public sealed class TemplateTreino : PlanoDeTreino
{
    private TemplateTreino()
    {
    }

    private TemplateTreino(Guid id, Guid personalId, string nome)
        : base(id, nome) => PersonalId = Guarda.Referencia(personalId, "Personal");

    public Guid PersonalId { get; private set; }

    public static TemplateTreino Criar(Guid id, Guid personalId, string nome) => new(id, personalId, nome);

    /// <summary>Copia o template para o aluno; mudanças posteriores no template não afetam o treino.</summary>
    public Treino GerarTreinoPara(Guid treinoId, Guid alunoId)
    {
        GarantirAtivo();
        return Treino.CriarDeTemplate(treinoId, alunoId, this);
    }
}
