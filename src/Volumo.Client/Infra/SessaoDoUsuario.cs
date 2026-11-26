using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Volumo.Shared.Autenticacao;

namespace Volumo.Client.Infra;

/// <summary>
/// Sessão no navegador. O token de acesso fica só em memória (nunca em localStorage); o refresh
/// token fica no cookie HttpOnly que a API grava. Ao abrir o app, a sessão é restaurada pelo cookie.
/// Renovações são serializadas: só uma por vez nesta aba.
/// Aluno que abre o app sem internet entra em "modo offline": sem token, só com o id guardado no
/// aparelho, para treinar com os dados locais. Quando a rede volta, a sessão é renovada pelo cookie.
/// </summary>
public sealed class SessaoDoUsuario(HttpClient semToken, ArmazenamentoLocal local) : AuthenticationStateProvider
{
    private const string PapelAluno = "Aluno";
    private static readonly ClaimsPrincipal Anonimo = new(new ClaimsIdentity());
    private readonly SemaphoreSlim _trava = new(1, 1);
    private ClaimsPrincipal _usuario = Anonimo;
    private bool _restaurou;

    public string? TokenDeAcesso { get; private set; }

    public DateTimeOffset ExpiraEm { get; private set; }

    public bool ComDoisFatores => _usuario.HasClaim("amr", "mfa");

    public bool ContaInativa => _usuario.HasClaim(c => c.Type == "conta_inativa");

    /// <summary>Aberto sem internet, sem token: só os dados guardados no aparelho.</summary>
    public bool Offline => TokenDeAcesso is null && _usuario.HasClaim("offline", "true");

    public Guid? UsuarioId => Guid.TryParse(_usuario.FindFirst("sub")?.Value, out var id) ? id : null;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_restaurou)
        {
            _restaurou = true;
            await RenovarAsync();
        }

        return new AuthenticationState(_usuario);
    }

    public void Iniciar(RespostaDeLogin resposta)
    {
        ArgumentNullException.ThrowIfNull(resposta);
        _restaurou = true;
        TokenDeAcesso = resposta.TokenDeAcesso;
        ExpiraEm = resposta.ExpiraEm ?? DateTimeOffset.UtcNow;
        _usuario = TokenDeAcesso is null ? Anonimo : LerClaims(TokenDeAcesso);
        if (_usuario.IsInRole(PapelAluno) && UsuarioId is { } id)
        {
            _ = local.GravarAsync(ChavesLocais.AlunoOffline, id);
        }

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_usuario)));
    }

    public void Encerrar()
    {
        TokenDeAcesso = null;
        _usuario = Anonimo;
        _ = local.ApagarAsync(ChavesLocais.AlunoOffline);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_usuario)));
    }

    /// <summary>Troca o cookie do refresh token por um token de acesso novo. Falso se a sessão acabou.</summary>
    public async Task<bool> RenovarAsync()
    {
        var tokenAntes = TokenDeAcesso;
        await _trava.WaitAsync();
        try
        {
            // Outra chamada já renovou enquanto esta esperava.
            if (TokenDeAcesso is not null && TokenDeAcesso != tokenAntes && !PertoDeVencer())
            {
                return true;
            }

            using var pedido = new HttpRequestMessage(HttpMethod.Post, "auth/refresh");
            pedido.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
            using var resposta = await semToken.SendAsync(pedido);
            if (!resposta.IsSuccessStatusCode)
            {
                if (TokenDeAcesso is not null || Offline)
                {
                    Encerrar();
                }

                return false;
            }

            Iniciar((await resposta.Content.ReadFromJsonAsync<RespostaDeLogin>())!);
            return true;
        }
        catch (HttpRequestException)
        {
            // Sem rede: mantém o que tem (o app continua funcionando offline com os dados locais).
            if (TokenDeAcesso is null && !Offline && await local.LerAsync<Guid?>(ChavesLocais.AlunoOffline) is { } alunoId)
            {
                _usuario = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim("sub", alunoId.ToString()), new Claim("role", PapelAluno), new Claim("offline", "true")],
                    "offline", "sub", "role"));
                NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_usuario)));
            }

            return TokenDeAcesso is not null;
        }
        finally
        {
            _trava.Release();
        }
    }

    public bool PertoDeVencer() => TokenDeAcesso is null || ExpiraEm - DateTimeOffset.UtcNow < TimeSpan.FromSeconds(60);

    /// <summary>Lê as claims do JWT só para a interface (quem é, papéis). Quem valida é a API.</summary>
    private static ClaimsPrincipal LerClaims(string token)
    {
        var partes = token.Split('.');
        var payload = partes[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
        using var json = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));

        var claims = new List<Claim>();
        foreach (var propriedade in json.RootElement.EnumerateObject())
        {
            if (propriedade.Value.ValueKind == JsonValueKind.Array)
            {
                claims.AddRange(propriedade.Value.EnumerateArray().Select(v => new Claim(propriedade.Name, v.ToString())));
            }
            else
            {
                claims.Add(new Claim(propriedade.Name, propriedade.Value.ToString()));
            }
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt", "sub", "role"));
    }
}
