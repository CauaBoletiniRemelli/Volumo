using Microsoft.EntityFrameworkCore;
using Volumo.Domain.Comum;
using Volumo.Domain.Treinos;
using Volumo.Infrastructure.Persistencia;
using Volumo.Shared.Treinos;
using DomainGroup = Volumo.Domain.Exercicios.GrupoMuscular;
using DomainMoment = Volumo.Domain.Treinos.MomentoDoCardio;
using MomentoDoCardio = Volumo.Shared.Treinos.MomentoDoCardio;

namespace Volumo.Api.Treinos;

/// <summary>Plano já convertido para o domínio: musculação, cardio e quando fazer o cardio.</summary>
public sealed record PlanDefinition(IReadOnlyList<DefinicaoDeItem> Itens, IReadOnlyList<DefinicaoDeCardio> Cardios, DomainMoment Momento)
{
    /// <summary>Substitui musculação e cardio do treino ou template.</summary>
    public void ApplyTo(PlanoDeTreino plano)
    {
        ArgumentNullException.ThrowIfNull(plano);
        plano.SubstituirItens(Itens);
        plano.SubstituirCardios(Cardios, Momento);
    }
}

/// <summary>Converte treinos e templates entre os contratos da API e o domínio.</summary>
public sealed class PlanMapper(VolumoDbContext db)
{
    public const string UnavailableExercise = "Exercício indisponível";

    /// <summary>
    /// Plano do domínio a partir do que o construtor enviou. Devolve <c>null</c> se algum exercício
    /// não existe, está inativo ou não é visível para quem está montando (de outro personal).
    /// Séries inválidas ou cardio com exercício que não é de cardio lançam a exceção de regra de
    /// negócio (vira 400 com a mensagem).
    /// </summary>
    public async Task<PlanDefinition?> ToDefinitionsAsync(PlanoDeTreinoEntrada entrada, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        var cardios = entrada.Cardios ?? [];
        var ids = entrada.Itens.Select(i => i.ExercicioId).Concat(cardios.Select(c => c.ExercicioId)).Distinct().ToList();
        var groups = await db.Exercicios.Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.GrupoMuscular, ct);
        if (groups.Count != ids.Count)
        {
            return null;
        }

        if (cardios.Any(c => groups[c.ExercicioId] != DomainGroup.Cardio))
        {
            throw new RegraDeNegocioException("Na aba de cardio, use exercícios do grupo Cardio.");
        }

        if (entrada.Itens.Any(i => groups[i.ExercicioId] == DomainGroup.Cardio))
        {
            throw new RegraDeNegocioException("Exercício de cardio vai na aba de cardio, não na de musculação.");
        }

        return new PlanDefinition(
            [.. entrada.Itens.Select(i => new DefinicaoDeItem(i.ExercicioId, [.. i.Series.Select(ToDomain)], i.Observacao))],
            [.. cardios.Select(c => new DefinicaoDeCardio(c.ExercicioId, c.DuracaoMinutos, c.Observacao))],
            Enum.IsDefined(entrada.MomentoDoCardio) ? Enum.Parse<DomainMoment>(entrada.MomentoDoCardio.ToString()) : (DomainMoment)0);
    }

    /// <summary>
    /// Itens para a resposta, com o nome de cada exercício. Exercício que foi inativado depois continua
    /// aparecendo pelo nome; se não for mais visível (personal de outro vínculo), aparece como indisponível.
    /// </summary>
    public async Task<IReadOnlyList<ItemDoPlanoSaida>> ToOutputAsync(PlanoDeTreino plano, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plano);
        var names = await NamesAsync(plano, ct);
        return [.. plano.Itens.Select(i => new ItemDoPlanoSaida(
            i.Id,
            i.Ordem,
            i.ExercicioId,
            names.GetValueOrDefault(i.ExercicioId, UnavailableExercise),
            i.Observacao,
            [.. i.Series.Select(ToContract)]))];
    }

    public async Task<TemplateDetalhe> ToDetailAsync(TemplateTreino template, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(template);
        return new TemplateDetalhe(
            template.Id, template.Nome, await ToOutputAsync(template, ct), await ToCardioOutputAsync(template, ct), ToContract(template.MomentoDoCardio));
    }

    public async Task<TreinoDetalhe> ToDetailAsync(Treino treino, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(treino);
        return new TreinoDetalhe(
            treino.Id, treino.Nome, treino.AlunoId, treino.PersonalId,
            await ToOutputAsync(treino, ct), await ToCardioOutputAsync(treino, ct), ToContract(treino.MomentoDoCardio));
    }

    public static DomainGroup ToDomain(GrupoMuscular grupo) => Enum.Parse<DomainGroup>(grupo.ToString());

    public static GrupoMuscular ToContract(DomainGroup grupo) => Enum.Parse<GrupoMuscular>(grupo.ToString());

    private async Task<IReadOnlyList<CardioDoPlanoSaida>> ToCardioOutputAsync(PlanoDeTreino plano, CancellationToken ct)
    {
        var names = await NamesAsync(plano, ct);
        return [.. plano.Cardios.Select(c => new CardioDoPlanoSaida(
            c.Id, c.Ordem, c.ExercicioId, names.GetValueOrDefault(c.ExercicioId, UnavailableExercise), c.DuracaoMinutos, c.Observacao))];
    }

    private Task<Dictionary<Guid, string>> NamesAsync(PlanoDeTreino plano, CancellationToken ct)
    {
        var ids = plano.Itens.Select(i => i.ExercicioId).Concat(plano.Cardios.Select(c => c.ExercicioId)).Distinct().ToList();
        return db.Exercicios.IgnoreQueryFilters([FiltrosDeConsulta.Ativos])
            .Where(e => ids.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Nome, ct);
    }

    private static MomentoDoCardio ToContract(DomainMoment momento) => Enum.Parse<MomentoDoCardio>(momento.ToString());

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
