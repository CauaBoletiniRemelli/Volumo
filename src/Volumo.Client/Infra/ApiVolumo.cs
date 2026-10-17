using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Volumo.Client.Infra;

/// <summary>Resultado de uma chamada: o valor ou a mensagem de erro já pronta para mostrar.</summary>
public sealed record Resultado<T>(T? Valor, string? Erro, HttpStatusCode Status)
{
    public bool Ok => Erro is null;
}

/// <summary>
/// Chamadas à API com tratamento de erro uniforme: lê o ProblemDetails e devolve a mensagem em
/// português que a API já escreve para o usuário. Sem rede, devolve uma mensagem de conexão.
/// </summary>
public sealed class ApiVolumo(HttpClient http)
{
    public const string SemConexao = "Sem conexão com o servidor. Confira a internet e tente de novo.";

    public Task<Resultado<T>> GetAsync<T>(string caminho) => EnviarAsync<T>(HttpMethod.Get, caminho, corpo: null);

    public Task<Resultado<T>> PostAsync<T>(string caminho, object? corpo = null) => EnviarAsync<T>(HttpMethod.Post, caminho, corpo);

    public Task<Resultado<Vazio>> PostAsync(string caminho, object? corpo = null) => EnviarAsync<Vazio>(HttpMethod.Post, caminho, corpo);

    public Task<Resultado<Vazio>> PutAsync(string caminho, object corpo) => EnviarAsync<Vazio>(HttpMethod.Put, caminho, corpo);

    private async Task<Resultado<T>> EnviarAsync<T>(HttpMethod metodo, string caminho, object? corpo)
    {
        try
        {
            using var pedido = new HttpRequestMessage(metodo, caminho);
            if (corpo is not null)
            {
                pedido.Content = JsonContent.Create(corpo, corpo.GetType());
            }

            using var resposta = await http.SendAsync(pedido);
            if (!resposta.IsSuccessStatusCode)
            {
                return new Resultado<T>(default, await LerErroAsync(resposta), resposta.StatusCode);
            }

            if (typeof(T) == typeof(Vazio) || resposta.StatusCode == HttpStatusCode.NoContent || resposta.Content.Headers.ContentLength == 0)
            {
                return new Resultado<T>(default, null, resposta.StatusCode);
            }

            return new Resultado<T>(await resposta.Content.ReadFromJsonAsync<T>(), null, resposta.StatusCode);
        }
        catch (HttpRequestException)
        {
            return new Resultado<T>(default, SemConexao, 0);
        }
    }

    private static async Task<string> LerErroAsync(HttpResponseMessage resposta)
    {
        try
        {
            using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
            var raiz = json.RootElement;
            if (raiz.TryGetProperty("errors", out var erros) && erros.ValueKind == JsonValueKind.Object)
            {
                var primeiro = erros.EnumerateObject().SelectMany(e => e.Value.EnumerateArray()).Select(v => v.GetString()).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(primeiro))
                {
                    return primeiro;
                }
            }

            var titulo = raiz.TryGetProperty("title", out var t) ? t.GetString() : null;
            var detalhe = raiz.TryGetProperty("detail", out var d) ? d.GetString() : null;
            return detalhe is null ? titulo ?? Generica(resposta.StatusCode) : $"{titulo} {detalhe}".Trim();
        }
        catch (JsonException)
        {
            return Generica(resposta.StatusCode);
        }
    }

    private static string Generica(HttpStatusCode status) => status switch
    {
        HttpStatusCode.TooManyRequests => "Muitas tentativas. Espere um pouco e tente de novo.",
        HttpStatusCode.Unauthorized => "Sua sessão terminou. Entre de novo.",
        HttpStatusCode.Forbidden => "Você não tem acesso a isso.",
        HttpStatusCode.NotFound => "Não encontrado.",
        _ => "Algo deu errado. Tente de novo em instantes.",
    };
}

/// <summary>Resposta sem corpo.</summary>
public sealed record Vazio;
