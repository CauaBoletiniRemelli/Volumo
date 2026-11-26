using System.Net;
using System.Net.Http.Json;
using Volumo.Api.Tests.Support;
using Volumo.Shared.Treinos;
using static Volumo.Api.Tests.Support.ContasDeTeste;

namespace Volumo.Api.Tests.Treinos;

public sealed class ExerciciosTests(VolumoApiFactory factory) : IClassFixture<VolumoApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Catalogo_semeado_aparece_para_personal_e_aluno_com_busca_e_filtro_por_grupo()
    {
        using var client = factory.CreateHttpsClient();
        var (_, tokenAluno, _, tokenPersonal) = await AlunoViaConviteAsync(client, factory);

        var todos = await ListarAsync(client, tokenPersonal);
        var busca = await ListarAsync(client, tokenAluno, "?busca=supino");
        var peitoral = await ListarAsync(client, tokenAluno, "?grupo=Peitoral");

        todos.Count(e => e.DoCatalogo).ShouldBeGreaterThanOrEqualTo(40);
        busca.Select(e => e.Nome).ShouldContain("Supino reto com barra");
        busca.ShouldAllBe(e => e.Nome.Contains("upino"));
        peitoral.ShouldNotBeEmpty();
        peitoral.ShouldAllBe(e => e.GrupoMuscular == GrupoMuscular.Peitoral);
    }

    [Fact]
    public async Task Exercicio_criado_pelo_personal_aparece_para_ele_e_seus_alunos_e_nao_para_outros()
    {
        using var client = factory.CreateHttpsClient();
        var (_, tokenAluno, _, tokenPersonal) = await AlunoViaConviteAsync(client, factory);
        var (_, _, tokenOutroPersonal) = await PersonalComDoisFatoresAsync(client, factory);
        var nome = $"Remada exclusiva {Guid.NewGuid():N}";

        using var criado = await EnviarAsync(client, HttpMethod.Post, "/exercicios", tokenPersonal, corpo: new DadosDoExercicio(nome, GrupoMuscular.Costas));

        criado.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await ListarAsync(client, tokenPersonal, $"?busca={nome}")).Single().DoCatalogo.ShouldBeFalse();
        (await ListarAsync(client, tokenAluno, $"?busca={nome}")).Count.ShouldBe(1);
        (await ListarAsync(client, tokenOutroPersonal, $"?busca={nome}")).ShouldBeEmpty();
    }

    [Fact]
    public async Task So_o_dono_altera_ou_inativa_e_o_catalogo_nao_muda()
    {
        using var client = factory.CreateHttpsClient();
        var (_, _, token) = await PersonalComDoisFatoresAsync(client, factory);
        var (_, _, tokenOutro) = await PersonalComDoisFatoresAsync(client, factory);
        var nome = $"Agachamento búlgaro {Guid.NewGuid():N}";
        using var criado = await EnviarAsync(client, HttpMethod.Post, "/exercicios", token, corpo: new DadosDoExercicio(nome, GrupoMuscular.Quadriceps));
        var id = (await criado.Content.ReadFromJsonAsync<ExercicioResumo>(Ct))!.Id;
        var catalogo = await ExercicioDoCatalogoAsync(client, token);
        var novosDados = new DadosDoExercicio(nome + " (unilateral)", GrupoMuscular.Gluteos);

        (await EnviarAsync(client, HttpMethod.Put, $"/exercicios/{catalogo}", token, corpo: novosDados)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await EnviarAsync(client, HttpMethod.Put, $"/exercicios/{id}", tokenOutro, corpo: novosDados)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await EnviarAsync(client, HttpMethod.Put, $"/exercicios/{id}", token, corpo: novosDados)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ListarAsync(client, token, $"?busca={nome}")).Single().GrupoMuscular.ShouldBe(GrupoMuscular.Gluteos);

        (await EnviarAsync(client, HttpMethod.Post, $"/exercicios/{id}/inativar", token)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ListarAsync(client, token, $"?busca={nome}")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Matriz_de_autorizacao_dos_exercicios()
    {
        using var client = factory.CreateHttpsClient();
        var (_, tokenAluno, _, _) = await AlunoViaConviteAsync(client, factory);
        var dados = new DadosDoExercicio("Qualquer", GrupoMuscular.Ombros);

        (await EnviarAsync(client, HttpMethod.Get, "/exercicios", null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await EnviarAsync(client, HttpMethod.Post, "/exercicios", tokenAluno, corpo: dados)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await EnviarAsync(client, HttpMethod.Post, "/exercicios", null, corpo: dados)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static async Task<List<ExercicioResumo>> ListarAsync(HttpClient client, string token, string query = "")
    {
        using var response = await EnviarAsync(client, HttpMethod.Get, "/exercicios" + query, token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<List<ExercicioResumo>>(Ct))!;
    }
}
