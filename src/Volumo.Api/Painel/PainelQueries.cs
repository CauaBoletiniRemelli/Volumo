using Microsoft.EntityFrameworkCore;
using Volumo.Domain.Sessoes;
using Volumo.Infrastructure.Persistencia;
using Volumo.Shared.Painel;
using DomainCriterion = Volumo.Domain.Sessoes.CriterioDeEstagnacao;

namespace Volumo.Api.Painel;

/// <summary>
/// Monta o painel do personal: último treino, frequência e estagnação de cada aluno com vínculo
/// ativo. As sessões vêm do filtro de dono (só alunos com vínculo ativo); a regra de estagnação é a
/// do domínio (<see cref="AnaliseDeEstagnacao"/>).
/// </summary>
public sealed class PainelQueries(VolumoDbContext db, TimeProvider clock)
{
    /// <summary>Mais dias que isto sem treinar → "sumido" (decisão de produto: 5 dias).</summary>
    public const int DiasParaSumido = 5;

    /// <summary>Quanto histórico entra na análise de estagnação (cobre o maior limite, 90 dias, com folga).</summary>
    public const int JanelaDeDias = 120;

    public async Task<PainelDoPersonal> BuildAsync(Guid personalId, CancellationToken ct)
    {
        var agora = clock.GetUtcNow();
        var alunos = await (
                from v in db.VinculosPersonalAluno
                join a in db.PerfisAluno on v.AlunoId equals a.Id
                where v.PersonalId == personalId && v.EncerradoEm == null
                orderby a.Nome
                select new { a.Id, a.Nome, v.IniciadoEm, v.CriterioDeEstagnacao, v.LimiteDeEstagnacao })
            .ToListAsync(ct);
        var ids = alunos.ConvertAll(a => a.Id);

        // Última sessão de cada aluno (sem limite de data: sumido há 6 meses continua aparecendo).
        var ultimas = await db.SessoesTreino
            .Where(s => ids.Contains(s.AlunoId))
            .GroupBy(s => s.AlunoId)
            .Select(g => g.OrderByDescending(s => s.IniciadaEm).Select(s => new { s.AlunoId, s.IniciadaEm, s.TreinoId }).First())
            .ToListAsync(ct);

        var inicioDaSemana = agora.AddDays(-7);
        var naSemana = await db.SessoesTreino
            .Where(s => ids.Contains(s.AlunoId) && s.IniciadaEm >= inicioDaSemana)
            .GroupBy(s => s.AlunoId)
            .Select(g => new { AlunoId = g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(x => x.AlunoId, x => x.Quantidade, ct);

        // Maior carga por sessão finalizada e exercício, dentro da janela.
        var inicioDaJanela = agora.AddDays(-JanelaDeDias);
        var cargas = await (
                from r in db.RegistrosSerie
                join s in db.SessoesTreino on r.SessaoId equals s.Id
                where ids.Contains(s.AlunoId) && s.FinalizadaEm != null && s.IniciadaEm >= inicioDaJanela
                group r by new { s.AlunoId, SessaoId = s.Id, s.IniciadaEm, r.ExercicioId } into g
                select new { g.Key.AlunoId, g.Key.IniciadaEm, g.Key.ExercicioId, MaiorCarga = g.Max(x => x.CargaKg) })
            .ToListAsync(ct);

        var treinoIds = ultimas.Where(u => u.TreinoId is not null).Select(u => u.TreinoId!.Value).Distinct().ToList();
        var nomesDeTreinos = await db.Treinos.IgnoreQueryFilters([FiltrosDeConsulta.Ativos])
            .Where(t => treinoIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Nome, ct);
        var exercicioIds = cargas.Select(c => c.ExercicioId).Distinct().ToList();
        var nomesDeExercicios = await db.Exercicios.IgnoreQueryFilters([FiltrosDeConsulta.Ativos])
            .Where(e => exercicioIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.Nome, ct);

        var resultado = alunos.ConvertAll(aluno =>
        {
            var ultima = ultimas.Find(u => u.AlunoId == aluno.Id);
            var diasSemTreinar = (agora - (ultima?.IniciadaEm ?? aluno.IniciadoEm)).Days;
            var situacao = ultima is null ? SituacaoDoAluno.NuncaTreinou
                : diasSemTreinar > DiasParaSumido ? SituacaoDoAluno.Sumido
                : SituacaoDoAluno.EmDia;

            var estagnados = cargas
                .Where(c => c.AlunoId == aluno.Id)
                .GroupBy(c => c.ExercicioId)
                .Select(g => (ExercicioId: g.Key, Analise: AnaliseDeEstagnacao.Avaliar(
                    g.Select(c => new CargaNaSessao(c.IniciadaEm, c.MaiorCarga)), aluno.CriterioDeEstagnacao, aluno.LimiteDeEstagnacao, agora)))
                .Where(x => x.Analise is not null)
                .Select(x => new ExercicioEstagnado(
                    x.ExercicioId,
                    nomesDeExercicios.GetValueOrDefault(x.ExercicioId, Sessoes.SessionQueries.UnavailableExercise),
                    x.Analise!.MaiorCargaKg,
                    x.Analise.UltimoAumentoEm,
                    x.Analise.SessoesDesdeOAumento))
                .OrderBy(e => e.UltimoAumentoEm)
                .ToList();

            return new AlunoNoPainel(
                aluno.Id,
                aluno.Nome,
                aluno.IniciadoEm,
                situacao,
                diasSemTreinar,
                ultima?.IniciadaEm,
                ultima?.TreinoId is { } treinoId ? nomesDeTreinos.GetValueOrDefault(treinoId) : null,
                naSemana.GetValueOrDefault(aluno.Id),
                new ConfiguracaoDeEstagnacao(ToContract(aluno.CriterioDeEstagnacao), aluno.LimiteDeEstagnacao),
                estagnados);
        });

        return new PainelDoPersonal(agora, DiasParaSumido, resultado);
    }

    public static Shared.Painel.CriterioDeEstagnacao ToContract(DomainCriterion criterio) =>
        Enum.Parse<Shared.Painel.CriterioDeEstagnacao>(criterio.ToString());

    public static DomainCriterion ToDomain(Shared.Painel.CriterioDeEstagnacao criterio) =>
        Enum.Parse<DomainCriterion>(criterio.ToString());
}
