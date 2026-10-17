using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Volumo.Api.Auth;
using Volumo.Shared.Autenticacao;

namespace Volumo.Api.Tests.Support;

/// <summary>Atalhos para criar contas e sessões pela API, como um cliente real faria.</summary>
internal static class ContasDeTeste
{
    public const string SenhaValida = "uma senha forte de teste";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static string NovoEmail(string prefixo = "personal") => $"{prefixo}-{Guid.NewGuid():N}@volumo.test";

    public static Task<HttpResponseMessage> CadastrarPersonalAsync(HttpClient client, string email, string senha = SenhaValida, string? cref = null) =>
        client.PostAsJsonAsync("/auth/cadastro/personal", new CadastroDePersonal("Personal de Teste", email, senha, cref), Ct);

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
        return (corpo!.TokenDeAcesso, CookieDeAtualizacao(response)!);
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
