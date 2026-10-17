using Volumo.Domain.Comum;
using Volumo.Domain.Exercicios;

namespace Volumo.Domain.Tests.Exercicios;

public sealed class ExercicioTests
{
    [Fact]
    public void Exercicio_do_catalogo_nao_tem_personal()
    {
        var exercicio = Exercicio.CriarDoCatalogo(Dados.NovoId(), "Supino reto", GrupoMuscular.Peitoral);

        exercicio.DoCatalogo.ShouldBeTrue();
        exercicio.PersonalId.ShouldBeNull();
    }

    [Fact]
    public void Exercicio_personalizado_pertence_ao_personal()
    {
        var personalId = Dados.NovoId();

        var exercicio = Exercicio.CriarPersonalizado(Dados.NovoId(), personalId, "Remada no banco 45°", GrupoMuscular.Costas);

        exercicio.DoCatalogo.ShouldBeFalse();
        exercicio.PersonalId.ShouldBe(personalId);
    }

    [Fact]
    public void Exercicio_inativo_nao_pode_ser_alterado()
    {
        var exercicio = Exercicio.CriarDoCatalogo(Dados.NovoId(), "Supino reto", GrupoMuscular.Peitoral);
        exercicio.Inativar(Dados.Agora);

        Should.Throw<RegraDeNegocioException>(() => exercicio.Atualizar("Supino inclinado", GrupoMuscular.Peitoral));
    }

    [Fact]
    public void Grupo_muscular_fora_do_enum_e_rejeitado()
    {
        Should.Throw<RegraDeNegocioException>(() =>
            Exercicio.CriarDoCatalogo(Dados.NovoId(), "Inventado", (GrupoMuscular)999));
    }
}
