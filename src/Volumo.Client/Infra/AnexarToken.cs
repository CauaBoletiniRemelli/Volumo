using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Volumo.Client.Infra;

/// <summary>
/// Põe o token de acesso em cada chamada à API e renova antes de vencer. Também libera o envio do
/// cookie (necessário nas rotas /auth, que usam o refresh token).
/// </summary>
public sealed class AnexarToken(SessaoDoUsuario sessao) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

        // Aberto offline: a internet pode ter voltado, então tenta recuperar a sessão pelo cookie.
        if (sessao.Offline || (sessao.TokenDeAcesso is not null && sessao.PertoDeVencer()))
        {
            await sessao.RenovarAsync();
        }

        if (sessao.TokenDeAcesso is { } token)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var resposta = await base.SendAsync(request, cancellationToken);
        if (resposta.StatusCode == System.Net.HttpStatusCode.Unauthorized && sessao.TokenDeAcesso is not null)
        {
            // Token recusado (conta suspensa, sessão encerrada em outro aparelho): volta para o login.
            sessao.Encerrar();
        }

        return resposta;
    }
}
