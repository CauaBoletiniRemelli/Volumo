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

    /// <summary>Quando o descanso atual começou (para a barra de progresso).</summary>
    public DateTimeOffset? DescansoDesde { get; set; }

    public List<SerieFeita> Series { get; set; } = [];

    /// <summary>Cardio antes da musculação (escolha feita ao começar). Nulo em sessões de antes do cardio.</summary>
    public bool? CardioAntes { get; set; }

    public List<CardioFeito> Cardios { get; set; } = [];
}

/// <summary>Cardio feito na sessão: <see cref="Cardio"/> aponta para o cardio prescrito.</summary>
public sealed record CardioFeito(Guid Id, int Cardio, Guid ExercicioId, int DuracaoMinutos, DateTimeOffset RealizadoEm);

/// <summary>Série registrada na sessão: <see cref="Item"/> e <see cref="Ordem"/> apontam para a prescrição.</summary>
public sealed record SerieFeita(Guid Id, int Item, int Ordem, Guid ExercicioId, int Repeticoes, decimal CargaKg, int? Rir, DateTimeOffset RealizadaEm);

/// <summary>
/// Treinos do aluno com cópia no aparelho. A cópia vale como cache por <see cref="Validade"/>: dentro
/// do prazo, abrir as telas (ou reabrir o app) não chama a API. Vencida, lê da API e atualiza a cópia;
/// sem conexão usa a cópia, de qualquer idade. Ao buscar a lista na API, também baixa o detalhe de
/// cada treino e as séries da última sessão, para treinar offline.
/// </summary>
public sealed class TreinosDoAluno(ApiVolumo api, ArmazenamentoLocal local, SessaoDoUsuario sessao)
{
    /// <summary>Treino muda pouco (só quando o personal edita); o botão "Atualizar" força a busca.</summary>
    public static readonly TimeSpan Validade = TimeSpan.FromMinutes(15);

    private Guid Usuario => sessao.UsuarioId ?? throw new InvalidOperationException("Sem usuário na sessão.");

    public Task<Carregado<List<TreinoResumo>>> ListarAsync(bool atualizar = false) =>
        CarregarAsync<List<TreinoResumo>>("treinos", ChavesLocais.Treinos(Usuario), atualizar);

    public Task<Carregado<TreinoDetalhe>> DetalheAsync(Guid treinoId, bool atualizar = false) =>
        CarregarAsync<TreinoDetalhe>($"treinos/{treinoId}", ChavesLocais.Treino(Usuario, treinoId), atualizar);

    /// <summary>Histórico guardado no aparelho (atualizado a cada download e a cada treino finalizado aqui).</summary>
    public async Task<List<SessaoResumo>> SessoesAsync() =>
        await local.LerAsync<List<SessaoResumo>>(ChavesLocais.Sessoes(Usuario)) ?? [];

    /// <summary>Põe no histórico do aparelho um treino que acabou de ser finalizado, sem esperar a API.</summary>
    public async Task RegistrarNoHistoricoAsync(SessaoResumo resumo)
    {
        var sessoes = await SessoesAsync();
        sessoes.RemoveAll(s => s.Id == resumo.Id);
        sessoes.Insert(0, resumo);
        await local.GravarAsync(ChavesLocais.Sessoes(Usuario), sessoes);
    }

    /// <summary>
    /// Baixa o detalhe de cada treino e as séries da última sessão de cada um (só quando a última
    /// sessão mudou desde o último download). Devolve as sessões ou nulo se estiver sem conexão.
    /// </summary>
    public async Task<List<SessaoResumo>?> BaixarParaOfflineAsync(IEnumerable<TreinoResumo> treinos)
    {
        var sessoes = await api.GetAsync<List<SessaoResumo>>("sessoes", atualizar: true);
        if (sessoes.Ok)
        {
            await local.GravarAsync(ChavesLocais.Sessoes(Usuario), sessoes.Valor);
        }

        foreach (var treino in treinos)
        {
            await DetalheAsync(treino.Id, atualizar: true);
            var ultima = sessoes.Valor?.Find(s => s.TreinoId == treino.Id && s.FinalizadaEm is not null);
            if (ultima is null || await local.LerAsync<Guid?>(ChavesLocais.UltimaSessaoBaixada(Usuario, treino.Id)) == ultima.Id)
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
                await local.GravarAsync(ChavesLocais.UltimaSessaoBaixada(Usuario, treino.Id), ultima.Id);
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

    private async Task<Carregado<T>> CarregarAsync<T>(string caminho, string chave, bool atualizar)
    {
        // A data da cópia fica numa chave à parte: cópias antigas (sem data) continuam servindo offline.
        var chaveDaData = ChavesLocais.DataDaCopia(chave);
        if (!atualizar && await local.LerAsync<DateTimeOffset?>(chaveDaData) is { } copiadaEm
            && DateTimeOffset.UtcNow - copiadaEm < Validade
            && await local.LerAsync<T>(chave) is { } recente)
        {
            return new Carregado<T>(recente, DoAparelho: true, Erro: null);
        }

        if (!sessao.Offline)
        {
            var resultado = await api.GetAsync<T>(caminho, atualizar: true);
            if (resultado.Ok)
            {
                await local.GravarAsync(chave, resultado.Valor);
                await local.GravarAsync(chaveDaData, DateTimeOffset.UtcNow);
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
