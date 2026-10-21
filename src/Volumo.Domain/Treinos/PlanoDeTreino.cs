using Volumo.Domain.Comum;

namespace Volumo.Domain.Treinos;

/// <summary>
/// Base comum de <see cref="TemplateTreino"/> e <see cref="Treino"/>: nome e lista ordenada de exercícios.
/// Os itens só mudam por aqui, para a ordem ficar sempre contínua (1, 2, 3...) e para um plano
/// inativo não poder ser alterado.
/// </summary>
public abstract class PlanoDeTreino : EntidadeInativavel
{
    public const int NomeMaximo = 100;
    public const int MaximoItens = 40;
    public const int MaximoCardios = 10;

    private readonly List<ItemTreino> _itens = [];
    private readonly List<ItemCardio> _cardios = [];

    protected PlanoDeTreino()
    {
    }

    protected PlanoDeTreino(Guid id, string nome)
        : base(id) => Renomear(nome);

    public string Nome { get; private set; } = null!;

    public IReadOnlyList<ItemTreino> Itens => [.. _itens.OrderBy(i => i.Ordem)];

    /// <summary>Cardio do treino, separado da musculação.</summary>
    public IReadOnlyList<ItemCardio> Cardios => [.. _cardios.OrderBy(c => c.Ordem)];

    /// <summary>Quando o personal recomenda fazer o cardio (antes ou depois da musculação).</summary>
    public MomentoDoCardio MomentoDoCardio { get; private set; } = MomentoDoCardio.Depois;

    /// <summary>Troca todo o cardio e o momento recomendado. Tudo é validado antes: se algo for inválido, nada muda.</summary>
    public void SubstituirCardios(IReadOnlyList<DefinicaoDeCardio> cardios, MomentoDoCardio momento)
    {
        ArgumentNullException.ThrowIfNull(cardios);
        GarantirAtivo();
        Guarda.EnumValido(momento, "Momento do cardio");
        if (cardios.Count > MaximoCardios)
        {
            throw new RegraDeNegocioException($"Um treino pode ter no máximo {MaximoCardios} cardios.");
        }

        var novos = cardios
            .Select((c, indice) => new ItemCardio(Guid.CreateVersion7(), indice + 1, c.ExercicioId, c.DuracaoMinutos, c.Observacao))
            .ToList();
        _cardios.Clear();
        _cardios.AddRange(novos);
        MomentoDoCardio = momento;
    }

    public void Renomear(string nome)
    {
        GarantirAtivo();
        Nome = Guarda.Texto(nome, "Nome do treino", NomeMaximo);
    }

    public ItemTreino AdicionarItem(Guid exercicioId, IEnumerable<Serie> series, string? observacao = null)
    {
        GarantirAtivo();
        if (_itens.Count >= MaximoItens)
        {
            throw new RegraDeNegocioException($"Um treino pode ter no máximo {MaximoItens} exercícios.");
        }

        var item = new ItemTreino(Guid.CreateVersion7(), _itens.Count + 1, exercicioId, series, observacao);
        _itens.Add(item);
        return item;
    }

    /// <summary>
    /// Troca todos os exercícios de uma vez, na ordem recebida (o construtor de treino envia o
    /// treino inteiro). Tudo é validado antes: se algum item for inválido, nada muda.
    /// </summary>
    public void SubstituirItens(IReadOnlyList<DefinicaoDeItem> itens)
    {
        ArgumentNullException.ThrowIfNull(itens);
        GarantirAtivo();
        if (itens.Count > MaximoItens)
        {
            throw new RegraDeNegocioException($"Um treino pode ter no máximo {MaximoItens} exercícios.");
        }

        var novos = itens
            .Select((item, indice) => new ItemTreino(Guid.CreateVersion7(), indice + 1, item.ExercicioId, item.Series, item.Observacao))
            .ToList();
        _itens.Clear();
        _itens.AddRange(novos);
    }

    public ItemTreino ObterItem(Guid itemId) =>
        _itens.Find(i => i.Id == itemId) ?? throw new RegraDeNegocioException("Exercício não encontrado neste treino.");

    public void DefinirSeries(Guid itemId, IEnumerable<Serie> series)
    {
        GarantirAtivo();
        ObterItem(itemId).DefinirSeries(series);
    }

    public void DefinirObservacao(Guid itemId, string? observacao)
    {
        GarantirAtivo();
        ObterItem(itemId).DefinirObservacao(observacao);
    }

    public void RemoverItem(Guid itemId)
    {
        GarantirAtivo();
        _itens.Remove(ObterItem(itemId));
        Renumerar([.. _itens.OrderBy(i => i.Ordem)]);
    }

    public void ReordenarItens(IReadOnlyList<Guid> idsNaNovaOrdem)
    {
        ArgumentNullException.ThrowIfNull(idsNaNovaOrdem);
        GarantirAtivo();

        var mesmosItens = idsNaNovaOrdem.Count == _itens.Count
            && idsNaNovaOrdem.Distinct().Count() == _itens.Count
            && idsNaNovaOrdem.All(id => _itens.Exists(i => i.Id == id));
        if (!mesmosItens)
        {
            throw new RegraDeNegocioException("A nova ordem precisa conter cada exercício do treino exatamente uma vez.");
        }

        Renumerar([.. idsNaNovaOrdem.Select(ObterItem)]);
    }

    protected void CopiarItensDe(PlanoDeTreino origem)
    {
        ArgumentNullException.ThrowIfNull(origem);
        _itens.AddRange(origem.Itens.Select(i => i.Copiar()));
        _cardios.AddRange(origem.Cardios.Select(c => c.Copiar()));
        MomentoDoCardio = origem.MomentoDoCardio;
    }

    private static void Renumerar(List<ItemTreino> itensEmOrdem)
    {
        for (var i = 0; i < itensEmOrdem.Count; i++)
        {
            itensEmOrdem[i].Ordem = i + 1;
        }
    }
}
