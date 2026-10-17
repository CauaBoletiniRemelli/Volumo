using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volumo.Api.Tests.Support;
using Volumo.Infrastructure.Contas;
using Volumo.Shared.Autenticacao;
using static Volumo.Api.Tests.Support.ContasDeTeste;

namespace Volumo.Api.Tests.Auth;

public sealed class LoginTests(VolumoApiFactory factory) : IClassFixture<VolumoApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Login_valido_devolve_token_de_15_minutos_e_cookie_protegido()
    {
        using var client = factory.CreateHttpsClient();
        var email = await PersonalConfirmadoAsync(client, factory);

        using var response = await LoginAsync(client, email);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<RespostaDeLogin>(Ct);
        corpo!.TokenDeAcesso.ShouldNotBeNullOrWhiteSpace();
        (corpo.ExpiraEm!.Value - DateTimeOffset.UtcNow).TotalMinutes.ShouldBeInRange(14, 15.1);

        var cookie = SetCookieDeAtualizacao(response)!.ToUpperInvariant();
        cookie.ShouldContain("HTTPONLY");
        cookie.ShouldContain("SECURE");
        cookie.ShouldContain("SAMESITE=STRICT");
        cookie.ShouldContain("PATH=/AUTH");
        cookie.ShouldNotContain("DOMAIN=");
    }

    [Fact]
    public async Task Token_de_acesso_abre_rota_protegida_com_os_papeis_da_conta()
    {
        using var client = factory.CreateHttpsClient();
        var email = await PersonalConfirmadoAsync(client, factory);
        var (token, _) = await EntrarAsync(client, email);

        using var response = await GetContaAsync(client, token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var conta = await response.Content.ReadFromJsonAsync<DadosDaConta>(Ct);
        conta!.Email.ShouldBe(email);
        conta.Papeis.ShouldBe(["Personal"]);
    }

    [Fact]
    public async Task Login_antes_de_confirmar_o_email_responde_403()
    {
        using var client = factory.CreateHttpsClient();
        var email = NovoEmail();
        (await CadastrarPersonalAsync(client, email)).Dispose();

        using var response = await LoginAsync(client, email);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        SetCookieDeAtualizacao(response).ShouldBeNull();
    }

    [Fact]
    public async Task Senha_errada_e_email_inexistente_recebem_a_mesma_resposta()
    {
        using var client = factory.CreateHttpsClient();
        var email = await PersonalConfirmadoAsync(client, factory);

        using var senhaErrada = await LoginAsync(client, email, "senha errada qualquer");
        using var inexistente = await LoginAsync(client, NovoEmail());

        senhaErrada.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        inexistente.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var p1 = await senhaErrada.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        var p2 = await inexistente.Content.ReadFromJsonAsync<ProblemDetails>(Ct);
        (p1!.Title, p1.Detail).ShouldBe((p2!.Title, p2.Detail));
    }

    [Fact]
    public async Task Cinco_senhas_erradas_bloqueiam_a_conta_mesmo_com_a_senha_certa_depois()
    {
        using var client = factory.CreateHttpsClient();
        var email = await PersonalConfirmadoAsync(client, factory);
        for (var i = 0; i < 5; i++)
        {
            (await LoginAsync(client, email, "senha errada qualquer")).Dispose();
        }

        using var response = await LoginAsync(client, email);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await using var db = BancoDeTestes.NovoContexto();
        (await db.Users.SingleAsync(u => u.Email == email, Ct)).LockoutEnd.ShouldNotBeNull();
    }

    [Fact]
    public async Task Login_certo_zera_a_contagem_de_erros()
    {
        using var client = factory.CreateHttpsClient();
        var email = await PersonalConfirmadoAsync(client, factory);
        for (var i = 0; i < 4; i++)
        {
            (await LoginAsync(client, email, "senha errada qualquer")).Dispose();
        }

        (await LoginAsync(client, email)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await LoginAsync(client, email, "senha errada qualquer")).Dispose();

        (await LoginAsync(client, email)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Conta_excluida_nao_consegue_entrar()
    {
        using var client = factory.CreateHttpsClient();
        var email = await PersonalConfirmadoAsync(client, factory);
        Guid usuarioId;
        await using (var db = BancoDeTestes.NovoContexto())
        {
            usuarioId = (await db.Users.SingleAsync(u => u.Email == email, Ct)).Id;
        }

        using (var escopo = factory.Services.CreateScope())
        {
            await escopo.ServiceProvider.GetRequiredService<ExclusaoDeConta>().AnonimizarAsync(usuarioId, Ct);
        }

        (await LoginAsync(client, email)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Rota_de_login_tem_limite_proprio_de_tentativas()
    {
        await using var limitada = new VolumoApiFactory().WithSetting("RateLimiting:Login:PermitLimit", "2");
        using var client = limitada.CreateHttpsClient();

        for (var i = 0; i < 2; i++)
        {
            (await LoginAsync(client, NovoEmail())).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        (await LoginAsync(client, NovoEmail())).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }
}
