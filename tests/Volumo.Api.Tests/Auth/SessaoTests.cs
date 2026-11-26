using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volumo.Api.Tests.Support;
using Volumo.Infrastructure.Contas;
using Volumo.Shared.Autenticacao;
using static Volumo.Api.Tests.Support.ContasDeTeste;

namespace Volumo.Api.Tests.Auth;

/// <summary>Refresh token rotativo: renovação, detecção de reuso e logout.</summary>
public sealed class SessaoTests(VolumoApiFactory factory) : IClassFixture<VolumoApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Refresh_troca_o_cookie_e_devolve_um_token_de_acesso_valido()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var email = await PersonalConfirmadoAsync(client, factory);
        var (_, cookie) = await EntrarAsync(client, email);

        using var response = await RefreshAsync(client, cookie);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var novoCookie = CookieDeAtualizacao(response);
        novoCookie.ShouldNotBeNull();
        novoCookie.ShouldNotBe(cookie);
        var corpo = await response.Content.ReadFromJsonAsync<RespostaDeLogin>(Ct);
        (await GetContaAsync(client, corpo!.TokenDeAcesso)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reusar_um_refresh_token_ja_usado_derruba_a_sessao_inteira()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var email = await PersonalConfirmadoAsync(client, factory);
        var (_, roubado) = await EntrarAsync(client, email);
        string? legitimo;
        using (var primeira = await RefreshAsync(client, roubado))
        {
            legitimo = CookieDeAtualizacao(primeira);
        }

        using var reuso = await RefreshAsync(client, roubado);
        using var depois = await RefreshAsync(client, legitimo);

        reuso.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        depois.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "a família toda foi revogada");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nao-e-um-token")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task Refresh_sem_cookie_valido_responde_401_e_apaga_o_cookie(string? cookie)
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);

        using var response = await RefreshAsync(client, cookie);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        CookieDeAtualizacao(response).ShouldBeNull();
        SetCookieDeAtualizacao(response).ShouldNotBeNull("o cookie é apagado (expira no passado)");
    }

    [Fact]
    public async Task Logout_encerra_so_a_sessao_daquele_dispositivo()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var email = await PersonalConfirmadoAsync(client, factory);
        var (_, celular) = await EntrarAsync(client, email);
        var (_, computador) = await EntrarAsync(client, email);

        (await LogoutAsync(client, celular)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await RefreshAsync(client, celular)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await RefreshAsync(client, computador)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_token_vencido_nao_renova()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var email = await PersonalConfirmadoAsync(client, factory);
        var (_, cookie) = await EntrarAsync(client, email);
        await using (var db = BancoDeTestes.NovoContexto())
        {
            var usuarioId = (await db.Users.SingleAsync(u => u.Email == email, Ct)).Id;
            await db.TokensDeAtualizacao.Where(t => t.UsuarioId == usuarioId)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.ExpiraEm, DateTimeOffset.UtcNow.AddSeconds(-1)), Ct);
        }

        (await RefreshAsync(client, cookie)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Excluir_a_conta_derruba_todas_as_sessoes()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var email = await PersonalConfirmadoAsync(client, factory);
        var (_, cookie) = await EntrarAsync(client, email);
        Guid usuarioId;
        await using (var db = BancoDeTestes.NovoContexto())
        {
            usuarioId = (await db.Users.SingleAsync(u => u.Email == email, Ct)).Id;
        }

        using (var escopo = factory.Services.CreateScope())
        {
            await escopo.ServiceProvider.GetRequiredService<ExclusaoDeConta>().AnonimizarAsync(usuarioId, Ct);
        }

        (await RefreshAsync(client, cookie)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Senha_errada_de_um_atacante_nao_derruba_a_sessao_aberta()
    {
        using var client = factory.CreateHttpsClient(handleCookies: false);
        var email = await PersonalConfirmadoAsync(client, factory);
        var (_, cookie) = await EntrarAsync(client, email);
        for (var i = 0; i < 5; i++)
        {
            (await LoginAsync(client, email, "senha errada qualquer")).Dispose();
        }

        (await RefreshAsync(client, cookie)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
