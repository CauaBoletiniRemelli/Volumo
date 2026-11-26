using Microsoft.JSInterop;
using Volumo.Shared.Sessoes;

namespace Volumo.Client.Infra;

/// <summary>Sessão que a API recusou de vez (dado inválido): sai da fila e o aluno é avisado.</summary>
public sealed record SessaoRecusada(Guid Id, DateTimeOffset IniciadaEm, string Motivo);

/// <summary>
/// Fila de sessões finalizadas no aparelho, enviada para <c>/sessoes/sincronizar</c> em lotes de até
/// 20. Envia ao finalizar, ao abrir o app, quando a internet volta e pelo botão "Enviar agora".
/// Reenviar é seguro: os Ids são gerados aqui e a API não duplica.
/// </summary>
public sealed class SincronizacaoDeSessoes(ApiVolumo api, ArmazenamentoLocal local, SessaoDoUsuario sessao, IJSRuntime js)
{
    private const int TamanhoDoLote = 20;
    private readonly SemaphoreSlim _enviando = new(1, 1);
    private DotNetObjectReference<SincronizacaoDeSessoes>? _referencia;

    public int Pendentes { get; private set; }

    public IReadOnlyList<SessaoRecusada> Recusadas { get; private set; } = [];

    public bool SemConexao { get; private set; }

    /// <summary>A contagem ou a conexão mudou: as telas redesenham o indicador.</summary>
    public event Action? Mudou;

    public async Task IniciarAsync()
    {
        if (_referencia is null)
        {
            _referencia = DotNetObjectReference.Create(this);
            try
            {
                await js.InvokeVoidAsync("volumo.conexao.observar", _referencia);
            }
            catch (JSException)
            {
            }
        }

        await EnviarAsync();
    }

    [JSInvokable]
    public async Task MudouConexao(bool online)
    {
        SemConexao = !online;
        Mudou?.Invoke();
        if (online)
        {
            await EnviarAsync();
        }
    }

    public async Task AdicionarAsync(SessaoRegistrada sessaoFinalizada)
    {
        if (sessao.UsuarioId is not { } usuario)
        {
            return;
        }

        var fila = await local.LerAsync<List<SessaoRegistrada>>(ChavesLocais.Fila(usuario)) ?? [];
        fila.RemoveAll(s => s.Id == sessaoFinalizada.Id);
        fila.Add(sessaoFinalizada);
        await local.GravarAsync(ChavesLocais.Fila(usuario), fila);
        await EnviarAsync();
    }

    public async Task EnviarAsync()
    {
        if (sessao.UsuarioId is not { } usuario || !await _enviando.WaitAsync(0))
        {
            return;
        }

        try
        {
            var fila = await local.LerAsync<List<SessaoRegistrada>>(ChavesLocais.Fila(usuario)) ?? [];
            var recusadas = await local.LerAsync<List<SessaoRecusada>>(ChavesLocais.Recusadas(usuario)) ?? [];

            while (fila.Count > 0)
            {
                // Aberto offline: precisa recuperar a sessão pelo cookie antes de enviar.
                if (sessao.Offline && !await sessao.RenovarAsync())
                {
                    SemConexao = sessao.Offline;
                    break;
                }

                var lote = fila.Take(TamanhoDoLote).ToList();
                var resultado = await api.PostAsync<ResultadoDaSincronizacao>("sessoes/sincronizar", new LoteDeSessoes(lote));
                SemConexao = resultado.Status == 0;
                if (!resultado.Ok)
                {
                    break;
                }

                foreach (var item in resultado.Valor!.Sessoes)
                {
                    fila.RemoveAll(s => s.Id == item.Id);
                    if (item.Situacao == SituacaoDaSincronizacao.Recusada)
                    {
                        var original = lote.Find(s => s.Id == item.Id);
                        recusadas.Add(new SessaoRecusada(item.Id, original?.IniciadaEm ?? DateTimeOffset.Now, item.Motivo ?? "Recusada pelo servidor."));
                    }
                }

                await local.GravarAsync(ChavesLocais.Fila(usuario), fila);
                await local.GravarAsync(ChavesLocais.Recusadas(usuario), recusadas);
                if (lote.TrueForAll(fila.Contains))
                {
                    break; // resposta sem nenhuma sessão do lote: tenta de novo mais tarde
                }
            }

            Pendentes = fila.Count;
            Recusadas = recusadas;
        }
        finally
        {
            _enviando.Release();
            Mudou?.Invoke();
        }
    }

    public async Task DispensarRecusadasAsync()
    {
        if (sessao.UsuarioId is { } usuario)
        {
            await local.ApagarAsync(ChavesLocais.Recusadas(usuario));
        }

        Recusadas = [];
        Mudou?.Invoke();
    }
}
