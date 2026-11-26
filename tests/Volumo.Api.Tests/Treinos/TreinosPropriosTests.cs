using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Volumo.Api.Tests.Support;
using Volumo.Infrastructure.Identity;
using Volumo.Shared.Sessoes;
using Volumo.Shared.Treinos;
using static Volumo.Api.Tests.Support.ContasDeTeste;

namespace Volumo.Api.Tests.Treinos;

/// <summary>
/// Treinos montados por quem treina: o aluno (com ou sem personal) e o personal nos próprios treinos
/// (todo personal também tem o perfil de aluno).
/// </summary>
public sealed class TreinosPropriosTests(VolumoApiFactory factory) : IClassFixture<VolumoApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Aluno_monta_altera_e_arquiva_o_proprio_treino()
    {
        using var client = factory.CreateHttpsClient();
        var (emailAluno, tokenAluno, _, tokenPersonal) = await AlunoViaConviteAsync(client, factory);
        var supino = await ExercicioDoCatalogoAsync(client, tokenAluno);
        var remada = await ExercicioDoCatalogoAsync(client, tokenAluno, "Remada curvada com barra");
        await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, $"/alunos/{await IdDoUsuarioAsync(emailAluno)}/treinos", tokenPersonal, corpo: Plano("Do personal", [supino])));

        var id = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/treinos", tokenAluno, corpo: Plano("Extra de sábado", [supino])));

        var lista = await GetAsync<List<TreinoResumo>>(client, "/treinos", tokenAluno);
        lista.Select(t => (t.Nome, t.MontadoPeloAluno)).ShouldBe([("Do personal", false), ("Extra de sábado", true)]); // o novo entra no fim
        (await GetAsync<TreinoDetalhe>(client, $"/treinos/{id}", tokenAluno)).PersonalId.ShouldBeNull();

        (await EnviarAsync(client, HttpMethod.Put, $"/treinos/{id}", tokenAluno, corpo: Plano("Extra de domingo", [supino, remada])))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var alterado = await GetAsync<TreinoDetalhe>(client, $"/treinos/{id}", tokenAluno);
        alterado.Nome.ShouldBe("Extra de domingo");
        alterado.Itens.Count.ShouldBe(2);

        (await EnviarAsync(client, HttpMethod.Post, $"/treinos/{id}/inativar", tokenAluno)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetAsync<List<TreinoResumo>>(client, "/treinos", tokenAluno)).Single().Nome.ShouldBe("Do personal");
    }

    [Fact]
    public async Task Personal_ve_o_treino_montado_pelo_aluno_mas_nao_altera_e_o_aluno_nao_altera_o_prescrito()
    {
        using var client = factory.CreateHttpsClient();
        var (emailAluno, tokenAluno, _, tokenPersonal) = await AlunoViaConviteAsync(client, factory);
        var alunoId = await IdDoUsuarioAsync(emailAluno);
        var supino = await ExercicioDoCatalogoAsync(client, tokenAluno);
        var doAluno = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/treinos", tokenAluno, corpo: Plano("Meu", [supino])));
        var prescrito = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, $"/alunos/{alunoId}/treinos", tokenPersonal, corpo: Plano("Do personal", [supino])));
        var plano = Plano("Mexido", [supino]);

        (await GetAsync<List<TreinoResumo>>(client, $"/alunos/{alunoId}/treinos", tokenPersonal)).Single(t => t.Id == doAluno).MontadoPeloAluno.ShouldBeTrue();
        (await GetAsync<TreinoDetalhe>(client, $"/treinos/{doAluno}", tokenPersonal)).Nome.ShouldBe("Meu");
        (await EnviarAsync(client, HttpMethod.Put, $"/treinos/{doAluno}", tokenPersonal, corpo: plano)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await EnviarAsync(client, HttpMethod.Post, $"/treinos/{doAluno}/inativar", tokenPersonal)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await EnviarAsync(client, HttpMethod.Put, $"/treinos/{prescrito}", tokenAluno, corpo: plano)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await EnviarAsync(client, HttpMethod.Post, $"/treinos/{prescrito}/inativar", tokenAluno)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await GetAsync<TreinoDetalhe>(client, $"/treinos/{prescrito}", tokenAluno)).Nome.ShouldBe("Do personal");
    }

    [Fact]
    public async Task Aluno_reordena_os_proprios_e_os_do_personal_ficam_onde_o_personal_pos()
    {
        using var client = factory.CreateHttpsClient();
        var (emailAluno, tokenAluno, _, tokenPersonal) = await AlunoViaConviteAsync(client, factory);
        var alunoId = await IdDoUsuarioAsync(emailAluno);
        var supino = await ExercicioDoCatalogoAsync(client, tokenAluno);
        var p1 = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, $"/alunos/{alunoId}/treinos", tokenPersonal, corpo: Plano("P1", [supino])));
        var p2 = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, $"/alunos/{alunoId}/treinos", tokenPersonal, corpo: Plano("P2", [supino])));
        var o1 = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/treinos", tokenAluno, corpo: Plano("O1", [supino])));
        var o2 = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/treinos", tokenAluno, corpo: Plano("O2", [supino])));
        (await EnviarAsync(client, HttpMethod.Put, $"/alunos/{alunoId}/treinos/ordem", tokenPersonal, corpo: new OrdemDosTreinos([o1, p1, o2, p2])))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await EnviarAsync(client, HttpMethod.Put, "/treinos/ordem", tokenAluno, corpo: new OrdemDosTreinos([o2, o1]))).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await GetAsync<List<TreinoResumo>>(client, "/treinos", tokenAluno)).Select(t => t.Nome).ShouldBe(["O2", "P1", "O1", "P2"]);
        (await GetAsync<List<TreinoResumo>>(client, $"/alunos/{alunoId}/treinos", tokenPersonal)).Select(t => t.Nome).ShouldBe(["O2", "P1", "O1", "P2"]);

        // Lista com treino prescrito, faltando um próprio ou repetida: 400.
        foreach (var invalida in new List<Guid>[] { [o1, o2, p1], [o1], [o1, o1] })
        {
            (await EnviarAsync(client, HttpMethod.Put, "/treinos/ordem", tokenAluno, corpo: new OrdemDosTreinos(invalida))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        (await EnviarAsync(client, HttpMethod.Put, "/treinos/ordem", null, corpo: new OrdemDosTreinos([o1, o2]))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await EnviarAsync(client, HttpMethod.Put, "/treinos/ordem", await AdminSemOutrosPapeisAsync(client, factory), corpo: new OrdemDosTreinos([o1, o2])))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Personal_reordena_todos_os_proprios_treinos()
    {
        using var client = factory.CreateHttpsClient();
        var (_, _, token) = await PersonalComDoisFatoresAsync(client, factory);
        var supino = await ExercicioDoCatalogoAsync(client, token);
        var a = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/treinos", token, corpo: Plano("A", [supino])));
        var b = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/treinos", token, corpo: Plano("B", [supino])));
        var c = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/treinos", token, corpo: Plano("C", [supino])));

        (await EnviarAsync(client, HttpMethod.Put, "/treinos/ordem", token, corpo: new OrdemDosTreinos([c, a, b]))).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await GetAsync<List<TreinoResumo>>(client, "/treinos", token)).Select(t => t.Nome).ShouldBe(["C", "A", "B"]);
    }

    [Fact]
    public async Task Matriz_de_autorizacao_dos_treinos_proprios()
    {
        using var client = factory.CreateHttpsClient();
        var (_, tokenAluno, _, _) = await AlunoViaConviteAsync(client, factory);
        var (_, tokenOutroAluno, _, tokenOutroPersonal) = await AlunoViaConviteAsync(client, factory);
        var supino = await ExercicioDoCatalogoAsync(client, tokenAluno);
        var id = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/treinos", tokenAluno, corpo: Plano("Meu", [supino])));
        var plano = Plano("Invasão", [supino]);

        (await EnviarAsync(client, HttpMethod.Post, "/treinos", null, corpo: plano)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await EnviarAsync(client, HttpMethod.Put, $"/treinos/{id}", null, corpo: plano)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var semPapel = await AdminSemOutrosPapeisAsync(client, factory);
        (await EnviarAsync(client, HttpMethod.Post, "/treinos", semPapel, corpo: plano)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await EnviarAsync(client, HttpMethod.Put, $"/treinos/{id}", semPapel, corpo: plano)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        foreach (var outro in new[] { tokenOutroAluno, tokenOutroPersonal })
        {
            (await EnviarAsync(client, HttpMethod.Get, $"/treinos/{id}", outro)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await EnviarAsync(client, HttpMethod.Put, $"/treinos/{id}", outro, corpo: plano)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await EnviarAsync(client, HttpMethod.Post, $"/treinos/{id}/inativar", outro)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        // Exercício que o aluno não enxerga (personalizado de outro personal).
        using var exclusivo = await EnviarAsync(client, HttpMethod.Post, "/exercicios", tokenOutroPersonal, corpo: new DadosDoExercicio("Exclusivo", GrupoMuscular.Costas));
        var exercicioDoOutro = (await exclusivo.Content.ReadFromJsonAsync<ExercicioResumo>(Ct))!.Id;
        (await EnviarAsync(client, HttpMethod.Post, "/treinos", tokenAluno, corpo: Plano("X", [exercicioDoOutro]))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Personal_monta_e_registra_os_proprios_treinos_sem_os_alunos_verem()
    {
        using var client = factory.CreateHttpsClient();
        var (_, tokenAluno, _, tokenPersonal) = await AlunoViaConviteAsync(client, factory);
        var supino = await ExercicioDoCatalogoAsync(client, tokenPersonal);

        var id = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/treinos", tokenPersonal, corpo: Plano("Meu treino de peito", [supino])));
        var inicio = DateTimeOffset.UtcNow.AddHours(-1);
        var sessao = new SessaoRegistrada(Guid.CreateVersion7(), id, inicio, inicio.AddMinutes(45), [new SerieRegistrada(Guid.CreateVersion7(), supino, 8, 60m, Rir: 1, inicio.AddMinutes(5))]);
        using var envio = await EnviarAsync(client, HttpMethod.Post, "/sessoes/sincronizar", tokenPersonal, corpo: new LoteDeSessoes([sessao]));

        envio.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await envio.Content.ReadFromJsonAsync<ResultadoDaSincronizacao>(Ct))!.Sessoes.Single().Situacao.ShouldBe(SituacaoDaSincronizacao.Sincronizada);
        (await GetAsync<List<TreinoResumo>>(client, "/treinos", tokenPersonal)).Single().Nome.ShouldBe("Meu treino de peito");
        (await GetAsync<List<SessaoResumo>>(client, "/sessoes", tokenPersonal)).Single().Id.ShouldBe(sessao.Id);

        (await EnviarAsync(client, HttpMethod.Get, $"/treinos/{id}", tokenAluno)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await EnviarAsync(client, HttpMethod.Get, $"/sessoes/{sessao.Id}", tokenAluno)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await GetAsync<List<TreinoResumo>>(client, "/treinos", tokenAluno)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Personal_nao_convida_a_si_mesmo()
    {
        using var client = factory.CreateHttpsClient();
        var (email, _, token) = await PersonalComDoisFatoresAsync(client, factory);

        using var response = await ConvidarAsync(client, token, email.ToUpperInvariant());

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Personal_nao_aceita_convite_de_outro_personal()
    {
        using var client = factory.CreateHttpsClient();
        var (email, _, token) = await PersonalComDoisFatoresAsync(client, factory);
        var (_, _, tokenOutro) = await PersonalComDoisFatoresAsync(client, factory);
        (await ConvidarAsync(client, tokenOutro, email)).StatusCode.ShouldBe(HttpStatusCode.Created);

        using var response = await EnviarAsync(client, HttpMethod.Post, "/convites/aceitar", token, corpo: new Volumo.Shared.Convites.TokenDeConvite(factory.Emails.TokenDoConvite(email)));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await GetAsync<List<Volumo.Shared.Alunos.AlunoResumo>>(client, "/alunos", tokenOutro)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Personal_sem_2FA_nao_altera_o_treino_que_prescreveu()
    {
        using var client = factory.CreateHttpsClient();
        var (emailAluno, _, emailPersonal, tokenPersonal) = await AlunoViaConviteAsync(client, factory);
        var supino = await ExercicioDoCatalogoAsync(client, tokenPersonal);
        var prescrito = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, $"/alunos/{await IdDoUsuarioAsync(emailAluno)}/treinos", tokenPersonal, corpo: Plano("A", [supino])));
        var semDoisFatores = await EntrarSemDoisFatoresAsync(emailPersonal, client);

        // Sem 2FA o treino do aluno nem aparece (filtro de dono): 404, como recurso de outro dono.
        (await EnviarAsync(client, HttpMethod.Put, $"/treinos/{prescrito}", semDoisFatores, corpo: Plano("B", [supino]))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await EnviarAsync(client, HttpMethod.Post, $"/treinos/{prescrito}/inativar", semDoisFatores)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Personal_sem_2FA_nao_le_dados_dos_alunos_nas_rotas_de_qualquer_conta_mas_le_os_proprios()
    {
        using var client = factory.CreateHttpsClient();
        var (emailAluno, tokenAluno, emailPersonal, tokenPersonal) = await AlunoViaConviteAsync(client, factory);
        var supino = await ExercicioDoCatalogoAsync(client, tokenPersonal);
        var doAluno = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, $"/alunos/{await IdDoUsuarioAsync(emailAluno)}/treinos", tokenPersonal, corpo: Plano("A", [supino])));
        var proprio = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/treinos", tokenPersonal, corpo: Plano("Meu", [supino])));
        var inicio = DateTimeOffset.UtcNow.AddHours(-1);
        var sessao = new SessaoRegistrada(Guid.CreateVersion7(), doAluno, inicio, inicio.AddMinutes(30), [new SerieRegistrada(Guid.CreateVersion7(), supino, 10, 40m, Rir: 2, inicio.AddMinutes(5))]);
        (await EnviarAsync(client, HttpMethod.Post, "/sessoes/sincronizar", tokenAluno, corpo: new LoteDeSessoes([sessao]))).StatusCode.ShouldBe(HttpStatusCode.OK);
        var semDoisFatores = await EntrarSemDoisFatoresAsync(emailPersonal, client);

        foreach (var caminho in new[] { $"/treinos/{doAluno}", $"/treinos/{doAluno}/evolucao", $"/treinos/{doAluno}/exportar?formato=Xlsx", $"/sessoes/{sessao.Id}" })
        {
            (await EnviarAsync(client, HttpMethod.Get, caminho, semDoisFatores)).StatusCode.ShouldBe(HttpStatusCode.NotFound, caminho);
            (await EnviarAsync(client, HttpMethod.Get, caminho, tokenPersonal)).StatusCode.ShouldBe(HttpStatusCode.OK, caminho); // com 2FA, continua vendo
        }

        (await GetAsync<TreinoDetalhe>(client, $"/treinos/{proprio}", semDoisFatores)).Nome.ShouldBe("Meu");
        (await GetAsync<List<TreinoResumo>>(client, "/treinos", semDoisFatores)).Single().Id.ShouldBe(proprio);
    }

    [Fact]
    public async Task Dias_da_semana_quem_altera_o_treino_marca_os_dias_e_os_dois_lados_veem()
    {
        using var client = factory.CreateHttpsClient();
        var (emailAluno, tokenAluno, _, tokenPersonal) = await AlunoViaConviteAsync(client, factory);
        var alunoId = await IdDoUsuarioAsync(emailAluno);
        var supino = await ExercicioDoCatalogoAsync(client, tokenAluno);
        var prescrito = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, $"/alunos/{alunoId}/treinos", tokenPersonal, corpo: Plano("Treino A", [supino])));
        var doAluno = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/treinos", tokenAluno, corpo: Plano("Extra", [supino])));
        (await GetAsync<TreinoDetalhe>(client, $"/treinos/{prescrito}", tokenAluno)).Dias.ShouldBe([]);

        (await EnviarAsync(client, HttpMethod.Put, $"/treinos/{prescrito}/dias", tokenPersonal, corpo: new DiasDoTreino([DayOfWeek.Thursday, DayOfWeek.Monday])))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await EnviarAsync(client, HttpMethod.Put, $"/treinos/{doAluno}/dias", tokenAluno, corpo: new DiasDoTreino([DayOfWeek.Saturday])))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var doLadoDoAluno = await GetAsync<List<TreinoResumo>>(client, "/treinos", tokenAluno);
        doLadoDoAluno.Single(t => t.Id == prescrito).Dias.ShouldBe([DayOfWeek.Monday, DayOfWeek.Thursday]);
        doLadoDoAluno.Single(t => t.Id == doAluno).Dias.ShouldBe([DayOfWeek.Saturday]);
        (await GetAsync<List<TreinoResumo>>(client, $"/alunos/{alunoId}/treinos", tokenPersonal)).Single(t => t.Id == doAluno).Dias.ShouldBe([DayOfWeek.Saturday]);
        (await GetAsync<TreinoDetalhe>(client, $"/treinos/{prescrito}", tokenAluno)).Dias.ShouldBe([DayOfWeek.Monday, DayOfWeek.Thursday]);

        // Mesma regra de quem altera o treino; dia fora de domingo a sábado é recusado.
        (await EnviarAsync(client, HttpMethod.Put, $"/treinos/{prescrito}/dias", tokenAluno, corpo: new DiasDoTreino([DayOfWeek.Friday])))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await EnviarAsync(client, HttpMethod.Put, $"/treinos/{doAluno}/dias", tokenPersonal, corpo: new DiasDoTreino([DayOfWeek.Friday])))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await EnviarAsync(client, HttpMethod.Put, $"/treinos/{doAluno}/dias", tokenAluno, corpo: new DiasDoTreino([(DayOfWeek)9])))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await EnviarAsync(client, HttpMethod.Put, $"/treinos/{prescrito}/dias", tokenPersonal, corpo: new DiasDoTreino([])))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetAsync<TreinoDetalhe>(client, $"/treinos/{prescrito}", tokenAluno)).Dias.ShouldBe([]);
    }

    /// <summary>Desliga o 2FA da conta direto no banco e entra só com a senha (sessão sem "mfa").</summary>
    private async Task<string> EntrarSemDoisFatoresAsync(string email, HttpClient client)
    {
        using (var escopo = factory.Services.CreateScope())
        {
            var userManager = escopo.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
            (await userManager.SetTwoFactorEnabledAsync((await userManager.FindByEmailAsync(email))!, false)).Succeeded.ShouldBeTrue();
        }

        return (await EntrarAsync(client, email)).TokenDeAcesso;
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string caminho, string token)
    {
        using var response = await EnviarAsync(client, HttpMethod.Get, caminho, token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }
}
