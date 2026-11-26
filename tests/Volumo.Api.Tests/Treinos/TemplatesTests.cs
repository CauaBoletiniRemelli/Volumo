using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Volumo.Api.Tests.Support;
using Volumo.Shared.Treinos;
using static Volumo.Api.Tests.Support.ContasDeTeste;

namespace Volumo.Api.Tests.Treinos;

public sealed class TemplatesTests(VolumoApiFactory factory) : IClassFixture<VolumoApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Template_criado_volta_com_exercicios_series_e_nomes_na_ordem()
    {
        using var client = factory.CreateHttpsClient();
        var (_, _, token) = await PersonalComDoisFatoresAsync(client, factory);
        var supino = await ExercicioDoCatalogoAsync(client, token);
        var crucifixo = await ExercicioDoCatalogoAsync(client, token, "Crucifixo com halteres");

        var id = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/templates", token, corpo: Plano("Peito A", [supino, crucifixo], series: 4, carga: 22.5m)));

        var detalhe = await GetAsync<TemplateDetalhe>(client, $"/templates/{id}", token);
        detalhe.Nome.ShouldBe("Peito A");
        detalhe.Itens.Select(i => (i.Ordem, i.NomeDoExercicio)).ShouldBe([(1, "Supino reto com barra"), (2, "Crucifixo com halteres")]);
        detalhe.Itens[0].Series.Count.ShouldBe(4);
        detalhe.Itens[0].Series[0].CargaKg.ShouldBe(22.5m);
        (await GetAsync<List<TemplateResumo>>(client, "/templates", token)).Single().QuantidadeDeExercicios.ShouldBe(2);
    }

    [Fact]
    public async Task Exercicio_de_outro_personal_ou_inexistente_e_serie_invalida_respondem_400()
    {
        using var client = factory.CreateHttpsClient();
        var (_, _, token) = await PersonalComDoisFatoresAsync(client, factory);
        var (_, _, tokenOutro) = await PersonalComDoisFatoresAsync(client, factory);
        using var doOutro = await EnviarAsync(client, HttpMethod.Post, "/exercicios", tokenOutro, corpo: new DadosDoExercicio("Exclusivo", GrupoMuscular.Costas));
        var exercicioDoOutro = (await doOutro.Content.ReadFromJsonAsync<ExercicioResumo>(Ct))!.Id;
        var supino = await ExercicioDoCatalogoAsync(client, token);
        var serieInvalida = new PlanoDeTreinoEntrada("Ruim", [new ItemDoPlano(supino, [new SeriePrescrita(TipoDeSerie.Trabalho, 12, 8, 60, null)], null)]);

        using var r1 = await EnviarAsync(client, HttpMethod.Post, "/templates", token, corpo: Plano("X", [exercicioDoOutro]));
        using var r2 = await EnviarAsync(client, HttpMethod.Post, "/templates", token, corpo: Plano("X", [Guid.CreateVersion7()]));
        using var r3 = await EnviarAsync(client, HttpMethod.Post, "/templates", token, corpo: serieInvalida);
        using var r4 = await EnviarAsync(client, HttpMethod.Post, "/templates", token, corpo: Plano("X", [supino], series: 0));

        r1.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        r2.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        r3.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await r3.Content.ReadFromJsonAsync<ProblemDetails>(Ct))!.Detail!.ShouldContain("Repetições máximas");
        r4.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Atualizar_substitui_nome_e_exercicios()
    {
        using var client = factory.CreateHttpsClient();
        var (_, _, token) = await PersonalComDoisFatoresAsync(client, factory);
        var supino = await ExercicioDoCatalogoAsync(client, token);
        var agachamento = await ExercicioDoCatalogoAsync(client, token, "Agachamento livre");
        var id = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/templates", token, corpo: Plano("Antes", [supino])));

        (await EnviarAsync(client, HttpMethod.Put, $"/templates/{id}", token, corpo: Plano("Depois", [agachamento, supino], series: 5)))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var detalhe = await GetAsync<TemplateDetalhe>(client, $"/templates/{id}", token);
        detalhe.Nome.ShouldBe("Depois");
        detalhe.Itens.Select(i => i.ExercicioId).ShouldBe([agachamento, supino]);
        detalhe.Itens.ShouldAllBe(i => i.Series.Count == 5);
    }

    [Fact]
    public async Task Atribuir_gera_treino_independente_para_o_aluno()
    {
        using var client = factory.CreateHttpsClient();
        var (emailAluno, tokenAluno, _, tokenPersonal) = await AlunoViaConviteAsync(client, factory);
        var alunoId = await IdDoUsuarioAsync(emailAluno);
        var supino = await ExercicioDoCatalogoAsync(client, tokenPersonal);
        var templateId = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/templates", tokenPersonal, corpo: Plano("Iniciante", [supino], carga: 10m)));

        var treinoId = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, $"/templates/{templateId}/atribuir", tokenPersonal, corpo: new AtribuicaoDeTemplate(alunoId)));
        (await EnviarAsync(client, HttpMethod.Put, $"/templates/{templateId}", tokenPersonal, corpo: Plano("Iniciante v2", [supino], carga: 50m))).Dispose();

        var treinos = await GetAsync<List<TreinoResumo>>(client, "/treinos", tokenAluno);
        treinos.Single().TemplateOrigemId.ShouldBe(templateId);
        var treino = await GetAsync<TreinoDetalhe>(client, $"/treinos/{treinoId}", tokenAluno);
        treino.Nome.ShouldBe("Iniciante");
        treino.Itens.Single().Series[0].CargaKg.ShouldBe(10m);
    }

    [Fact]
    public async Task Template_inativado_some_e_nao_pode_ser_atribuido()
    {
        using var client = factory.CreateHttpsClient();
        var (emailAluno, _, _, tokenPersonal) = await AlunoViaConviteAsync(client, factory);
        var supino = await ExercicioDoCatalogoAsync(client, tokenPersonal);
        var id = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/templates", tokenPersonal, corpo: Plano("Velho", [supino])));

        (await EnviarAsync(client, HttpMethod.Post, $"/templates/{id}/inativar", tokenPersonal)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await GetAsync<List<TemplateResumo>>(client, "/templates", tokenPersonal)).ShouldBeEmpty();
        (await EnviarAsync(client, HttpMethod.Post, $"/templates/{id}/atribuir", tokenPersonal, corpo: new AtribuicaoDeTemplate(await IdDoUsuarioAsync(emailAluno))))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Matriz_de_autorizacao_dos_templates()
    {
        using var client = factory.CreateHttpsClient();
        var (_, _, tokenDono) = await PersonalComDoisFatoresAsync(client, factory);
        var (emailAlunoDoOutro, tokenAluno, _, tokenOutroPersonal) = await AlunoViaConviteAsync(client, factory);
        var supino = await ExercicioDoCatalogoAsync(client, tokenDono);
        var id = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/templates", tokenDono, corpo: Plano("Do dono", [supino])));
        var plano = Plano("Invasão", [supino]);

        (await EnviarAsync(client, HttpMethod.Get, $"/templates/{id}", null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await EnviarAsync(client, HttpMethod.Get, $"/templates/{id}", tokenAluno)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await EnviarAsync(client, HttpMethod.Get, $"/templates/{id}", tokenOutroPersonal)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await EnviarAsync(client, HttpMethod.Put, $"/templates/{id}", tokenOutroPersonal, corpo: plano)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await EnviarAsync(client, HttpMethod.Post, $"/templates/{id}/inativar", tokenOutroPersonal)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // O dono não consegue atribuir a aluno de outro personal.
        (await EnviarAsync(client, HttpMethod.Post, $"/templates/{id}/atribuir", tokenDono, corpo: new AtribuicaoDeTemplate(await IdDoUsuarioAsync(emailAlunoDoOutro))))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await EnviarAsync(client, HttpMethod.Get, $"/templates/{id}", tokenDono)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Template_com_cardio_volta_separado_da_musculacao_e_vai_para_o_treino_do_aluno()
    {
        using var client = factory.CreateHttpsClient();
        var (emailAluno, tokenAluno, _, tokenPersonal) = await AlunoViaConviteAsync(client, factory);
        var supino = await ExercicioDoCatalogoAsync(client, tokenPersonal);
        var esteira = await ExercicioDoCatalogoAsync(client, tokenPersonal, "Esteira");
        var plano = Plano("Peito + cardio", [supino]) with
        {
            Cardios = [new CardioPrescrito(esteira, 20, "Zona 2, inclinação 5%")],
            MomentoDoCardio = MomentoDoCardio.Antes,
        };

        var templateId = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/templates", tokenPersonal, corpo: plano));
        var treinoId = await IdCriadoAsync(await EnviarAsync(
            client, HttpMethod.Post, $"/templates/{templateId}/atribuir", tokenPersonal, corpo: new AtribuicaoDeTemplate(await IdDoUsuarioAsync(emailAluno))));

        var template = await GetAsync<TemplateDetalhe>(client, $"/templates/{templateId}", tokenPersonal);
        template.Itens.Single().ExercicioId.ShouldBe(supino);
        var cardio = template.Cardios!.Single();
        (cardio.NomeDoExercicio, cardio.DuracaoMinutos, cardio.Observacao).ShouldBe(("Esteira", 20, "Zona 2, inclinação 5%"));
        template.MomentoDoCardio.ShouldBe(MomentoDoCardio.Antes);

        var treino = await GetAsync<TreinoDetalhe>(client, $"/treinos/{treinoId}", tokenAluno);
        treino.Cardios!.Single().ExercicioId.ShouldBe(esteira);
        treino.MomentoDoCardio.ShouldBe(MomentoDoCardio.Antes);
    }

    [Fact]
    public async Task Sem_cardio_o_plano_fica_sem_cardio_e_recomenda_depois()
    {
        using var client = factory.CreateHttpsClient();
        var (_, _, token) = await PersonalComDoisFatoresAsync(client, factory);
        var id = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/templates", token, corpo: Plano("Só musculação", [await ExercicioDoCatalogoAsync(client, token)])));

        var detalhe = await GetAsync<TemplateDetalhe>(client, $"/templates/{id}", token);

        detalhe.Cardios.ShouldBeEmpty();
        detalhe.MomentoDoCardio.ShouldBe(MomentoDoCardio.Depois);
    }

    [Fact]
    public async Task Cardio_com_exercicio_que_nao_e_de_cardio_ou_duracao_invalida_responde_400()
    {
        using var client = factory.CreateHttpsClient();
        var (_, _, token) = await PersonalComDoisFatoresAsync(client, factory);
        var supino = await ExercicioDoCatalogoAsync(client, token);
        var esteira = await ExercicioDoCatalogoAsync(client, token, "Esteira");

        using var naoECardio = await EnviarAsync(client, HttpMethod.Post, "/templates", token,
            corpo: Plano("X", [supino]) with { Cardios = [new CardioPrescrito(supino, 10, null)] });
        using var longoDemais = await EnviarAsync(client, HttpMethod.Post, "/templates", token,
            corpo: Plano("X", [supino]) with { Cardios = [new CardioPrescrito(esteira, 181, null)] });
        using var momentoInvalido = await EnviarAsync(client, HttpMethod.Post, "/templates", token,
            corpo: Plano("X", [supino]) with { MomentoDoCardio = (MomentoDoCardio)99 });

        using var cardioNaMusculacao = await EnviarAsync(client, HttpMethod.Post, "/templates", token, corpo: Plano("X", [esteira]));
        cardioNaMusculacao.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await cardioNaMusculacao.Content.ReadFromJsonAsync<ProblemDetails>(Ct))!.Detail!.ShouldContain("aba de cardio");

        naoECardio.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await naoECardio.Content.ReadFromJsonAsync<ProblemDetails>(Ct))!.Detail!.ShouldContain("grupo Cardio");
        longoDemais.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        momentoInvalido.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string caminho, string token)
    {
        using var response = await EnviarAsync(client, HttpMethod.Get, caminho, token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }
}
