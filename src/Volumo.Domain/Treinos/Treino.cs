using Volumo.Domain.Comum;

namespace Volumo.Domain.Treinos;

/// <summary>Treino de um aluno, prescrito pelo personal ou montado pelo próprio aluno.</summary>
public sealed class Treino : PlanoDeTreino
{
    private Treino()
    {
    }

    private Treino(Guid id, Guid alunoId, Guid? personalId, string nome)
        : base(id, nome)
    {
        AlunoId = Guarda.Referencia(alunoId, "Aluno");
        PersonalId = personalId is { } personal ? Guarda.Referencia(personal, "Personal") : null;
    }

    public Guid AlunoId { get; private set; }

    /// <summary>Nulo quando o próprio aluno montou o treino (quem treina sozinho).</summary>
    public Guid? PersonalId { get; private set; }

    public Guid? TemplateOrigemId { get; private set; }

    public static Treino CriarPeloPersonal(Guid id, Guid personalId, Guid alunoId, string nome) =>
        new(id, alunoId, personalId, nome);

    public static Treino CriarPeloAluno(Guid id, Guid alunoId, string nome) => new(id, alunoId, null, nome);

    internal static Treino CriarDeTemplate(Guid id, Guid alunoId, TemplateTreino template)
    {
        var treino = new Treino(id, alunoId, template.PersonalId, template.Nome) { TemplateOrigemId = template.Id };
        treino.CopiarItensDe(template);
        return treino;
    }
}
