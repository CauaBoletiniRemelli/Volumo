using Volumo.Shared.Autenticacao;
using Volumo.Shared.Treinos;

namespace Volumo.Client.Infra;

/// <summary>
/// Desenhos do corpo (frente e costas) em <c>wwwroot/img/corpo</c>, gerados a partir do
/// react-native-body-highlighter (MIT). Cada músculo é um grupo <c>class="m-{slug}"</c>; o mapa pinta
/// os do exercício trocando a classe. Os arquivos só são baixados quando um mapa aparece e ficam na
/// memória da aba (e no cache do service worker, como o resto do site).
/// </summary>
public sealed class DesenhosDoCorpo(HttpClient site)
{
    private readonly Dictionary<string, Task<string?>> _svgs = new(StringComparer.Ordinal);

    /// <summary>Áreas do desenho de cada grupo muscular. Cardio e abdutores não têm área própria.</summary>
    public static IReadOnlyList<string> Areas(GrupoMuscular grupo) => grupo switch
    {
        GrupoMuscular.Peitoral => ["chest"],
        GrupoMuscular.Costas => ["upper-back"],
        GrupoMuscular.Ombros => ["deltoids"],
        GrupoMuscular.Biceps => ["biceps"],
        GrupoMuscular.Triceps => ["triceps"],
        GrupoMuscular.Antebraco => ["forearm"],
        GrupoMuscular.Trapezio => ["trapezius"],
        GrupoMuscular.Abdomen => ["abs", "obliques"],
        GrupoMuscular.Lombar => ["lower-back"],
        GrupoMuscular.Quadriceps => ["quadriceps"],
        GrupoMuscular.Posteriores => ["hamstring"],
        GrupoMuscular.Gluteos => ["gluteal"],
        GrupoMuscular.Panturrilhas => ["calves"],
        GrupoMuscular.Adutores => ["adductors"],
        GrupoMuscular.CorpoInteiro =>
            ["chest", "upper-back", "deltoids", "biceps", "triceps", "forearm", "trapezius", "abs", "obliques", "lower-back", "quadriceps", "hamstring", "gluteal", "calves", "adductors"],
        _ => [],
    };

    /// <summary>
    /// SVG de um lado do corpo com as áreas marcadas como "principal" ou "secundario". Nulo se o
    /// arquivo não carregar (sem internet e fora do cache).
    /// </summary>
    public async Task<string?> DesenharAsync(CorpoDoDesenho corpo, bool costas, IReadOnlySet<string> principais, IReadOnlySet<string> secundarias)
    {
        var arquivo = $"img/corpo/{(corpo == CorpoDoDesenho.Feminino ? "feminino" : "masculino")}-{(costas ? "costas" : "frente")}.svg";
        if (!_svgs.TryGetValue(arquivo, out var carregando))
        {
            carregando = BaixarAsync(arquivo);
            _svgs[arquivo] = carregando;
        }

        var svg = await carregando;
        if (svg is null)
        {
            _svgs.Remove(arquivo);
            return null;
        }

        foreach (var area in secundarias.Except(principais))
        {
            svg = svg.Replace($"class=\"m-{area}\"", $"class=\"m-{area} secundario\"", StringComparison.Ordinal);
        }

        foreach (var area in principais)
        {
            svg = svg.Replace($"class=\"m-{area}\"", $"class=\"m-{area} principal\"", StringComparison.Ordinal);
        }

        return svg;
    }

    private async Task<string?> BaixarAsync(string arquivo)
    {
        try
        {
            return await site.GetStringAsync(arquivo);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}
