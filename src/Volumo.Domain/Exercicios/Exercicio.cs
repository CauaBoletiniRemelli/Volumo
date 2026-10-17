using Volumo.Domain.Comum;

namespace Volumo.Domain.Exercicios;

/// <summary>
/// Exercício do catálogo da plataforma (sem personal) ou criado por um personal,
/// visível só para ele e os alunos dele.
/// </summary>
public sealed class Exercicio : EntidadeInativavel
{
    public const int NomeMaximo = 100;

    private Exercicio()
    {
    }

    private Exercicio(Guid id, Guid? personalId, string nome, GrupoMuscular grupoMuscular)
        : base(id)
    {
        PersonalId = personalId;
        DefinirDados(nome, grupoMuscular);
    }

    public string Nome { get; private set; } = null!;

    public GrupoMuscular GrupoMuscular { get; private set; }

    /// <summary>Nulo para exercícios do catálogo da plataforma.</summary>
    public Guid? PersonalId { get; private set; }

    public bool DoCatalogo => PersonalId is null;

    public static Exercicio CriarDoCatalogo(Guid id, string nome, GrupoMuscular grupoMuscular) =>
        new(id, null, nome, grupoMuscular);

    public static Exercicio CriarPersonalizado(Guid id, Guid personalId, string nome, GrupoMuscular grupoMuscular) =>
        new(id, Guarda.Referencia(personalId, "Personal"), nome, grupoMuscular);

    public void Atualizar(string nome, GrupoMuscular grupoMuscular)
    {
        GarantirAtivo();
        DefinirDados(nome, grupoMuscular);
    }

    private void DefinirDados(string nome, GrupoMuscular grupoMuscular)
    {
        Nome = Guarda.Texto(nome, "Nome do exercício", NomeMaximo);
        GrupoMuscular = Guarda.EnumValido(grupoMuscular, "Grupo muscular");
    }
}
