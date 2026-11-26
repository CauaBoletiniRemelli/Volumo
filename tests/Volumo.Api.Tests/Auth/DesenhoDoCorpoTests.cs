using System.Net;
using System.Net.Http.Json;
using Volumo.Api.Tests.Support;
using Volumo.Shared.Autenticacao;
using Volumo.Shared.Treinos;
using static Volumo.Api.Tests.Support.ContasDeTeste;

namespace Volumo.Api.Tests.Auth;

/// <summary>Preferência do desenho do corpo (músculos dos exercícios) e série preparatória no treino.</summary>
public sealed class DesenhoDoCorpoTests(VolumoApiFactory factory) : IClassFixture<VolumoApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Desenho_comeca_masculino_e_a_propria_pessoa_troca()
    {
        using var client = factory.CreateHttpsClient();
        var (_, tokenAluno, _, tokenPersonal) = await AlunoViaConviteAsync(client, factory);

        foreach (var token in new[] { tokenAluno, tokenPersonal })
        {
            (await ContaAsync(client, token)).CorpoDoDesenho.ShouldBe(CorpoDoDesenho.Masculino);
            (await EnviarAsync(client, HttpMethod.Put, "/conta/desenho-do-corpo", token, corpo: new PreferenciaDeDesenho(CorpoDoDesenho.Feminino)))
                .StatusCode.ShouldBe(HttpStatusCode.NoContent);
            (await ContaAsync(client, token)).CorpoDoDesenho.ShouldBe(CorpoDoDesenho.Feminino);
        }
    }

    [Fact]
    public async Task Valor_invalido_e_sem_login_sao_recusados()
    {
        using var client = factory.CreateHttpsClient();
        var (_, tokenAluno, _, _) = await AlunoViaConviteAsync(client, factory);

        (await EnviarAsync(client, HttpMethod.Put, "/conta/desenho-do-corpo", tokenAluno, corpo: new { corpoDoDesenho = "Robo" }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await EnviarAsync(client, HttpMethod.Put, "/conta/desenho-do-corpo", null, corpo: new PreferenciaDeDesenho(CorpoDoDesenho.Feminino)))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await ContaAsync(client, tokenAluno)).CorpoDoDesenho.ShouldBe(CorpoDoDesenho.Masculino);
    }

    [Fact]
    public async Task Serie_preparatoria_e_gravada_e_volta_no_treino()
    {
        using var client = factory.CreateHttpsClient();
        var (emailAluno, tokenAluno, _, tokenPersonal) = await AlunoViaConviteAsync(client, factory);
        var alunoId = await IdDoUsuarioAsync(emailAluno);
        var supino = await ExercicioDoCatalogoAsync(client, tokenPersonal);
        var plano = Plano("Treino A", [supino], series: 2);
        plano = plano with { Itens = [plano.Itens[0] with { Series = [plano.Itens[0].Series[0] with { Tipo = TipoDeSerie.Preparatoria, RepeticoesMinimas = 3, RepeticoesMaximas = 3 }, plano.Itens[0].Series[1]] }] };

        var treinoId = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, $"/alunos/{alunoId}/treinos", tokenPersonal, corpo: plano));

        using var response = await EnviarAsync(client, HttpMethod.Get, $"/treinos/{treinoId}", tokenAluno);
        var treino = (await response.Content.ReadFromJsonAsync<TreinoDetalhe>(Ct))!;
        treino.Itens.Single().Series.Select(s => s.Tipo).ShouldBe([TipoDeSerie.Preparatoria, TipoDeSerie.Trabalho]);
    }

    private static async Task<DadosDaConta> ContaAsync(HttpClient client, string token)
    {
        using var response = await GetContaAsync(client, token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<DadosDaConta>(Ct))!;
    }
}
