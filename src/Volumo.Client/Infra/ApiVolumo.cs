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
/// Leituras (GET) ficam em cache na memória da aba por <see cref="Validade"/>: voltar a uma tela já
/// vista não chama a API de novo. Qualquer gravação bem-sucedida (POST/PUT) limpa o cache, então o
/// que a própria pessoa muda aparece na hora; o que outras pessoas mudam aparece ao vencer o prazo ou
/// no botão "Atualizar". Trocar de usuário também limpa. Nada disso vai para o disco.
/// </summary>
public sealed class ApiVolumo
{
    public const string SemConexao = "Sem conexão com o servidor. Confira a internet e tente de novo.";
    public static readonly TimeSpan Validade = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly Dictionary<string, (string Json, DateTimeOffset Em)> _cache = new(StringComparer.Ordinal);
    private Guid? _usuarioDoCache;

    public ApiVolumo(HttpClient http, SessaoDoUsuario sessao)
    {
        ArgumentNullException.ThrowIfNull(sessao);
        _http = http;
        sessao.AuthenticationStateChanged += _ =>
        {
            if (sessao.UsuarioId != _usuarioDoCache)
            {
                LimparCache();
                _usuarioDoCache = sessao.UsuarioId;
            }
        };
    }

    /// <param name="atualizar">Ignora o cache e busca de novo (botão "Atualizar").</param>
    public async Task<Resultado<T>> GetAsync<T>(string caminho, bool atualizar = false)
    {
        // O valor fica guardado como JSON: cada tela recebe uma cópia nova e pode alterá-la à vontade.
        if (!atualizar && _cache.TryGetValue(caminho, out var guardado) && DateTimeOffset.UtcNow - guardado.Em < Validade)
        {
            return new Resultado<T>(JsonSerializer.Deserialize<T>(guardado.Json, Json), null, HttpStatusCode.OK);
        }

        var resultado = await EnviarAsync<T>(HttpMethod.Get, caminho, corpo: null);
        if (resultado.Ok && resultado.Valor is not null)
        {
            _cache[caminho] = (JsonSerializer.Serialize(resultado.Valor, Json), DateTimeOffset.UtcNow);
        }

        return resultado;
    }

    public Task<Resultado<T>> PostAsync<T>(string caminho, object? corpo = null) => EnviarAsync<T>(HttpMethod.Post, caminho, corpo);

    public Task<Resultado<Vazio>> PostAsync(string caminho, object? corpo = null) => EnviarAsync<Vazio>(HttpMethod.Post, caminho, corpo);

    public Task<Resultado<Vazio>> PutAsync(string caminho, object corpo) => EnviarAsync<Vazio>(HttpMethod.Put, caminho, corpo);

    /// <summary>Envia um arquivo no campo "arquivo" (multipart). A API confere o conteúdo real.</summary>
    public Task<Resultado<T>> EnviarArquivoAsync<T>(string caminho, Stream conteudo, string nomeDoArquivo, string tipoDeConteudo)
    {
        var arquivo = new StreamContent(conteudo);
        arquivo.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(string.IsNullOrEmpty(tipoDeConteudo) ? "application/octet-stream" : tipoDeConteudo);
        return EnviarAsync<T>(HttpMethod.Post, caminho, new MultipartFormDataContent { { arquivo, "arquivo", nomeDoArquivo } });
    }

    /// <summary>Baixa um arquivo (exportação em Excel ou PDF). Não passa pelo cache.</summary>
    public async Task<Resultado<byte[]>> BaixarAsync(string caminho)
    {
        try
        {
            using var resposta = await _http.GetAsync(caminho);
            return resposta.IsSuccessStatusCode
                ? new Resultado<byte[]>(await resposta.Content.ReadAsByteArrayAsync(), null, resposta.StatusCode)
                : new Resultado<byte[]>(null, await LerErroAsync(resposta), resposta.StatusCode);
        }
        catch (HttpRequestException)
        {
            return new Resultado<byte[]>(null, SemConexao, 0);
        }
    }

    public void LimparCache() => _cache.Clear();

    private async Task<Resultado<T>> EnviarAsync<T>(HttpMethod metodo, string caminho, object? corpo)
    {
        try
        {
            using var pedido = new HttpRequestMessage(metodo, caminho);
            if (corpo is HttpContent conteudo)
            {
                pedido.Content = conteudo;
            }
            else if (corpo is not null)
            {
                pedido.Content = JsonContent.Create(corpo, corpo.GetType());
            }

            using var resposta = await _http.SendAsync(pedido);
            if (!resposta.IsSuccessStatusCode)
            {
                return new Resultado<T>(default, await LerErroAsync(resposta), resposta.StatusCode);
            }

            if (metodo != HttpMethod.Get)
            {
                LimparCache();
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
