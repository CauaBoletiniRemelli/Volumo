using System.Text.Json;
using Microsoft.JSInterop;

namespace Volumo.Client.Infra;

/// <summary>
/// Dados de treino guardados no aparelho (IndexedDB, via <c>volumo.local</c>) para o app funcionar
/// sem internet. Nunca guarda token. Se o navegador recusar (janela privada, espaço cheio), as
/// leituras voltam vazias e o app segue funcionando, só que sem modo offline.
/// </summary>
public sealed class ArmazenamentoLocal(IJSRuntime js)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<T?> LerAsync<T>(string chave)
    {
        try
        {
            var texto = await js.InvokeAsync<string?>("volumo.local.ler", chave);
            return texto is null ? default : JsonSerializer.Deserialize<T>(texto, Json);
        }
        catch (Exception e) when (e is JSException or JsonException)
        {
            return default;
        }
    }

    public async Task GravarAsync<T>(string chave, T valor)
    {
        try
        {
            await js.InvokeVoidAsync("volumo.local.gravar", chave, JsonSerializer.Serialize(valor, Json));
        }
        catch (JSException)
        {
            // Sem armazenamento: o dado fica só na memória desta aba.
        }
    }

    public async Task ApagarAsync(string chave)
    {
        try
        {
            await js.InvokeVoidAsync("volumo.local.apagar", chave);
        }
        catch (JSException)
        {
        }
    }

    public async Task ApagarPrefixoAsync(string prefixo)
    {
        try
        {
            await js.InvokeVoidAsync("volumo.local.apagarPrefixo", prefixo);
        }
        catch (JSException)
        {
        }
    }
}

/// <summary>Chaves do armazenamento local. Tudo por usuário; "cache" é apagado ao sair.</summary>
public static class ChavesLocais
{
    /// <summary>Último aluno que entrou neste aparelho (só id), para abrir o app sem internet.</summary>
    public const string AlunoOffline = "aluno-offline";

    public static string Cache(Guid usuarioId) => $"{usuarioId}:cache:";

    public static string Treinos(Guid usuarioId) => $"{Cache(usuarioId)}treinos";

    public static string Treino(Guid usuarioId, Guid treinoId) => $"{Cache(usuarioId)}treino:{treinoId}";

    public static string UltimasSeries(Guid usuarioId, Guid treinoId) => $"{Cache(usuarioId)}ultimas:{treinoId}";

    /// <summary>Id da sessão de onde vieram as <see cref="UltimasSeries"/> (evita baixar de novo a mesma).</summary>
    public static string UltimaSessaoBaixada(Guid usuarioId, Guid treinoId) => $"{Cache(usuarioId)}ultima-sessao:{treinoId}";

    public static string Sessoes(Guid usuarioId) => $"{Cache(usuarioId)}sessoes";

    /// <summary>Quando a cópia guardada em <paramref name="chave"/> veio da API.</summary>
    public static string DataDaCopia(string chave) => $"{chave}:em";

    public static string SessaoEmAndamento(Guid usuarioId) => $"{usuarioId}:em-andamento";

    public static string Fila(Guid usuarioId) => $"{usuarioId}:fila";

    public static string Recusadas(Guid usuarioId) => $"{usuarioId}:recusadas";
}
