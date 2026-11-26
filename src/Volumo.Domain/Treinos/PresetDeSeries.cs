using Volumo.Domain.Comum;

namespace Volumo.Domain.Treinos;

/// <summary>
/// Esquema de séries com nome ("Força 5 × 5", "Pirâmide crescente"...) para aplicar num exercício do
/// construtor de uma vez. Guarda só a estrutura (tipo, repetições e descanso): a carga depende do
/// exercício e de quem treina. Aplicar copia as séries; mudar o preset depois não altera treinos.
/// Os prontos da plataforma (<see cref="Prontos"/>) ficam no código, sem dono; os próprios são de
/// quem montou (personal ou aluno).
/// </summary>
public sealed class PresetDeSeries : EntidadeInativavel
{
    public const int NomeMaximo = 60;

    private readonly List<Serie> _series = [];

    private PresetDeSeries()
    {
    }

    private PresetDeSeries(Guid id, Guid? donoId, string nome, IEnumerable<Serie> series)
        : base(id)
    {
        DonoId = donoId is { } dono ? Guarda.Referencia(dono, "Dono") : null;
        Renomear(nome);
        DefinirSeries(series);
    }

    /// <summary>Usuário que montou o preset; nulo nos prontos da plataforma.</summary>
    public Guid? DonoId { get; private set; }

    public string Nome { get; private set; } = null!;

    public IReadOnlyList<Serie> Series => [.. _series.OrderBy(s => s.Ordem)];

    public bool Pronto => DonoId is null;

    /// <summary>Prontos da plataforma, na ordem em que aparecem. Ids fixos: não trocar.</summary>
    public static IReadOnlyList<PresetDeSeries> Prontos { get; } =
    [
        PresetPronto("019a2f00-0000-7000-8000-000000000001", "Clássico 3 × 10", Trabalho(3, 10, 10, 90)),
        PresetPronto("019a2f00-0000-7000-8000-000000000002", "Hipertrofia 4 × 8–12", Trabalho(4, 8, 12, 90)),
        PresetPronto("019a2f00-0000-7000-8000-000000000003", "Força 5 × 5", Trabalho(5, 5, 5, 180)),
        PresetPronto("019a2f00-0000-7000-8000-000000000004", "Pirâmide crescente", [.. new[] { 10, 8, 5, 3 }.Select(reps => Serie.Criar(TipoSerie.Trabalho, reps, reps, 120))]),
        PresetPronto("019a2f00-0000-7000-8000-000000000005", "Resistência 3 × 15–20", Trabalho(3, 15, 20, 60)),
        PresetPronto(
            "019a2f00-0000-7000-8000-000000000006",
            "Com aquecimento",
            [Serie.Criar(TipoSerie.Aquecimento, 15, 15, 60), Serie.Criar(TipoSerie.Preparatoria, 5, 5, 90), .. Trabalho(3, 8, 12, 90)]),
    ];

    public static PresetDeSeries Criar(Guid id, Guid donoId, string nome, IEnumerable<Serie> series) => new(id, donoId, nome, series);

    public void Renomear(string nome)
    {
        GarantirAtivo();
        Nome = Guarda.Texto(nome, "Nome do preset", NomeMaximo);
    }

    /// <summary>Troca todas as séries. Mesmos limites do exercício no treino (1 a 20) e sem carga.</summary>
    public void DefinirSeries(IEnumerable<Serie> series)
    {
        ArgumentNullException.ThrowIfNull(series);
        GarantirAtivo();

        var novas = series.Select((serie, indice) => serie.NaPosicao(indice + 1)).ToList();
        if (novas.Count == 0)
        {
            throw new RegraDeNegocioException("O preset precisa de pelo menos uma série.");
        }

        if (novas.Count > ItemTreino.MaximoSeries)
        {
            throw new RegraDeNegocioException($"Um preset pode ter no máximo {ItemTreino.MaximoSeries} séries.");
        }

        if (novas.Exists(s => s.CargaKg is not null))
        {
            throw new RegraDeNegocioException("O preset guarda só as séries, sem carga: a carga fica no treino.");
        }

        _series.Clear();
        _series.AddRange(novas);
    }

    private static PresetDeSeries PresetPronto(string id, string nome, IReadOnlyList<Serie> series) => new(Guid.Parse(id), null, nome, series);

    private static IReadOnlyList<Serie> Trabalho(int quantidade, int repeticoesMinimas, int repeticoesMaximas, int descansoSegundos) =>
        [.. Enumerable.Range(0, quantidade).Select(_ => Serie.Criar(TipoSerie.Trabalho, repeticoesMinimas, repeticoesMaximas, descansoSegundos))];
}
