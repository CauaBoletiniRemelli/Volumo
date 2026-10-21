using System.Net;
using System.Net.Http.Json;
using Volumo.Api.Tests.Support;
using Volumo.Shared.Alunos;
using Volumo.Shared.Convites;
using Volumo.Shared.Treinos;
using static Volumo.Api.Tests.Support.ContasDeTeste;

namespace Volumo.Api.Tests.Treinos;

/// <summary>Treinos prescritos pelo personal e o que acontece quando o vínculo acaba.</summary>
public sealed class TreinosTests(VolumoApiFactory factory) : IClassFixture<VolumoApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Personal_monta_treino_e_o_aluno_ve_na_tela_principal()
    {
        using var client = factory.CreateHttpsClient();
        var (emailAluno, tokenAluno, _, tokenPersonal) = await AlunoViaConviteAsync(client, factory);
        var alunoId = await IdDoUsuarioAsync(emailAluno);
        var supino = await ExercicioDoCatalogoAsync(client, tokenPersonal);

        var treinoId = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, $"/alunos/{alunoId}/treinos", tokenPersonal, corpo: Plano("Treino A", [supino])));

        (await GetAsync<List<TreinoResumo>>(client, "/treinos", tokenAluno)).Single().Id.ShouldBe(treinoId);
        (await GetAsync<List<TreinoResumo>>(client, $"/alunos/{alunoId}/treinos", tokenPersonal)).Single().Nome.ShouldBe("Treino A");
        var detalhe = await GetAsync<TreinoDetalhe>(client, $"/treinos/{treinoId}", tokenAluno);
        detalhe.AlunoId.ShouldBe(alunoId);
        detalhe.Itens.Single().NomeDoExercicio.ShouldBe("Supino reto com barra");
    }

    [Fact]
    public async Task Aluno_ve_o_exercicio_personalizado_do_seu_personal_no_treino()
    {
        using var client = factory.CreateHttpsClient();
        var (emailAluno, tokenAluno, _, tokenPersonal) = await AlunoViaConviteAsync(client, factory);
        using var criado = await EnviarAsync(client, HttpMethod.Post, "/exercicios", tokenPersonal, corpo: new DadosDoExercicio("Remada do personal", GrupoMuscular.Costas));
        var exercicio = (await criado.Content.ReadFromJsonAsync<ExercicioResumo>(Ct))!.Id;

        var treinoId = await IdCriadoAsync(await EnviarAsync(
            client, HttpMethod.Post, $"/alunos/{await IdDoUsuarioAsync(emailAluno)}/treinos", tokenPersonal, corpo: Plano("Costas", [exercicio])));

        (await GetAsync<TreinoDetalhe>(client, $"/treinos/{treinoId}", tokenAluno)).Itens.Single().NomeDoExercicio.ShouldBe("Remada do personal");
    }

    [Fact]
    public async Task Personal_altera_o_treino_e_o_aluno_nao_pode_alterar()
    {
        using var client = factory.CreateHttpsClient();
        var (emailAluno, tokenAluno, _, tokenPersonal) = await AlunoViaConviteAsync(client, factory);
        var supino = await ExercicioDoCatalogoAsync(client, tokenPersonal);
        var treinoId = await IdCriadoAsync(await EnviarAsync(
            client, HttpMethod.Post, $"/alunos/{await IdDoUsuarioAsync(emailAluno)}/treinos", tokenPersonal, corpo: Plano("Treino A", [supino])));

        (await EnviarAsync(client, HttpMethod.Put, $"/treinos/{treinoId}", tokenPersonal, corpo: Plano("Treino A (semana 2)", [supino], series: 4)))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await EnviarAsync(client, HttpMethod.Put, $"/treinos/{treinoId}", tokenAluno, corpo: Plano("Hackeado", [supino])))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var detalhe = await GetAsync<TreinoDetalhe>(client, $"/treinos/{treinoId}", tokenAluno);
        detalhe.Nome.ShouldBe("Treino A (semana 2)");
        detalhe.Itens.Single().Series.Count.ShouldBe(4);
    }

    [Fact]
    public async Task Treino_inativado_some_da_lista_do_aluno()
    {
        using var client = factory.CreateHttpsClient();
        var (emailAluno, tokenAluno, _, tokenPersonal) = await AlunoViaConviteAsync(client, factory);
        var supino = await ExercicioDoCatalogoAsync(client, tokenPersonal);
        var treinoId = await IdCriadoAsync(await EnviarAsync(
            client, HttpMethod.Post, $"/alunos/{await IdDoUsuarioAsync(emailAluno)}/treinos", tokenPersonal, corpo: Plano("Antigo", [supino])));

        (await EnviarAsync(client, HttpMethod.Post, $"/treinos/{treinoId}/inativar", tokenPersonal)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await GetAsync<List<TreinoResumo>>(client, "/treinos", tokenAluno)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Quando_o_personal_encerra_o_vinculo_perde_o_acesso_e_o_aluno_mantem_os_treinos()
    {
        using var client = factory.CreateHttpsClient();
        var (emailAluno, tokenAluno, _, tokenPersonal) = await AlunoViaConviteAsync(client, factory);
        var alunoId = await IdDoUsuarioAsync(emailAluno);
        var supino = await ExercicioDoCatalogoAsync(client, tokenPersonal);
        var treinoId = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, $"/alunos/{alunoId}/treinos", tokenPersonal, corpo: Plano("Treino A", [supino])));

        (await EnviarAsync(client, HttpMethod.Post, $"/alunos/{alunoId}/encerrar-vinculo", tokenPersonal)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await GetAsync<List<AlunoResumo>>(client, "/alunos", tokenPersonal)).ShouldBeEmpty();
        (await EnviarAsync(client, HttpMethod.Get, $"/treinos/{treinoId}", tokenPersonal)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await EnviarAsync(client, HttpMethod.Get, $"/alunos/{alunoId}/treinos", tokenPersonal)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await EnviarAsync(client, HttpMethod.Put, $"/treinos/{treinoId}", tokenPersonal, corpo: Plano("X", [supino]))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await GetAsync<TreinoDetalhe>(client, $"/treinos/{treinoId}", tokenAluno)).Nome.ShouldBe("Treino A");
    }

    [Fact]
    public async Task Aluno_encerra_o_vinculo_e_pode_aceitar_outro_personal()
    {
        using var client = factory.CreateHttpsClient();
        var (emailAluno, tokenAluno, _, _) = await AlunoViaConviteAsync(client, factory);
        var (_, _, tokenNovoPersonal) = await PersonalComDoisFatoresAsync(client, factory);
        (await ConvidarAsync(client, tokenNovoPersonal, emailAluno)).Dispose();

        (await EnviarAsync(client, HttpMethod.Post, "/conta/vinculo/encerrar", tokenAluno)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var aceite = await EnviarAsync(
            client, HttpMethod.Post, "/convites/aceitar", tokenAluno, corpo: new TokenDeConvite(factory.Emails.TokenDoConvite(emailAluno)));

        aceite.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetAsync<List<AlunoResumo>>(client, "/alunos", tokenNovoPersonal)).Single().Nome.ShouldBe("Aluno de Teste");
        (await EnviarAsync(client, HttpMethod.Post, "/conta/vinculo/encerrar", tokenAluno)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await EnviarAsync(client, HttpMethod.Post, "/conta/vinculo/encerrar", tokenAluno)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Matriz_de_autorizacao_dos_treinos()
    {
        using var client = factory.CreateHttpsClient();
        var (emailAluno, tokenAluno, _, tokenPersonal) = await AlunoViaConviteAsync(client, factory);
        var (_, tokenOutroAluno, _, tokenOutroPersonal) = await AlunoViaConviteAsync(client, factory);
        var alunoId = await IdDoUsuarioAsync(emailAluno);
        var supino = await ExercicioDoCatalogoAsync(client, tokenPersonal);
        var treinoId = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, $"/alunos/{alunoId}/treinos", tokenPersonal, corpo: Plano("Treino A", [supino])));
        var plano = Plano("Invasão", [supino]);

        (await EnviarAsync(client, HttpMethod.Get, $"/treinos/{treinoId}", null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await EnviarAsync(client, HttpMethod.Get, $"/treinos/{treinoId}", tokenOutroAluno)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await EnviarAsync(client, HttpMethod.Get, $"/treinos/{treinoId}", tokenOutroPersonal)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await EnviarAsync(client, HttpMethod.Put, $"/treinos/{treinoId}", tokenOutroPersonal, corpo: plano)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await EnviarAsync(client, HttpMethod.Get, $"/alunos/{alunoId}/treinos", tokenOutroPersonal)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await EnviarAsync(client, HttpMethod.Post, $"/alunos/{alunoId}/treinos", tokenOutroPersonal, corpo: plano)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await EnviarAsync(client, HttpMethod.Post, $"/alunos/{alunoId}/encerrar-vinculo", tokenOutroPersonal)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await EnviarAsync(client, HttpMethod.Get, "/alunos", tokenAluno)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await EnviarAsync(client, HttpMethod.Get, "/treinos", tokenPersonal)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Treinos_aparecem_na_ordem_de_criacao_e_o_personal_reordena_para_os_dois_lados()
    {
        using var client = factory.CreateHttpsClient();
        var (emailAluno, tokenAluno, _, tokenPersonal) = await AlunoViaConviteAsync(client, factory);
        var alunoId = await IdDoUsuarioAsync(emailAluno);
        var supino = await ExercicioDoCatalogoAsync(client, tokenPersonal);

        // Nomes fora da ordem alfabética: a lista segue a ordem de criação, não o nome.
        var c = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, $"/alunos/{alunoId}/treinos", tokenPersonal, corpo: Plano("C · Pernas", [supino])));
        var a = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, $"/alunos/{alunoId}/treinos", tokenPersonal, corpo: Plano("A · Peito", [supino])));
        var b = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, $"/alunos/{alunoId}/treinos", tokenPersonal, corpo: Plano("B · Costas", [supino])));
        (await GetAsync<List<TreinoResumo>>(client, "/treinos", tokenAluno)).Select(t => t.Id).ShouldBe([c, a, b]);

        (await EnviarAsync(client, HttpMethod.Put, $"/alunos/{alunoId}/treinos/ordem", tokenPersonal, corpo: new OrdemDosTreinos([a, b, c])))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await GetAsync<List<TreinoResumo>>(client, "/treinos", tokenAluno)).Select(t => t.Id).ShouldBe([a, b, c]);
        (await GetAsync<List<TreinoResumo>>(client, $"/alunos/{alunoId}/treinos", tokenPersonal)).Select(t => t.Id).ShouldBe([a, b, c]);

        // Treino novo (inclusive vindo de modelo) entra no fim.
        var modelo = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/templates", tokenPersonal, corpo: Plano("D · Ombros", [supino])));
        var d = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, $"/templates/{modelo}/atribuir", tokenPersonal, corpo: new AtribuicaoDeTemplate(alunoId)));
        (await GetAsync<List<TreinoResumo>>(client, "/treinos", tokenAluno)).Select(t => t.Id).ShouldBe([a, b, c, d]);
    }

    [Fact]
    public async Task Reordenar_com_lista_incompleta_repetida_ou_com_treino_arquivado_responde_400()
    {
        using var client = factory.CreateHttpsClient();
        var (emailAluno, _, _, tokenPersonal) = await AlunoViaConviteAsync(client, factory);
        var alunoId = await IdDoUsuarioAsync(emailAluno);
        var supino = await ExercicioDoCatalogoAsync(client, tokenPersonal);
        var a = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, $"/alunos/{alunoId}/treinos", tokenPersonal, corpo: Plano("A", [supino])));
        var b = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, $"/alunos/{alunoId}/treinos", tokenPersonal, corpo: Plano("B", [supino])));
        var arquivado = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, $"/alunos/{alunoId}/treinos", tokenPersonal, corpo: Plano("Velho", [supino])));
        (await EnviarAsync(client, HttpMethod.Post, $"/treinos/{arquivado}/inativar", tokenPersonal)).Dispose();
        var rota = $"/alunos/{alunoId}/treinos/ordem";

        (await EnviarAsync(client, HttpMethod.Put, rota, tokenPersonal, corpo: new OrdemDosTreinos([a]))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await EnviarAsync(client, HttpMethod.Put, rota, tokenPersonal, corpo: new OrdemDosTreinos([a, a]))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await EnviarAsync(client, HttpMethod.Put, rota, tokenPersonal, corpo: new OrdemDosTreinos([b, a, arquivado]))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await EnviarAsync(client, HttpMethod.Put, rota, tokenPersonal, corpo: new OrdemDosTreinos([b, Guid.CreateVersion7()]))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await EnviarAsync(client, HttpMethod.Put, rota, tokenPersonal, corpo: new OrdemDosTreinos([b, a]))).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task So_o_personal_do_aluno_reordena()
    {
        using var client = factory.CreateHttpsClient();
        var (emailAluno, tokenAluno, _, tokenPersonal) = await AlunoViaConviteAsync(client, factory);
        var (_, _, tokenOutroPersonal) = await PersonalComDoisFatoresAsync(client, factory);
        var alunoId = await IdDoUsuarioAsync(emailAluno);
        var a = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, $"/alunos/{alunoId}/treinos", tokenPersonal, corpo: Plano("A", [await ExercicioDoCatalogoAsync(client, tokenPersonal)])));
        var rota = $"/alunos/{alunoId}/treinos/ordem";
        var corpo = new OrdemDosTreinos([a]);

        (await EnviarAsync(client, HttpMethod.Put, rota, null, corpo: corpo)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await EnviarAsync(client, HttpMethod.Put, rota, tokenAluno, corpo: corpo)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await EnviarAsync(client, HttpMethod.Put, rota, tokenOutroPersonal, corpo: corpo)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string caminho, string token)
    {
        using var response = await EnviarAsync(client, HttpMethod.Get, caminho, token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }
}
