using Volumo.Shared.Sessoes;
using Volumo.Shared.Treinos;

namespace Volumo.Client.Infra;

/// <summary>Resultado lido da API ou, sem conexão, da cópia guardada no aparelho.</summary>
public sealed record Carregado<T>(T? Valor, bool DoAparelho, string? Erro);

/// <summary>Carga e repetições feitas na última vez, por exercício e na ordem das séries.</summary>
public sealed record SerieAnterior(int Repeticoes, decimal CargaKg);

/// <summary>Treino em execução, gravado no aparelho a cada série (fechar o app não perde nada).</summary>
public sealed class SessaoEmAndamento
{
    public Guid Id { get; set; }

    public Guid TreinoId { get; set; }

    public DateTimeOffset IniciadaEm { get; set; }

    public int ExercicioAtual { get; set; }

    public DateTimeOffset? DescansoAte { get; set; }

    public List<SerieFeita> Series { get; set; } = [];
}

/// <summary>Série registrada na sessão: <see cref="Item"/> e <see cref="Ordem"/> apontam para a prescrição.</summary>
public sealed record SerieFeita(Guid Id, int Item, int Ordem, Guid ExercicioId, int Repeticoes, decimal CargaKg, int? Rir, DateTimeOffset RealizadaEm);

/// <summary>
/// Treinos do aluno com cópia no aparelho: online lê da API e atualiza a cópia; sem conexão usa a
/// cópia. Ao abrir a lista com internet, já baixa o detalhe de cada treino para usar offline.
/// </summary>
public sealed class TreinosDoAluno(ApiVolumo api, ArmazenamentoLocal local, SessaoDoUsuario sessao)
{
    private Guid Usuario => sessao.UsuarioId ?? throw new InvalidOperationException("Sem usuário na sessão.");

    public Task<Carregado<List<TreinoResumo>>> ListarAsync() =>
        CarregarAsync<List<TreinoResumo>>("treinos", ChavesLocais.Treinos(Usuario));

    public Task<Carregado<TreinoDetalhe>> DetalheAsync(Guid treinoId) =>
        CarregarAsync<TreinoDetalhe>($"treinos/{treinoId}", ChavesLocais.Treino(Usuario, treinoId));

    /// <summary>
    /// Guarda o detalhe de cada treino e as séries da última sessão de cada um. Devolve as últimas
    /// sessões (para a tela mostrar o histórico) ou nulo se estiver sem conexão.
    /// </summary>
    public async Task<List<SessaoResumo>?> BaixarParaOfflineAsync(IEnumerable<TreinoResumo> treinos)
    {
        var sessoes = await api.GetAsync<List<SessaoResumo>>("sessoes");
        foreach (var treino in treinos)
        {
            await DetalheAsync(treino.Id);
            var ultima = sessoes.Valor?.Find(s => s.TreinoId == treino.Id && s.FinalizadaEm is not null);
            if (ultima is null)
            {
                continue;
            }

            var detalhe = await api.GetAsync<SessaoDetalhe>($"sessoes/{ultima.Id}");
            if (detalhe.Ok)
            {
                await GuardarUltimasSeriesAsync(treino.Id, detalhe.Valor!.Series
                    .OrderBy(s => s.RealizadaEm)
                    .GroupBy(s => s.ExercicioId)
                    .ToDictionary(g => g.Key, g => g.Select(s => new SerieAnterior(s.Repeticoes, s.CargaKg)).ToList()));
            }
        }

        return sessoes.Valor;
    }

    public async Task<Dictionary<Guid, List<SerieAnterior>>> UltimasSeriesAsync(Guid treinoId) =>
        await local.LerAsync<Dictionary<Guid, List<SerieAnterior>>>(ChavesLocais.UltimasSeries(Usuario, treinoId)) ?? [];

    public Task GuardarUltimasSeriesAsync(Guid treinoId, Dictionary<Guid, List<SerieAnterior>> series) =>
        local.GravarAsync(ChavesLocais.UltimasSeries(Usuario, treinoId), series);

    public Task<SessaoEmAndamento?> EmAndamentoAsync() => local.LerAsync<SessaoEmAndamento>(ChavesLocais.SessaoEmAndamento(Usuario));

    public Task GuardarEmAndamentoAsync(SessaoEmAndamento sessaoEmAndamento) =>
        local.GravarAsync(ChavesLocais.SessaoEmAndamento(Usuario), sessaoEmAndamento);

    public Task DescartarEmAndamentoAsync() => local.ApagarAsync(ChavesLocais.SessaoEmAndamento(Usuario));

    /// <summary>Ao sair: apaga as cópias de treinos deste usuário. A fila de envio fica.</summary>
    public Task LimparCopiasAsync() => local.ApagarPrefixoAsync(ChavesLocais.Cache(Usuario));

    private async Task<Carregado<T>> CarregarAsync<T>(string caminho, string chave)
    {
        if (!sessao.Offline)
        {
            var resultado = await api.GetAsync<T>(caminho);
            if (resultado.Ok)
            {
                await local.GravarAsync(chave, resultado.Valor);
                return new Carregado<T>(resultado.Valor, DoAparelho: false, Erro: null);
            }

            if (resultado.Status != 0)
            {
                return new Carregado<T>(default, DoAparelho: false, resultado.Erro);
            }
        }

        var copia = await local.LerAsync<T>(chave);
        return copia is null
            ? new Carregado<T>(default, DoAparelho: true, "Sem conexão, e este conteúdo ainda não foi baixado para o aparelho.")
            : new Carregado<T>(copia, DoAparelho: true, Erro: null);
    }
}
