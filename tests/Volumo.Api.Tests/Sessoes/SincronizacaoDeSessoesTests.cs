using System.Net;
using System.Net.Http.Json;
using Volumo.Api.Tests.Support;
using Volumo.Shared.Sessoes;
using Volumo.Shared.Treinos;
using static Volumo.Api.Tests.Support.ContasDeTeste;

namespace Volumo.Api.Tests.Sessoes;

/// <summary>Sessões registradas no app (inclusive offline) e enviadas em lote.</summary>
public sealed class SincronizacaoDeSessoesTests(VolumoApiFactory factory) : IClassFixture<VolumoApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sessao_registrada_offline_e_gravada_e_aparece_no_historico()
    {
        using var client = factory.CreateHttpsClient();
        var aluno = await AlunoComTreinoAsync(client);
        var inicio = DateTimeOffset.UtcNow.AddHours(-2);
        var sessao = Sessao(aluno.TreinoId, inicio, finalizada: inicio.AddMinutes(50), Serie(aluno.Supino, 10, 40m, inicio.AddMinutes(5)), Serie(aluno.Supino, 8, 42.5m, inicio.AddMinutes(8)));

        var resultado = await SincronizarAsync(client, aluno.Token, sessao);

        resultado.Single().Situacao.ShouldBe(SituacaoDaSincronizacao.Sincronizada);
        var historico = await GetAsync<List<SessaoResumo>>(client, "/sessoes", aluno.Token);
        var resumo = historico.Single();
        resumo.NomeDoTreino.ShouldBe("Treino A");
        resumo.QuantidadeDeSeries.ShouldBe(2);
        resumo.VolumeKg.ShouldBe((10 * 40m) + (8 * 42.5m));
        var detalhe = await GetAsync<SessaoDetalhe>(client, $"/sessoes/{sessao.Id}", aluno.Token);
        detalhe.Series.Select(s => (s.NomeDoExercicio, s.Repeticoes)).ShouldBe([("Supino reto com barra", 10), ("Supino reto com barra", 8)]);
    }

    [Fact]
    public async Task Reenviar_o_mesmo_lote_nao_duplica_nada()
    {
        using var client = factory.CreateHttpsClient();
        var aluno = await AlunoComTreinoAsync(client);
        var inicio = DateTimeOffset.UtcNow.AddHours(-1);
        var sessao = Sessao(aluno.TreinoId, inicio, finalizada: inicio.AddMinutes(40), Serie(aluno.Supino, 10, 40m, inicio.AddMinutes(5)));

        await SincronizarAsync(client, aluno.Token, sessao);
        var reenvio = await SincronizarAsync(client, aluno.Token, sessao);

        reenvio.Single().Situacao.ShouldBe(SituacaoDaSincronizacao.Sincronizada);
        (await GetAsync<List<SessaoResumo>>(client, "/sessoes", aluno.Token)).Single().QuantidadeDeSeries.ShouldBe(1);
    }

    [Fact]
    public async Task Sessao_pode_chegar_aos_poucos_e_ser_finalizada_depois()
    {
        using var client = factory.CreateHttpsClient();
        var aluno = await AlunoComTreinoAsync(client);
        var inicio = DateTimeOffset.UtcNow.AddMinutes(-30);
        var primeiraSerie = Serie(aluno.Supino, 12, 30m, inicio.AddMinutes(3));
        var id = Guid.CreateVersion7();

        await SincronizarAsync(client, aluno.Token, new SessaoRegistrada(id, aluno.TreinoId, inicio, null, [primeiraSerie]));
        await SincronizarAsync(client, aluno.Token, new SessaoRegistrada(
            id, aluno.TreinoId, inicio, inicio.AddMinutes(25), [primeiraSerie, Serie(aluno.Supino, 10, 32.5m, inicio.AddMinutes(6))]));

        var detalhe = await GetAsync<SessaoDetalhe>(client, $"/sessoes/{id}", aluno.Token);
        detalhe.Series.Count.ShouldBe(2);
        detalhe.FinalizadaEm.ShouldBe(inicio.AddMinutes(25));
    }

    [Fact]
    public async Task Cada_sessao_do_lote_e_independente()
    {
        using var client = factory.CreateHttpsClient();
        var aluno = await AlunoComTreinoAsync(client);
        var inicio = DateTimeOffset.UtcNow.AddHours(-3);
        var valida = Sessao(aluno.TreinoId, inicio, inicio.AddMinutes(30), Serie(aluno.Supino, 10, 40m, inicio.AddMinutes(5)));
        var noFuturo = Sessao(aluno.TreinoId, DateTimeOffset.UtcNow.AddHours(2), null);

        using var response = await EnviarAsync(client, HttpMethod.Post, "/sessoes/sincronizar", aluno.Token, corpo: new LoteDeSessoes([valida, noFuturo]));
        var resultado = (await response.Content.ReadFromJsonAsync<ResultadoDaSincronizacao>(Ct))!.Sessoes;

        resultado.Select(r => r.Situacao).ShouldBe([SituacaoDaSincronizacao.Sincronizada, SituacaoDaSincronizacao.Recusada]);
        resultado[1].Motivo!.ShouldContain("futuro");
        (await GetAsync<List<SessaoResumo>>(client, "/sessoes", aluno.Token)).Single().Id.ShouldBe(valida.Id);
    }

    [Fact]
    public async Task Dados_invalidos_ou_de_outra_pessoa_sao_recusados_sem_gravar()
    {
        using var client = factory.CreateHttpsClient();
        var aluno = await AlunoComTreinoAsync(client);
        var outro = await AlunoComTreinoAsync(client);
        var inicio = DateTimeOffset.UtcNow.AddHours(-1);
        using var exclusivo = await EnviarAsync(client, HttpMethod.Post, "/exercicios", outro.TokenPersonal, corpo: new DadosDoExercicio("Do outro personal", GrupoMuscular.Costas));
        var exercicioDoOutro = (await exclusivo.Content.ReadFromJsonAsync<ExercicioResumo>(Ct))!.Id;
        var sessaoDoOutro = Sessao(outro.TreinoId, inicio, null, Serie(outro.Supino, 10, 40m, inicio.AddMinutes(5)));
        await SincronizarAsync(client, outro.Token, sessaoDoOutro);

        var resultados = await SincronizarAsync(
            client,
            aluno.Token,
            Sessao(outro.TreinoId, inicio, null),                                                  // treino de outro aluno
            Sessao(aluno.TreinoId, inicio, null, Serie(exercicioDoOutro, 10, 40m, inicio)),        // exercício que não vê
            sessaoDoOutro with { Series = [Serie(aluno.Supino, 1, 1m, inicio.AddMinutes(9))] },    // Id de sessão de outra pessoa
            new SessaoRegistrada(Guid.NewGuid(), aluno.TreinoId, inicio, null, []),                // Id que não é GUID v7
            Sessao(aluno.TreinoId, inicio, null, Serie(aluno.Supino, 10, 40m, inicio.AddMinutes(-5)))); // série antes do início

        resultados.ShouldAllBe(r => r.Situacao == SituacaoDaSincronizacao.Recusada);
        (await GetAsync<List<SessaoResumo>>(client, "/sessoes", aluno.Token)).ShouldBeEmpty();
        (await GetAsync<SessaoDetalhe>(client, $"/sessoes/{sessaoDoOutro.Id}", outro.Token)).Series.Count.ShouldBe(1, "a sessão do outro aluno não mudou");
    }

    [Fact]
    public async Task Personal_ve_as_sessoes_dos_seus_alunos_e_de_mais_ninguem()
    {
        using var client = factory.CreateHttpsClient();
        var aluno = await AlunoComTreinoAsync(client);
        var (_, _, tokenOutroPersonal) = await PersonalComDoisFatoresAsync(client, factory);
        var inicio = DateTimeOffset.UtcNow.AddHours(-1);
        var sessao = Sessao(aluno.TreinoId, inicio, inicio.AddMinutes(30), Serie(aluno.Supino, 10, 40m, inicio.AddMinutes(5)));
        await SincronizarAsync(client, aluno.Token, sessao);

        (await GetAsync<List<SessaoResumo>>(client, $"/alunos/{aluno.Id}/sessoes", aluno.TokenPersonal)).Single().Id.ShouldBe(sessao.Id);
        (await GetAsync<SessaoDetalhe>(client, $"/sessoes/{sessao.Id}", aluno.TokenPersonal)).AlunoId.ShouldBe(aluno.Id);
        (await EnviarAsync(client, HttpMethod.Get, $"/sessoes/{sessao.Id}", tokenOutroPersonal)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await EnviarAsync(client, HttpMethod.Get, $"/alunos/{aluno.Id}/sessoes", tokenOutroPersonal)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Matriz_de_autorizacao_das_sessoes()
    {
        using var client = factory.CreateHttpsClient();
        var aluno = await AlunoComTreinoAsync(client);
        var lote = new LoteDeSessoes([Sessao(aluno.TreinoId, DateTimeOffset.UtcNow.AddMinutes(-10), null)]);

        (await EnviarAsync(client, HttpMethod.Post, "/sessoes/sincronizar", null, corpo: lote)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await EnviarAsync(client, HttpMethod.Post, "/sessoes/sincronizar", aluno.TokenPersonal, corpo: lote)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await EnviarAsync(client, HttpMethod.Get, "/sessoes", aluno.TokenPersonal)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await EnviarAsync(client, HttpMethod.Get, $"/sessoes/{Guid.CreateVersion7()}", aluno.Token)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var grande = new LoteDeSessoes([.. Enumerable.Range(0, 21).Select(_ => Sessao(aluno.TreinoId, DateTimeOffset.UtcNow.AddMinutes(-10), null))]);
        (await EnviarAsync(client, HttpMethod.Post, "/sessoes/sincronizar", aluno.Token, corpo: grande)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private sealed record AlunoComTreino(Guid Id, string Token, string TokenPersonal, Guid TreinoId, Guid Supino);

    private async Task<AlunoComTreino> AlunoComTreinoAsync(HttpClient client)
    {
        var (emailAluno, tokenAluno, _, tokenPersonal) = await AlunoViaConviteAsync(client, factory);
        var alunoId = await IdDoUsuarioAsync(emailAluno);
        var supino = await ExercicioDoCatalogoAsync(client, tokenPersonal);
        var treinoId = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, $"/alunos/{alunoId}/treinos", tokenPersonal, corpo: Plano("Treino A", [supino])));
        return new AlunoComTreino(alunoId, tokenAluno, tokenPersonal, treinoId, supino);
    }

    private static SessaoRegistrada Sessao(Guid treinoId, DateTimeOffset inicio, DateTimeOffset? finalizada, params SerieRegistrada[] series) =>
        new(Guid.CreateVersion7(), treinoId, inicio, finalizada, series);

    private static SerieRegistrada Serie(Guid exercicioId, int repeticoes, decimal carga, DateTimeOffset quando) =>
        new(Guid.CreateVersion7(), exercicioId, repeticoes, carga, Rir: 2, quando);

    private static async Task<IReadOnlyList<ResultadoDaSessao>> SincronizarAsync(HttpClient client, string token, params SessaoRegistrada[] sessoes)
    {
        using var response = await EnviarAsync(client, HttpMethod.Post, "/sessoes/sincronizar", token, corpo: new LoteDeSessoes(sessoes));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ResultadoDaSincronizacao>(Ct))!.Sessoes;
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string caminho, string token)
    {
        using var response = await EnviarAsync(client, HttpMethod.Get, caminho, token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }
}
