using Microsoft.EntityFrameworkCore;
using Volumo.Domain.Sessoes;
using Volumo.Infrastructure.Persistencia;
using Volumo.Shared.Sessoes;

namespace Volumo.Api.Sessoes;

/// <summary>Consultas de histórico de sessões, usadas pelo aluno e pelo personal.</summary>
public sealed class SessionQueries(VolumoDbContext db)
{
    public const int HistoryLimit = 50;
    public const string UnavailableExercise = "Exercício indisponível";

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
            })
            .ToListAsync(ct);

        var names = await TreinoNamesAsync(rows.Where(r => r.TreinoId is not null).Select(r => r.TreinoId!.Value), ct);
        return [.. rows.Select(r => new SessaoResumo(
            r.Id, r.TreinoId, r.TreinoId is { } t ? names.GetValueOrDefault(t) : null, r.IniciadaEm, r.FinalizadaEm, r.Series, r.Volume))];
    }

    public async Task<SessaoDetalhe?> DetailAsync(Guid id, CancellationToken ct)
    {
        var sessao = await db.SessoesTreino.AsNoTracking().Include(s => s.Registros).SingleOrDefaultAsync(s => s.Id == id, ct);
        if (sessao is null)
        {
            return null;
        }

        var exercicioIds = sessao.Registros.Select(r => r.ExercicioId).Distinct().ToList();
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
                r.Id, r.ExercicioId, exercicios.GetValueOrDefault(r.ExercicioId, UnavailableExercise), r.Repeticoes, r.CargaKg, r.Rir, r.RealizadaEm))]);
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
