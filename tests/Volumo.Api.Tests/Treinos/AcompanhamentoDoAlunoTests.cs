using System.Net;
using System.Net.Http.Json;
using Volumo.Api.Tests.Support;
using Volumo.Shared.Sessoes;
using Volumo.Shared.Treinos;
using static Volumo.Api.Tests.Support.ContasDeTeste;

namespace Volumo.Api.Tests.Treinos;

/// <summary>Observação do aluno nos exercícios, evolução por exercício e exportação em Excel e PDF.</summary>
public sealed class AcompanhamentoDoAlunoTests(VolumoApiFactory factory) : IClassFixture<VolumoApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Observacao_feita_na_sessao_aparece_no_treino_para_o_aluno_e_o_personal_sem_mexer_na_do_personal()
    {
        using var client = factory.CreateHttpsClient();
        var aluno = await AlunoComTreinoAsync(client);
        var inicio = DateTimeOffset.UtcNow.AddHours(-1);

        await SincronizarAsync(client, aluno.Token, Sessao(aluno, inicio, Serie(aluno.Supino, 10, 40m, inicio.AddMinutes(5))) with
        {
            Anotacoes = [new AnotacaoRegistrada(aluno.Supino, "Banco na posição 3", inicio.AddMinutes(6))],
        });

        foreach (var token in new[] { aluno.Token, aluno.TokenPersonal })
        {
            var item = (await GetAsync<TreinoDetalhe>(client, $"/treinos/{aluno.TreinoId}", token)).Itens.Single();
            item.ObservacaoDoAluno.ShouldBe("Banco na posição 3");
            item.Observacao.ShouldBe("Desça até o peito");
        }
    }

    [Fact]
    public async Task Observacao_mais_antiga_nao_sobrescreve_a_mais_nova_e_texto_vazio_limpa()
    {
        using var client = factory.CreateHttpsClient();
        var aluno = await AlunoComTreinoAsync(client);
        (await EnviarAsync(client, HttpMethod.Put, Caminho(aluno), aluno.Token, corpo: new ObservacaoDoAlunoEntrada("Pegada fechada")))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Sessão feita offline antes da mudança acima chega depois: não volta ao texto antigo.
        var antes = DateTimeOffset.UtcNow.AddHours(-2);
        await SincronizarAsync(client, aluno.Token, Sessao(aluno, antes, Serie(aluno.Supino, 10, 40m, antes.AddMinutes(5))) with
        {
            Anotacoes = [new AnotacaoRegistrada(aluno.Supino, "Pegada aberta", antes.AddMinutes(6))],
        });
        (await GetAsync<TreinoDetalhe>(client, $"/treinos/{aluno.TreinoId}", aluno.Token)).Itens.Single().ObservacaoDoAluno.ShouldBe("Pegada fechada");

        (await EnviarAsync(client, HttpMethod.Put, Caminho(aluno), aluno.Token, corpo: new ObservacaoDoAlunoEntrada("  ")))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetAsync<TreinoDetalhe>(client, $"/treinos/{aluno.TreinoId}", aluno.Token)).Itens.Single().ObservacaoDoAluno.ShouldBeNull();
    }

    [Fact]
    public async Task So_o_aluno_dono_grava_observacao_e_so_em_exercicio_do_treino()
    {
        using var client = factory.CreateHttpsClient();
        var aluno = await AlunoComTreinoAsync(client);
        var outro = await AlunoComTreinoAsync(client);
        var corpo = new ObservacaoDoAlunoEntrada("Texto");

        (await EnviarAsync(client, HttpMethod.Put, Caminho(aluno), outro.Token, corpo: corpo)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await EnviarAsync(client, HttpMethod.Put, Caminho(aluno), aluno.TokenPersonal, corpo: corpo)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await EnviarAsync(client, HttpMethod.Put, $"/treinos/{aluno.TreinoId}/exercicios/{Guid.CreateVersion7()}/observacao-do-aluno", aluno.Token, corpo: corpo))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await EnviarAsync(client, HttpMethod.Put, Caminho(aluno), aluno.Token, corpo: new ObservacaoDoAlunoEntrada(new string('x', 501))))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await EnviarAsync(client, HttpMethod.Put, Caminho(aluno), null, corpo: corpo)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Evolucao_mostra_por_dia_a_serie_mais_pesada_as_repeticoes_e_o_volume()
    {
        using var client = factory.CreateHttpsClient();
        var aluno = await AlunoComTreinoAsync(client);
        var primeira = DateTimeOffset.UtcNow.AddDays(-3);
        var segunda = DateTimeOffset.UtcNow.AddDays(-1);
        await SincronizarAsync(client, aluno.Token,
            Sessao(aluno, segunda, Serie(aluno.Supino, 8, 45m, segunda.AddMinutes(5)), Serie(aluno.Supino, 10, 40m, segunda.AddMinutes(8))),
            Sessao(aluno, primeira, Serie(aluno.Supino, 10, 40m, primeira.AddMinutes(5))));

        var evolucao = await GetAsync<EvolucaoDoTreino>(client, $"/treinos/{aluno.TreinoId}/evolucao", aluno.Token);

        var pontos = evolucao.Exercicios.Single().Pontos;
        pontos.Select(p => (p.CargaMaximaKg, p.RepeticoesNaCargaMaxima, p.Repeticoes, p.VolumeKg, p.Series))
            .ShouldBe([(40m, 10, 10, 400m, 1), (45m, 8, 18, 760m, 2)]);
        (await GetAsync<EvolucaoDoTreino>(client, $"/treinos/{aluno.TreinoId}/evolucao", aluno.TokenPersonal)).Exercicios.Single().Pontos.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData("xlsx", "PK")]
    [InlineData("pdf", "%PDF")]
    public async Task Treino_e_sessao_podem_ser_exportados(string formato, string assinatura)
    {
        using var client = factory.CreateHttpsClient();
        var aluno = await AlunoComTreinoAsync(client);
        var inicio = DateTimeOffset.UtcNow.AddHours(-1);
        var sessao = Sessao(aluno, inicio, Serie(aluno.Supino, 10, 40m, inicio.AddMinutes(5)));
        await SincronizarAsync(client, aluno.Token, sessao);

        foreach (var (caminho, token) in new[]
        {
            ($"/treinos/{aluno.TreinoId}/exportar?formato={formato}&fuso=-180", aluno.Token),
            ($"/treinos/{aluno.TreinoId}/exportar?formato={formato}", aluno.TokenPersonal),
            ($"/sessoes/{sessao.Id}/exportar?formato={formato}&fuso=-180", aluno.Token),
        })
        {
            using var response = await EnviarAsync(client, HttpMethod.Get, caminho, token);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.Content.Headers.ContentDisposition!.FileNameStar.ShouldEndWith($".{formato}");
            var bytes = await response.Content.ReadAsByteArrayAsync(Ct);
            System.Text.Encoding.ASCII.GetString(bytes, 0, assinatura.Length).ShouldBe(assinatura);
        }
    }

    [Fact]
    public async Task Exportar_dados_de_outra_pessoa_responde_404()
    {
        using var client = factory.CreateHttpsClient();
        var aluno = await AlunoComTreinoAsync(client);
        var outro = await AlunoComTreinoAsync(client);
        var inicio = DateTimeOffset.UtcNow.AddHours(-1);
        var sessao = Sessao(aluno, inicio, Serie(aluno.Supino, 10, 40m, inicio.AddMinutes(5)));
        await SincronizarAsync(client, aluno.Token, sessao);

        foreach (var token in new[] { outro.Token, outro.TokenPersonal })
        {
            (await EnviarAsync(client, HttpMethod.Get, $"/treinos/{aluno.TreinoId}/exportar?formato=pdf", token)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await EnviarAsync(client, HttpMethod.Get, $"/sessoes/{sessao.Id}/exportar?formato=xlsx", token)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await EnviarAsync(client, HttpMethod.Get, $"/treinos/{aluno.TreinoId}/evolucao", token)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }
    }

    private sealed record AlunoComTreino(Guid Id, string Token, string TokenPersonal, Guid TreinoId, Guid Supino);

    private async Task<AlunoComTreino> AlunoComTreinoAsync(HttpClient client)
    {
        var (emailAluno, tokenAluno, _, tokenPersonal) = await AlunoViaConviteAsync(client, factory);
        var alunoId = await IdDoUsuarioAsync(emailAluno);
        var supino = await ExercicioDoCatalogoAsync(client, tokenPersonal);
        var plano = Plano("Treino A", [supino]);
        plano = plano with { Itens = [plano.Itens[0] with { Observacao = "Desça até o peito" }] };
        var treinoId = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, $"/alunos/{alunoId}/treinos", tokenPersonal, corpo: plano));
        return new AlunoComTreino(alunoId, tokenAluno, tokenPersonal, treinoId, supino);
    }

    private static string Caminho(AlunoComTreino aluno) => $"/treinos/{aluno.TreinoId}/exercicios/{aluno.Supino}/observacao-do-aluno";

    private static SessaoRegistrada Sessao(AlunoComTreino aluno, DateTimeOffset inicio, params SerieRegistrada[] series) =>
        new(Guid.CreateVersion7(), aluno.TreinoId, inicio, inicio.AddMinutes(45), series);

    private static SerieRegistrada Serie(Guid exercicioId, int repeticoes, decimal carga, DateTimeOffset quando) =>
        new(Guid.CreateVersion7(), exercicioId, repeticoes, carga, Rir: 2, quando);

    private static async Task SincronizarAsync(HttpClient client, string token, params SessaoRegistrada[] sessoes)
    {
        using var response = await EnviarAsync(client, HttpMethod.Post, "/sessoes/sincronizar", token, corpo: new LoteDeSessoes(sessoes));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var resultado = (await response.Content.ReadFromJsonAsync<ResultadoDaSincronizacao>(Ct))!.Sessoes;
        resultado.ShouldAllBe(r => r.Situacao == SituacaoDaSincronizacao.Sincronizada);
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string caminho, string token)
    {
        using var response = await EnviarAsync(client, HttpMethod.Get, caminho, token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }
}
