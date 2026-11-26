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
    public void Detalhes_do_catalogo_guardam_secundarios_equipamento_e_passos()
    {
        var exercicio = Exercicio.CriarDoCatalogo(Dados.NovoId(), "Supino reto", GrupoMuscular.Peitoral);

        exercicio.DefinirDetalhes(
            [GrupoMuscular.Triceps, GrupoMuscular.Ombros, GrupoMuscular.Triceps, GrupoMuscular.Peitoral],
            Equipamento.Barra,
            ["Deite no banco com os pés no chão.", "  ", "Desça a barra até o peito\ne empurre."]);

        exercicio.MusculosSecundarios.ShouldBe([GrupoMuscular.Triceps, GrupoMuscular.Ombros], "sem repetidos e sem o grupo principal");
        exercicio.Equipamento.ShouldBe(Equipamento.Barra);
        exercicio.Instrucoes.ShouldBe(["Deite no banco com os pés no chão.", "Desça a barra até o peito e empurre."]);
    }

    [Fact]
    public void Detalhes_vazios_limpam_tudo()
    {
        var exercicio = Exercicio.CriarDoCatalogo(Dados.NovoId(), "Supino reto", GrupoMuscular.Peitoral);
        exercicio.DefinirDetalhes([GrupoMuscular.Triceps], Equipamento.Barra, ["Passo"]);

        exercicio.DefinirDetalhes([], null, []);

        exercicio.MusculosSecundarios.ShouldBeEmpty();
        exercicio.Equipamento.ShouldBeNull();
        exercicio.Instrucoes.ShouldBeEmpty();
    }

    [Fact]
    public void Detalhes_alem_dos_limites_falham_sem_mudar_nada()
    {
        var exercicio = Exercicio.CriarDoCatalogo(Dados.NovoId(), "Supino reto", GrupoMuscular.Peitoral);
        exercicio.DefinirDetalhes([GrupoMuscular.Triceps], Equipamento.Barra, ["Passo"]);
        string[] passosDemais = [.. Enumerable.Range(1, Exercicio.MaximoPassos + 1).Select(i => $"Passo {i}")];

        Should.Throw<RegraDeNegocioException>(() => exercicio.DefinirDetalhes([], null, passosDemais));
        Should.Throw<RegraDeNegocioException>(() => exercicio.DefinirDetalhes([], null, [new string('x', Exercicio.PassoMaximo + 1)]));
        Should.Throw<RegraDeNegocioException>(() => exercicio.DefinirDetalhes(
            [GrupoMuscular.Costas, GrupoMuscular.Ombros, GrupoMuscular.Biceps, GrupoMuscular.Triceps, GrupoMuscular.Abdomen, GrupoMuscular.Lombar], null, []));
        Should.Throw<RegraDeNegocioException>(() => exercicio.DefinirDetalhes([], (Equipamento)99, []));

        exercicio.Instrucoes.ShouldBe(["Passo"]);
        exercicio.Equipamento.ShouldBe(Equipamento.Barra);
    }

    [Fact]
    public void Trocar_foto_ou_video_devolve_a_chave_anterior()
    {
        var exercicio = Exercicio.CriarDoCatalogo(Dados.NovoId(), "Supino reto", GrupoMuscular.Peitoral);

        exercicio.DefinirFoto("exercicios/a.jpg").ShouldBeNull();
        exercicio.DefinirFoto("exercicios/b.jpg").ShouldBe("exercicios/a.jpg");
        exercicio.DefinirVideo("exercicios/v.mp4").ShouldBeNull();
        exercicio.DefinirVideo(null).ShouldBe("exercicios/v.mp4");

        exercicio.FotoChave.ShouldBe("exercicios/b.jpg");
        exercicio.VideoChave.ShouldBeNull();
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
