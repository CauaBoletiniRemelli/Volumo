using Microsoft.EntityFrameworkCore;
using Volumo.Domain.Sessoes;
using Volumo.Domain.Treinos;
using Volumo.Infrastructure.Persistencia;
using Volumo.Shared.Sessoes;

namespace Volumo.Api.Sessoes;

/// <summary>Consultas de histórico de sessões, usadas pelo aluno e pelo personal.</summary>
public sealed class SessionQueries(VolumoDbContext db)
{
    public const int HistoryLimit = 50;
    public const string UnavailableExercise = "Exercício indisponível";

    /// <summary>Sessões olhadas no gráfico de evolução e na exportação do treino.</summary>
    public const int EvolutionLimit = 30;

    /// <summary>Sessões mais recentes primeiro, com contagem de séries e volume (repetições × carga).</summary>
    public async Task<IReadOnlyList<SessaoResumo>> SummariesAsync(IQueryable<SessaoTreino> sessoes, CancellationToken ct)
    {
        var rows = await sessoes
            .OrderByDescending(s => s.IniciadaEm)
            .Take(HistoryLimit)
            .Select(s => new
            {
                s.Id,
                s.TreinoId,
                s.IniciadaEm,
                s.FinalizadaEm,
                Series = s.Registros.Count,
                Volume = s.Registros.Sum(r => r.Repeticoes * r.CargaKg),
                Cardio = s.Cardios.Sum(c => c.DuracaoMinutos),
            })
            .ToListAsync(ct);

        var names = await TreinoNamesAsync(rows.Where(r => r.TreinoId is not null).Select(r => r.TreinoId!.Value), ct);
        return [.. rows.Select(r => new SessaoResumo(
            r.Id, r.TreinoId, r.TreinoId is { } t ? names.GetValueOrDefault(t) : null, r.IniciadaEm, r.FinalizadaEm, r.Series, r.Volume, r.Cardio))];
    }

    public async Task<SessaoDetalhe?> DetailAsync(Guid id, CancellationToken ct)
    {
        var sessao = await db.SessoesTreino.AsNoTracking().Include(s => s.Registros).Include(s => s.Cardios).SingleOrDefaultAsync(s => s.Id == id, ct);
        if (sessao is null)
        {
            return null;
        }

        var exercicioIds = sessao.Registros.Select(r => r.ExercicioId).Concat(sessao.Cardios.Select(c => c.ExercicioId)).Distinct().ToList();
        var exercicios = await db.Exercicios.IgnoreQueryFilters([FiltrosDeConsulta.Ativos])
            .Where(e => exercicioIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.Nome, ct);
        var treinos = sessao.TreinoId is { } treinoId ? await TreinoNamesAsync([treinoId], ct) : [];

        return new SessaoDetalhe(
            sessao.Id,
            sessao.AlunoId,
            sessao.TreinoId,
            sessao.TreinoId is { } t ? treinos.GetValueOrDefault(t) : null,
            sessao.IniciadaEm,
            sessao.FinalizadaEm,
            [.. sessao.Registros.Select(r => new SerieDaSessao(
                r.Id, r.ExercicioId, exercicios.GetValueOrDefault(r.ExercicioId, UnavailableExercise), r.Repeticoes, r.CargaKg, r.Rir, r.RealizadaEm))],
            [.. sessao.Cardios.Select(c => new CardioDaSessao(
                c.Id, c.ExercicioId, exercicios.GetValueOrDefault(c.ExercicioId, UnavailableExercise), c.DuracaoMinutos, c.RealizadoEm))]);
    }

    /// <summary>
    /// Evolução de cada exercício nas últimas <see cref="EvolutionLimit"/> sessões do treino: por dia,
    /// a série mais pesada, as repetições e o volume. Exercícios na ordem do plano; os que saíram do
    /// plano mas têm histórico vêm depois, por nome.
    /// </summary>
    public async Task<EvolucaoDoTreino> EvolutionAsync(Treino treino, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(treino);
        var sessoes = await db.SessoesTreino.AsNoTracking()
            .Where(s => s.TreinoId == treino.Id)
            .OrderByDescending(s => s.IniciadaEm)
            .Take(EvolutionLimit)
            .Select(s => new { s.Id, s.IniciadaEm })
            .ToListAsync(ct);
        var ids = sessoes.Select(s => s.Id).ToList();
        var series = await db.RegistrosSerie.AsNoTracking()
            .Where(r => ids.Contains(r.SessaoId))
            .Select(r => new { r.SessaoId, r.ExercicioId, r.Repeticoes, r.CargaKg })
            .ToListAsync(ct);

        var exercicioIds = series.Select(s => s.ExercicioId).Distinct().ToList();
        var names = await db.Exercicios.IgnoreQueryFilters([FiltrosDeConsulta.Ativos])
            .Where(e => exercicioIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.Nome, ct);
        var inicio = sessoes.ToDictionary(s => s.Id, s => s.IniciadaEm);
        var ordem = treino.Itens.Select((item, indice) => (item.ExercicioId, indice)).DistinctBy(p => p.ExercicioId).ToDictionary(p => p.ExercicioId, p => p.indice);

        var exercicios = series
            .GroupBy(s => s.ExercicioId)
            .Select(porExercicio => new EvolucaoDoExercicio(
                porExercicio.Key,
                names.GetValueOrDefault(porExercicio.Key, UnavailableExercise),
                [.. porExercicio
                    .GroupBy(s => s.SessaoId)
                    .Select(dia =>
                    {
                        var pesada = dia.OrderByDescending(s => s.CargaKg).ThenByDescending(s => s.Repeticoes).First();
                        return new PontoDeEvolucao(
                            dia.Key, inicio[dia.Key], pesada.CargaKg, pesada.Repeticoes, dia.Sum(s => s.Repeticoes), dia.Sum(s => s.Repeticoes * s.CargaKg), dia.Count());
                    })
                    .OrderBy(p => p.Data)]))
            .OrderBy(e => ordem.GetValueOrDefault(e.ExercicioId, int.MaxValue))
            .ThenBy(e => e.NomeDoExercicio, StringComparer.CurrentCulture)
            .ToList();

        return new EvolucaoDoTreino(treino.Id, exercicios);
    }

    /// <summary>Nomes dos treinos, inclusive arquivados (o histórico continua mostrando o nome).</summary>
    private Task<Dictionary<Guid, string>> TreinoNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return db.Treinos.IgnoreQueryFilters([FiltrosDeConsulta.Ativos])
            .Where(t => list.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Nome, ct);
    }
}
