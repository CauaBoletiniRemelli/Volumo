using Microsoft.EntityFrameworkCore;
using Volumo.Domain.Treinos;
using Volumo.Infrastructure.Persistencia;
using Volumo.Shared.Treinos;
using DomainGroup = Volumo.Domain.Exercicios.GrupoMuscular;

namespace Volumo.Api.Treinos;

/// <summary>Converte treinos e templates entre os contratos da API e o domínio.</summary>
public sealed class PlanMapper(VolumoDbContext db)
{
    public const string UnavailableExercise = "Exercício indisponível";

    /// <summary>
    /// Itens do domínio a partir do que o construtor enviou. Devolve <c>null</c> se algum exercício
    /// não existe, está inativo ou não é visível para quem está montando (de outro personal).
    /// Séries inválidas lançam a exceção de regra de negócio (vira 400 com a mensagem).
    /// </summary>
    public async Task<IReadOnlyList<DefinicaoDeItem>?> ToDefinitionsAsync(PlanoDeTreinoEntrada entrada, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        var ids = entrada.Itens.Select(i => i.ExercicioId).Distinct().ToList();
        var visible = await db.Exercicios.CountAsync(e => ids.Contains(e.Id), ct);
        if (visible != ids.Count)
        {
            return null;
        }

        return [.. entrada.Itens.Select(i => new DefinicaoDeItem(i.ExercicioId, [.. i.Series.Select(ToDomain)], i.Observacao))];
    }

    /// <summary>
    /// Itens para a resposta, com o nome de cada exercício. Exercício que foi inativado depois continua
    /// aparecendo pelo nome; se não for mais visível (personal de outro vínculo), aparece como indisponível.
    /// </summary>
    public async Task<IReadOnlyList<ItemDoPlanoSaida>> ToOutputAsync(PlanoDeTreino plano, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plano);
        var ids = plano.Itens.Select(i => i.ExercicioId).Distinct().ToList();
        var names = await db.Exercicios.IgnoreQueryFilters([FiltrosDeConsulta.Ativos])
            .Where(e => ids.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Nome, ct);

        return [.. plano.Itens.Select(i => new ItemDoPlanoSaida(
            i.Id,
            i.Ordem,
            i.ExercicioId,
            names.GetValueOrDefault(i.ExercicioId, UnavailableExercise),
            i.Observacao,
            [.. i.Series.Select(ToContract)]))];
    }

    public static DomainGroup ToDomain(GrupoMuscular grupo) => Enum.Parse<DomainGroup>(grupo.ToString());

    public static GrupoMuscular ToContract(DomainGroup grupo) => Enum.Parse<GrupoMuscular>(grupo.ToString());

    private static Serie ToDomain(SeriePrescrita serie) => Serie.Criar(
        Enum.Parse<TipoSerie>(serie.Tipo.ToString()),
        serie.RepeticoesMinimas,
        serie.RepeticoesMaximas,
        serie.DescansoSegundos,
        serie.CargaKg);

    private static SeriePrescrita ToContract(Serie serie) => new(
        Enum.Parse<TipoDeSerie>(serie.Tipo.ToString()),
        serie.RepeticoesMinimas,
        serie.RepeticoesMaximas,
        serie.DescansoSegundos,
        serie.CargaKg);
}
