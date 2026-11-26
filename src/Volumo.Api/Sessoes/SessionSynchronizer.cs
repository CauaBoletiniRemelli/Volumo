using Microsoft.EntityFrameworkCore;
using Volumo.Api.Treinos;
using Volumo.Domain.Comum;
using Volumo.Domain.Sessoes;
using Volumo.Infrastructure.Persistencia;
using Volumo.Shared.Sessoes;

namespace Volumo.Api.Sessoes;

/// <summary>
/// Grava as sessões que o app registrou (muitas vezes offline). Cada sessão é independente: uma
/// recusada não impede as outras. Reenviar a mesma sessão não duplica nada, porque os Ids vêm do app
/// e o domínio ignora séries já registradas.
/// </summary>
public sealed class SessionSynchronizer(VolumoDbContext db, StudentNotes notes, TimeProvider clock)
{
    /// <summary>Folga para relógio de celular um pouco adiantado.</summary>
    public static readonly TimeSpan FutureTolerance = TimeSpan.FromMinutes(5);

    public async Task<ResultadoDaSessao> SyncAsync(Guid alunoId, SessaoRegistrada sessao, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sessao);
        try
        {
            return await TrySyncAsync(alunoId, sessao, ct);
        }
        catch (RegraDeNegocioException e)
        {
            db.ChangeTracker.Clear();
            return Rejected(sessao.Id, e.Message);
        }
        catch (DbUpdateException)
        {
            // Outro envio gravou os mesmos Ids ao mesmo tempo (dois aparelhos, reenvio rápido):
            // tenta de novo, agora pelo caminho de sessão existente, que é idempotente.
            db.ChangeTracker.Clear();
        }

        try
        {
            return await TrySyncAsync(alunoId, sessao, ct);
        }
        catch (Exception e) when (e is RegraDeNegocioException or DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return Rejected(sessao.Id, e is RegraDeNegocioException ? e.Message : "Não foi possível gravar a sessão.");
        }
    }

    private async Task<ResultadoDaSessao> TrySyncAsync(Guid alunoId, SessaoRegistrada dados, CancellationToken ct)
    {
        var cardios = dados.Cardios ?? [];
        var anotacoes = dados.Anotacoes ?? [];
        var limit = clock.GetUtcNow() + FutureTolerance;
        if (dados.IniciadaEm > limit || dados.FinalizadaEm > limit || dados.Series.Any(s => s.RealizadaEm > limit) || cardios.Any(c => c.RealizadoEm > limit)
            || anotacoes.Any(a => a.AtualizadaEm > limit))
        {
            return Rejected(dados.Id, "Horário no futuro. Confira a data e a hora do aparelho.");
        }

        // Id de sessão que já existe e é de outra pessoa: recusa sem dizer de quem é.
        var owner = await db.SessoesTreino.IgnoreQueryFilters([FiltrosDeConsulta.Dono])
            .Where(s => s.Id == dados.Id).Select(s => (Guid?)s.AlunoId).SingleOrDefaultAsync(ct);
        if (owner is not null && owner != alunoId)
        {
            return Rejected(dados.Id, "Sessão inválida.");
        }

        // Treino arquivado depois de começar a sessão offline continua valendo.
        if (dados.TreinoId is { } treinoId
            && !await db.Treinos.IgnoreQueryFilters([FiltrosDeConsulta.Ativos]).AnyAsync(t => t.Id == treinoId && t.AlunoId == alunoId, ct))
        {
            return Rejected(dados.Id, "Treino não encontrado.");
        }

        var exercicios = dados.Series.Select(s => s.ExercicioId).Concat(cardios.Select(c => c.ExercicioId)).Distinct().ToList();
        var visible = await db.Exercicios.IgnoreQueryFilters([FiltrosDeConsulta.Ativos]).CountAsync(e => exercicios.Contains(e.Id), ct);
        if (visible != exercicios.Count)
        {
            return Rejected(dados.Id, "Exercício não encontrado.");
        }

        SessaoTreino sessao;
        if (owner is null)
        {
            sessao = SessaoTreino.Iniciar(dados.Id, alunoId, dados.TreinoId, dados.IniciadaEm);
            db.SessoesTreino.Add(sessao);
        }
        else
        {
            sessao = await db.SessoesTreino.Include(s => s.Registros).Include(s => s.Cardios).SingleAsync(s => s.Id == dados.Id, ct);
        }

        foreach (var serie in dados.Series.OrderBy(s => s.RealizadaEm))
        {
            sessao.RegistrarSerie(serie.Id, serie.ExercicioId, serie.Repeticoes, serie.CargaKg, serie.Rir, serie.RealizadaEm);
        }

        foreach (var cardio in cardios.OrderBy(c => c.RealizadoEm))
        {
            sessao.RegistrarCardio(cardio.Id, cardio.ExercicioId, cardio.DuracaoMinutos, cardio.RealizadoEm);
        }

        if (dados.FinalizadaEm is { } fim)
        {
            sessao.Finalizar(fim);
        }

        // O treino já foi conferido acima; sem treino (sessão livre), não há onde guardar a observação.
        if (dados.TreinoId is { } treinoDasAnotacoes && anotacoes.Count > 0)
        {
            await notes.ApplyAsync(alunoId, treinoDasAnotacoes, anotacoes.Select(a => new StudentNote(a.ExercicioId, a.Texto, a.AtualizadaEm)), ct);
        }

        await db.SaveChangesAsync(ct);
        return new ResultadoDaSessao(dados.Id, SituacaoDaSincronizacao.Sincronizada, null);
    }

    private static ResultadoDaSessao Rejected(Guid id, string motivo) => new(id, SituacaoDaSincronizacao.Recusada, motivo);
}
