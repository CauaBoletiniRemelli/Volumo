using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volumo.Api.Auth;
using Volumo.Shared.Autenticacao;

namespace Volumo.Api.Tests.Support;

/// <summary>Atalhos para criar contas e sessões pela API, como um cliente real faria.</summary>
internal static class ContasDeTeste
{
    public const string SenhaValida = "uma senha forte de teste";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static string NovoEmail(string prefixo = "personal") => $"{prefixo}-{Guid.NewGuid():N}@volumo.test";

    public static Task<HttpResponseMessage> CadastrarPersonalAsync(
        HttpClient client, string email, string senha = SenhaValida, string? cref = null, string tokenTurnstile = VerificacaoHumanaFalsa.TokenAprovado) =>
        client.PostAsJsonAsync("/auth/cadastro/personal", new CadastroDePersonal("Personal de Teste", email, senha, cref, tokenTurnstile), Ct);

    public static async Task ConfirmarEmailAsync(HttpClient client, VolumoApiFactory factory, string email)
    {
        var (usuarioId, token) = factory.Emails.LinkDeConfirmacao(email);
        using var response = await client.PostAsJsonAsync("/auth/confirmar-email", new ConfirmacaoDeEmail(usuarioId, token), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    /// <summary>Cadastra e confirma um personal; devolve o e-mail.</summary>
    public static async Task<string> PersonalConfirmadoAsync(HttpClient client, VolumoApiFactory factory)
    {
        var email = NovoEmail();
        using (var response = await CadastrarPersonalAsync(client, email))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        }

        await ConfirmarEmailAsync(client, factory, email);
        return email;
    }

    public static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string senha = SenhaValida) =>
        client.PostAsJsonAsync("/auth/login", new PedidoDeLogin(email, senha), Ct);

    /// <summary>Faz login e devolve o token de acesso e o valor do cookie do refresh token.</summary>
    public static async Task<(string TokenDeAcesso, string Cookie)> EntrarAsync(HttpClient client, string email)
    {
        using var response = await LoginAsync(client, email);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corpo = await response.Content.ReadFromJsonAsync<RespostaDeLogin>(Ct);
        return (corpo!.TokenDeAcesso!, CookieDeAtualizacao(response)!);
    }

    /// <summary>Inicia e ativa o 2FA na conta logada; devolve a chave, os códigos e a nova sessão (com 2FA).</summary>
    public static async Task<(string Chave, IReadOnlyList<string> Codigos, string TokenDeAcesso, string Cookie)> AtivarDoisFatoresAsync(
        HttpClient client, string tokenDeAcesso, string? cookie = null)
    {
        using var inicio = await EnviarAsync(client, HttpMethod.Post, "/auth/2fa/iniciar", tokenDeAcesso, cookie);
        inicio.StatusCode.ShouldBe(HttpStatusCode.OK);
        var configuracao = await inicio.Content.ReadFromJsonAsync<ConfiguracaoDeDoisFatores>(Ct);

        using var ativacao = await EnviarAsync(
            client, HttpMethod.Post, "/auth/2fa/ativar", tokenDeAcesso, cookie, new AtivacaoDeDoisFatores(Totp.Codigo(configuracao!.Chave)));
        ativacao.StatusCode.ShouldBe(HttpStatusCode.OK);
        var resposta = await ativacao.Content.ReadFromJsonAsync<RespostaDeAtivacaoDeDoisFatores>(Ct);
        return (configuracao.Chave, resposta!.CodigosDeRecuperacao, resposta.Sessao.TokenDeAcesso!, CookieDeAtualizacao(ativacao)!);
    }

    /// <summary>Personal cadastrado, confirmado e com 2FA ativo; devolve a chave do autenticador e um token com 2FA.</summary>
    public static async Task<(string Email, string Chave, string TokenDeAcesso)> PersonalComDoisFatoresAsync(HttpClient client, VolumoApiFactory factory)
    {
        var email = await PersonalConfirmadoAsync(client, factory);
        var (token, _) = await EntrarAsync(client, email);
        var (chave, _, tokenComDoisFatores, _) = await AtivarDoisFatoresAsync(client, token);
        return (email, chave, tokenComDoisFatores);
    }

    public static Task<HttpResponseMessage> ConvidarAsync(HttpClient client, string tokenDoPersonal, string emailDoAluno) =>
        EnviarAsync(client, HttpMethod.Post, "/convites", tokenDoPersonal, corpo: new Volumo.Shared.Convites.NovoConvite(emailDoAluno));

    public static Task<HttpResponseMessage> CadastrarAlunoAsync(HttpClient client, string tokenDoConvite, string senha = SenhaValida) =>
        client.PostAsJsonAsync("/auth/cadastro/aluno", new CadastroDeAluno(tokenDoConvite, "Aluno de Teste", senha), Ct);

    /// <summary>Personal com 2FA convida e o aluno se cadastra pelo link. Devolve os dois.</summary>
    public static async Task<(string EmailAluno, string TokenAluno, string EmailPersonal, string TokenPersonal)> AlunoViaConviteAsync(
        HttpClient client, VolumoApiFactory factory)
    {
        var (emailPersonal, _, tokenPersonal) = await PersonalComDoisFatoresAsync(client, factory);
        var emailAluno = NovoEmail("aluno");
        (await ConvidarAsync(client, tokenPersonal, emailAluno)).StatusCode.ShouldBe(HttpStatusCode.Created);

        using var cadastro = await CadastrarAlunoAsync(client, factory.Emails.TokenDoConvite(emailAluno));
        cadastro.StatusCode.ShouldBe(HttpStatusCode.OK);
        var sessao = await cadastro.Content.ReadFromJsonAsync<RespostaDeLogin>(Ct);
        return (emailAluno, sessao!.TokenDeAcesso!, emailPersonal, tokenPersonal);
    }

    /// <summary>Dá o papel Admin à conta (não há rota para isso: é feito direto na plataforma).</summary>
    public static async Task TornarAdminAsync(VolumoApiFactory factory, string email)
    {
        using var escopo = factory.Services.CreateScope();
        var userManager = escopo.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Volumo.Infrastructure.Identity.Usuario>>();
        var usuario = await userManager.FindByEmailAsync(email);
        (await userManager.AddToRoleAsync(usuario!, Volumo.Domain.Usuarios.Papeis.Admin)).Succeeded.ShouldBeTrue();
    }

    public static async Task<Guid> IdDoUsuarioAsync(string email)
    {
        await using var db = BancoDeTestes.NovoContexto();
        return await db.Users.Where(u => u.NormalizedEmail == email.ToUpperInvariant()).Select(u => u.Id).SingleAsync(Ct);
    }

    /// <summary>Requisição com token de acesso e/ou cookie do refresh token explícitos.</summary>
    public static async Task<HttpResponseMessage> EnviarAsync(
        HttpClient client, HttpMethod metodo, string caminho, string? tokenDeAcesso, string? cookie = null, object? corpo = null)
    {
        using var request = new HttpRequestMessage(metodo, caminho);
        if (tokenDeAcesso is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenDeAcesso);
        }

        if (cookie is not null)
        {
            request.Headers.Add("Cookie", $"{RefreshTokenCookie.Name}={cookie}");
        }

        if (corpo is not null)
        {
            request.Content = JsonContent.Create(corpo);
        }

        return await client.SendAsync(request, Ct);
    }

    /// <summary>Linha Set-Cookie completa do refresh token (com atributos), ou null.</summary>
    public static string? SetCookieDeAtualizacao(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var valores)
            ? valores.FirstOrDefault(v => v.StartsWith(RefreshTokenCookie.Name + "=", StringComparison.Ordinal))
            : null;

    /// <summary>Só o valor do cookie do refresh token, ou null.</summary>
    public static string? CookieDeAtualizacao(HttpResponseMessage response)
    {
        var linha = SetCookieDeAtualizacao(response);
        if (linha is null)
        {
            return null;
        }

        var valor = linha[(RefreshTokenCookie.Name.Length + 1)..].Split(';')[0];
        return valor.Length == 0 ? null : valor;
    }

    /// <summary>Chama /auth/refresh enviando o cookie manualmente (cliente criado com handleCookies: false).</summary>
    public static async Task<HttpResponseMessage> RefreshAsync(HttpClient client, string? cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh");
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", $"{RefreshTokenCookie.Name}={cookie}");
        }

        return await client.SendAsync(request, Ct);
    }

    public static async Task<HttpResponseMessage> LogoutAsync(HttpClient client, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        request.Headers.Add("Cookie", $"{RefreshTokenCookie.Name}={cookie}");
        return await client.SendAsync(request, Ct);
    }

    public static async Task<HttpResponseMessage> GetContaAsync(HttpClient client, string? tokenDeAcesso)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/conta");
        if (tokenDeAcesso is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenDeAcesso);
        }

        return await client.SendAsync(request, Ct);
    }
}
