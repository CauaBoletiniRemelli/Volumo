using System.Net;
using System.Net.Http.Json;
using Volumo.Api.Tests.Support;
using Volumo.Shared.Autenticacao;
using Volumo.Shared.Painel;
using Volumo.Shared.Sessoes;
using static Volumo.Api.Tests.Support.ContasDeTeste;

namespace Volumo.Api.Tests.Painel;

/// <summary>Painel do personal: quem treinou, quem sumiu e quem estagnou.</summary>
public sealed class PainelTests(VolumoApiFactory factory) : IClassFixture<VolumoApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Mostra_quem_esta_em_dia_quem_sumiu_e_quem_nunca_treinou()
    {
        using var client = factory.CreateHttpsClient();
        var turma = await TurmaAsync(client, alunos: 3);
        var agora = DateTimeOffset.UtcNow;
        await TreinarAsync(client, turma.Alunos[0], agora.AddHours(-2), 40m);
        await TreinarAsync(client, turma.Alunos[1], agora.AddDays(-6).AddHours(-1), 40m);

        var painel = await PainelAsync(client, turma.TokenPersonal);

        painel.DiasParaSumido.ShouldBe(5);
        var emDia = painel.Alunos.Single(a => a.Id == turma.Alunos[0].Id);
        emDia.Situacao.ShouldBe(SituacaoDoAluno.EmDia);
        emDia.NomeDoUltimoTreino.ShouldBe("Treino A");
        emDia.TreinosNosUltimos7Dias.ShouldBe(1);
        var sumido = painel.Alunos.Single(a => a.Id == turma.Alunos[1].Id);
        sumido.Situacao.ShouldBe(SituacaoDoAluno.Sumido);
        sumido.DiasSemTreinar.ShouldBe(6);
        painel.Alunos.Single(a => a.Id == turma.Alunos[2].Id).Situacao.ShouldBe(SituacaoDoAluno.NuncaTreinou);
        painel.Alunos.ShouldAllBe(a => a.Estagnacao == new ConfiguracaoDeEstagnacao(CriterioDeEstagnacao.Tempo, 15));
    }

    [Fact]
    public async Task Estagnacao_segue_o_criterio_que_o_personal_escolheu_para_o_aluno()
    {
        using var client = factory.CreateHttpsClient();
        var turma = await TurmaAsync(client, alunos: 1);
        var aluno = turma.Alunos[0];
        var agora = DateTimeOffset.UtcNow;
        await TreinarAsync(client, aluno, agora.AddDays(-20), 40m);
        await TreinarAsync(client, aluno, agora.AddDays(-12), 40m);
        await TreinarAsync(client, aluno, agora.AddDays(-3), 40m);

        // Padrão: 15 dias sem subir peso.
        var estagnado = (await AlunoNoPainelAsync(client, turma.TokenPersonal, aluno.Id)).Estagnados.Single();
        estagnado.NomeDoExercicio.ShouldBe("Supino reto com barra");
        estagnado.MaiorCargaKg.ShouldBe(40m);
        estagnado.SessoesDesdeOAumento.ShouldBe(2);

        await ConfigurarAsync(client, turma.TokenPersonal, aluno.Id, CriterioDeEstagnacao.Sessoes, 3, HttpStatusCode.NoContent);
        (await AlunoNoPainelAsync(client, turma.TokenPersonal, aluno.Id)).Estagnados.ShouldBeEmpty();

        await ConfigurarAsync(client, turma.TokenPersonal, aluno.Id, CriterioDeEstagnacao.Sessoes, 2, HttpStatusCode.NoContent);
        var configurado = await AlunoNoPainelAsync(client, turma.TokenPersonal, aluno.Id);
        configurado.Estagnacao.ShouldBe(new ConfiguracaoDeEstagnacao(CriterioDeEstagnacao.Sessoes, 2));
        configurado.Estagnados.Count.ShouldBe(1);

        // Subiu a carga: sai do alerta.
        await TreinarAsync(client, aluno, agora.AddHours(-1), 42.5m);
        (await AlunoNoPainelAsync(client, turma.TokenPersonal, aluno.Id)).Estagnados.ShouldBeEmpty();
    }

    [Fact]
    public async Task Personal_so_ve_os_proprios_alunos_com_vinculo_ativo()
    {
        using var client = factory.CreateHttpsClient();
        var turma = await TurmaAsync(client, alunos: 2);
        var outra = await TurmaAsync(client, alunos: 1);

        (await PainelAsync(client, outra.TokenPersonal)).Alunos.Select(a => a.Id).ShouldBe([outra.Alunos[0].Id]);

        using var encerrar = await EnviarAsync(client, HttpMethod.Post, $"/alunos/{turma.Alunos[0].Id}/encerrar-vinculo", turma.TokenPersonal);
        encerrar.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await PainelAsync(client, turma.TokenPersonal)).Alunos.Select(a => a.Id).ShouldBe([turma.Alunos[1].Id]);
    }

    [Fact]
    public async Task Matriz_de_autorizacao_do_painel()
    {
        using var client = factory.CreateHttpsClient();
        var turma = await TurmaAsync(client, alunos: 1);
        var outra = await TurmaAsync(client, alunos: 1);
        var (tokenSemDoisFatores, _) = await EntrarAsync(client, await PersonalConfirmadoAsync(client, factory));
        var aluno = turma.Alunos[0];

        (await EnviarAsync(client, HttpMethod.Get, "/painel", null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await EnviarAsync(client, HttpMethod.Get, "/painel", aluno.Token)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await EnviarAsync(client, HttpMethod.Get, "/painel", tokenSemDoisFatores)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var config = new ConfiguracaoDeEstagnacao(CriterioDeEstagnacao.Tempo, 30);
        (await EnviarAsync(client, HttpMethod.Put, $"/alunos/{aluno.Id}/estagnacao", null, corpo: config)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await EnviarAsync(client, HttpMethod.Put, $"/alunos/{aluno.Id}/estagnacao", aluno.Token, corpo: config)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await EnviarAsync(client, HttpMethod.Put, $"/alunos/{aluno.Id}/estagnacao", outra.TokenPersonal, corpo: config)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ConfigurarAsync(client, turma.TokenPersonal, aluno.Id, CriterioDeEstagnacao.Sessoes, 30, HttpStatusCode.BadRequest);
        await ConfigurarAsync(client, turma.TokenPersonal, aluno.Id, CriterioDeEstagnacao.Tempo, 3, HttpStatusCode.BadRequest);
        (await AlunoNoPainelAsync(client, turma.TokenPersonal, aluno.Id)).Estagnacao.ShouldBe(new ConfiguracaoDeEstagnacao(CriterioDeEstagnacao.Tempo, 15));
    }

    private sealed record Aluno(Guid Id, string Token, Guid TreinoId, Guid Supino);

    private sealed record Turma(string TokenPersonal, IReadOnlyList<Aluno> Alunos);

    /// <summary>Um personal com 2FA e <paramref name="alunos"/> alunos, cada um com o "Treino A" (supino).</summary>
    private async Task<Turma> TurmaAsync(HttpClient client, int alunos)
    {
        var (_, _, tokenPersonal) = await PersonalComDoisFatoresAsync(client, factory);
        var supino = await ExercicioDoCatalogoAsync(client, tokenPersonal);
        var lista = new List<Aluno>();
        for (var i = 0; i < alunos; i++)
        {
            var email = NovoEmail("aluno");
            (await ConvidarAsync(client, tokenPersonal, email)).StatusCode.ShouldBe(HttpStatusCode.Created);
            using var cadastro = await CadastrarAlunoAsync(client, factory.Emails.TokenDoConvite(email));
            var token = (await cadastro.Content.ReadFromJsonAsync<RespostaDeLogin>(Ct))!.TokenDeAcesso!;
            var id = await IdDoUsuarioAsync(email);
            var treinoId = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, $"/alunos/{id}/treinos", tokenPersonal, corpo: Plano("Treino A", [supino])));
            lista.Add(new Aluno(id, token, treinoId, supino));
        }

        return new Turma(tokenPersonal, lista);
    }

    /// <summary>Sessão finalizada com duas séries de supino na carga indicada.</summary>
    private static async Task TreinarAsync(HttpClient client, Aluno aluno, DateTimeOffset inicio, decimal carga)
    {
        var sessao = new SessaoRegistrada(Guid.CreateVersion7(), aluno.TreinoId, inicio, inicio.AddMinutes(40),
        [
            new SerieRegistrada(Guid.CreateVersion7(), aluno.Supino, 10, carga, 2, inicio.AddMinutes(5)),
            new SerieRegistrada(Guid.CreateVersion7(), aluno.Supino, 8, carga, 1, inicio.AddMinutes(8)),
        ]);
        using var response = await EnviarAsync(client, HttpMethod.Post, "/sessoes/sincronizar", aluno.Token, corpo: new LoteDeSessoes([sessao]));
        (await response.Content.ReadFromJsonAsync<ResultadoDaSincronizacao>(Ct))!.Sessoes.Single().Situacao.ShouldBe(SituacaoDaSincronizacao.Sincronizada);
    }

    private static async Task ConfigurarAsync(HttpClient client, string token, Guid alunoId, CriterioDeEstagnacao criterio, int limite, HttpStatusCode esperado)
    {
        using var response = await EnviarAsync(client, HttpMethod.Put, $"/alunos/{alunoId}/estagnacao", token, corpo: new ConfiguracaoDeEstagnacao(criterio, limite));
        response.StatusCode.ShouldBe(esperado);
    }

    private static async Task<PainelDoPersonal> PainelAsync(HttpClient client, string token)
    {
        using var response = await EnviarAsync(client, HttpMethod.Get, "/painel", token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<PainelDoPersonal>(Ct))!;
    }

    private static async Task<AlunoNoPainel> AlunoNoPainelAsync(HttpClient client, string token, Guid alunoId) =>
        (await PainelAsync(client, token)).Alunos.Single(a => a.Id == alunoId);
}
