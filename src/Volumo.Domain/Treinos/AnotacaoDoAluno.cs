using Volumo.Domain.Comum;

namespace Volumo.Domain.Treinos;

/// <summary>
/// Observação que o próprio aluno escreve num exercício do treino (ajuste do banco, pegada, dor).
/// Fica separada da observação do personal, que o aluno não pode alterar, e é guardada por treino e
/// exercício: o personal recria os itens ao editar o treino, e a anotação continua valendo.
/// Texto vazio limpa a anotação (a linha fica, para a data da última mudança continuar valendo).
/// </summary>
public sealed class AnotacaoDoAluno : Entidade
{
    public const int TextoMaximo = 500;

    private AnotacaoDoAluno()
    {
    }

    private AnotacaoDoAluno(Guid id, Guid alunoId, Guid treinoId, Guid exercicioId, string? texto, DateTimeOffset atualizadaEm)
        : base(id)
    {
        AlunoId = Guarda.Referencia(alunoId, "Aluno");
        TreinoId = Guarda.Referencia(treinoId, "Treino");
        ExercicioId = Guarda.Referencia(exercicioId, "Exercício");
        Texto = Guarda.TextoOpcional(texto, "Observação", TextoMaximo);
        AtualizadaEm = atualizadaEm;
    }

    public Guid AlunoId { get; private set; }

    public Guid TreinoId { get; private set; }

    public Guid ExercicioId { get; private set; }

    public string? Texto { get; private set; }

    public DateTimeOffset AtualizadaEm { get; private set; }

    public static AnotacaoDoAluno Criar(Guid id, Guid alunoId, Guid treinoId, Guid exercicioId, string? texto, DateTimeOffset em) =>
        new(id, alunoId, treinoId, exercicioId, texto, em);

    /// <summary>
    /// Troca o texto se a mudança for mais nova que a gravada (o app manda anotações feitas offline,
    /// às vezes de mais de um aparelho: vale a última). Devolve se mudou.
    /// </summary>
    public bool Atualizar(string? texto, DateTimeOffset em)
    {
        if (em <= AtualizadaEm)
        {
            return false;
        }

        Texto = Guarda.TextoOpcional(texto, "Observação", TextoMaximo);
        AtualizadaEm = em;
        return true;
    }

    /// <summary>Exclusão de conta: texto livre pode ter dado de saúde, então sai.</summary>
    public void Anonimizar() => Texto = null;
}
