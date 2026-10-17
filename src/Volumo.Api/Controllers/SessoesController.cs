using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volumo.Api.Auth;
using Volumo.Api.Sessoes;
using Volumo.Infrastructure.Persistencia;
using Volumo.Shared.Sessoes;

namespace Volumo.Api.Controllers;

/// <summary>
/// Sessões de treino. O aluno sincroniza o que registrou no app (inclusive offline) e vê o próprio
/// histórico; o personal vê as sessões dos alunos com vínculo ativo (filtro de dono).
/// </summary>
[ApiController]
[Route("sessoes")]
public sealed class SessoesController(VolumoDbContext db, SessionSynchronizer synchronizer, SessionQueries queries) : ControllerBase
{
    /// <summary>
    /// Recebe um lote de sessões. Responde 200 com o resultado de cada uma: "Sincronizada" (gravada ou
    /// já gravada antes) ou "Recusada" com o motivo, para o app parar de reenviar e avisar o aluno.
    /// </summary>
    [HttpPost("sincronizar")]
    [Authorize(Policy = Policies.Aluno)]
    public async Task<ResultadoDaSincronizacao> Sync(LoteDeSessoes lote, CancellationToken ct)
    {
        var alunoId = User.UsuarioId()!.Value;
        var results = new List<ResultadoDaSessao>(lote.Sessoes.Count);
        foreach (var sessao in lote.Sessoes)
        {
            results.Add(await synchronizer.SyncAsync(alunoId, sessao, ct));
        }

        return new ResultadoDaSincronizacao(results);
    }

    [HttpGet]
    [Authorize(Policy = Policies.Aluno)]
    public Task<IReadOnlyList<SessaoResumo>> ListMine(CancellationToken ct)
    {
        var alunoId = User.UsuarioId()!.Value;
        return queries.SummariesAsync(db.SessoesTreino.Where(s => s.AlunoId == alunoId), ct);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.ContaAtiva)]
    public async Task<ActionResult<SessaoDetalhe>> Get(Guid id, CancellationToken ct) =>
        await queries.DetailAsync(id, ct) is { } detalhe ? detalhe : NotFound();
}
