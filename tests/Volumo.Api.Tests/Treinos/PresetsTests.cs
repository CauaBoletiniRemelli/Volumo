using System.Net;
using System.Net.Http.Json;
using Volumo.Api.Controllers;
using Volumo.Api.Tests.Support;
using Volumo.Domain.Treinos;
using Volumo.Shared.Treinos;
using static Volumo.Api.Tests.Support.ContasDeTeste;
using TipoSerie = Volumo.Domain.Treinos.TipoSerie;

namespace Volumo.Api.Tests.Treinos;

/// <summary>Presets de séries: prontos da plataforma e os próprios de cada conta (personal ou aluno).</summary>
public sealed class PresetsTests(VolumoApiFactory factory) : IClassFixture<VolumoApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static PresetDeSeriesEntrada Entrada(string nome, params int[] repeticoes) =>
        new(nome, [.. repeticoes.Select(r => new SerieDoPreset(TipoDeSerie.Trabalho, r, r, 120))]);

    [Fact]
    public async Task Lista_traz_os_prontos_primeiro_e_depois_os_proprios_por_nome()
    {
        using var client = factory.CreateHttpsClient();
        var (_, _, token) = await PersonalComDoisFatoresAsync(client, factory);
        await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/presets", token, corpo: Entrada("Zebra", 10)));
        await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/presets", token, corpo: Entrada("Alfa", 12, 10)));

        var presets = await GetAsync<List<PresetDeSeriesSaida>>(client, "/presets", token);

        var prontos = presets.TakeWhile(p => p.Pronto).ToList();
        prontos.Select(p => p.Id).ShouldBe(PresetDeSeries.Prontos.Select(p => p.Id));
        presets.Skip(prontos.Count).Select(p => (p.Nome, p.Pronto)).ShouldBe([("Alfa", false), ("Zebra", false)]);
        presets.Single(p => p.Nome == "Pirâmide crescente").Series.Select(s => s.RepeticoesMaximas).ShouldBe([10, 8, 5, 3]);
    }

    [Fact]
    public async Task Aluno_tambem_cria_altera_e_arquiva_os_proprios()
    {
        using var client = factory.CreateHttpsClient();
        var (_, tokenAluno, _, _) = await AlunoViaConviteAsync(client, factory);

        var id = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/presets", tokenAluno, corpo: Entrada("Meu", 10, 8, 6)));
        (await EnviarAsync(client, HttpMethod.Put, $"/presets/{id}", tokenAluno,
            corpo: new PresetDeSeriesEntrada("Meu com aquecimento", [new SerieDoPreset(TipoDeSerie.Aquecimento, 15, 20, 60), new SerieDoPreset(TipoDeSerie.Trabalho, 8, 12, 90)])))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var alterado = await GetAsync<PresetDeSeriesSaida>(client, $"/presets/{id}", tokenAluno);
        alterado.Nome.ShouldBe("Meu com aquecimento");
        alterado.Series.ShouldBe([new SerieDoPreset(TipoDeSerie.Aquecimento, 15, 20, 60), new SerieDoPreset(TipoDeSerie.Trabalho, 8, 12, 90)]);

        (await EnviarAsync(client, HttpMethod.Post, $"/presets/{id}/inativar", tokenAluno)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetAsync<List<PresetDeSeriesSaida>>(client, "/presets", tokenAluno)).ShouldAllBe(p => p.Pronto);
        (await EnviarAsync(client, HttpMethod.Get, $"/presets/{id}", tokenAluno)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Personal_sem_2FA_usa_os_proprios_presets()
    {
        // Presets não expõem dados de alunos: a política não exige 2FA.
        using var client = factory.CreateHttpsClient();
        var (token, _) = await EntrarAsync(client, await PersonalConfirmadoAsync(client, factory));

        (await EnviarAsync(client, HttpMethod.Post, "/presets", token, corpo: Entrada("Sem 2FA", 10))).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Series_invalidas_respondem_400()
    {
        using var client = factory.CreateHttpsClient();
        var (_, tokenAluno, _, _) = await AlunoViaConviteAsync(client, factory);

        using var semSeries = await EnviarAsync(client, HttpMethod.Post, "/presets", tokenAluno, corpo: new PresetDeSeriesEntrada("Vazio", []));
        using var repsInvertidas = await EnviarAsync(client, HttpMethod.Post, "/presets", tokenAluno,
            corpo: new PresetDeSeriesEntrada("Ruim", [new SerieDoPreset(TipoDeSerie.Trabalho, 12, 8, 60)]));
        using var semNome = await EnviarAsync(client, HttpMethod.Post, "/presets", tokenAluno, corpo: Entrada(" ", 10));
        using var muitas = await EnviarAsync(client, HttpMethod.Post, "/presets", tokenAluno, corpo: Entrada("Muitas", [.. Enumerable.Repeat(10, 21)]));

        semSeries.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        repsInvertidas.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        semNome.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        muitas.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cada_conta_tem_um_limite_de_presets()
    {
        using var client = factory.CreateHttpsClient();
        var (emailAluno, tokenAluno, _, _) = await AlunoViaConviteAsync(client, factory);
        var alunoId = await IdDoUsuarioAsync(emailAluno);
        await using (var db = BancoDeTestes.NovoContexto())
        {
            db.PresetsDeSeries.AddRange(Enumerable.Range(0, PresetsController.MaximoPorConta)
                .Select(i => PresetDeSeries.Criar(Guid.CreateVersion7(), alunoId, $"Preset {i}", [Serie.Criar(TipoSerie.Trabalho, 10, 10, 60)])));
            await db.SaveChangesAsync(Ct);
        }

        using var response = await EnviarAsync(client, HttpMethod.Post, "/presets", tokenAluno, corpo: Entrada("Um a mais", 10));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Matriz_de_autorizacao_dos_presets()
    {
        using var client = factory.CreateHttpsClient();
        var (_, _, token) = await PersonalComDoisFatoresAsync(client, factory);
        var (_, tokenAluno, _, _) = await AlunoViaConviteAsync(client, factory);
        var id = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/presets", token, corpo: Entrada("Do personal", 10)));
        var pronto = PresetDeSeries.Prontos[0].Id;
        var corpo = Entrada("Invasão", 5);

        (await EnviarAsync(client, HttpMethod.Get, "/presets", null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await EnviarAsync(client, HttpMethod.Post, "/presets", null, corpo: corpo)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await EnviarAsync(client, HttpMethod.Get, "/presets", await AdminSemOutrosPapeisAsync(client, factory))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Preset de outra conta: invisível.
        (await EnviarAsync(client, HttpMethod.Get, $"/presets/{id}", tokenAluno)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await EnviarAsync(client, HttpMethod.Put, $"/presets/{id}", tokenAluno, corpo: corpo)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await EnviarAsync(client, HttpMethod.Post, $"/presets/{id}/inativar", tokenAluno)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await GetAsync<List<PresetDeSeriesSaida>>(client, "/presets", tokenAluno)).ShouldNotContain(p => p.Id == id);

        // Pronto: todos veem, ninguém altera.
        (await GetAsync<PresetDeSeriesSaida>(client, $"/presets/{pronto}", tokenAluno)).Pronto.ShouldBeTrue();
        (await EnviarAsync(client, HttpMethod.Put, $"/presets/{pronto}", token, corpo: corpo)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await EnviarAsync(client, HttpMethod.Post, $"/presets/{pronto}/inativar", tokenAluno)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await GetAsync<PresetDeSeriesSaida>(client, $"/presets/{id}", token)).Nome.ShouldBe("Do personal");
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string caminho, string token)
    {
        using var response = await EnviarAsync(client, HttpMethod.Get, caminho, token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }
}
