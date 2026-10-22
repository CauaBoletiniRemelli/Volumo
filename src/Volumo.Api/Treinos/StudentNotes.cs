using Microsoft.EntityFrameworkCore;
using Volumo.Domain.Treinos;
using Volumo.Infrastructure.Persistencia;

namespace Volumo.Api.Treinos;

/// <summary>Observação do aluno num exercício do treino, vinda da sessão (offline) ou gravada direto.</summary>
public sealed record StudentNote(Guid ExercicioId, string? Texto, DateTimeOffset Em);

/// <summary>
/// Observações que o aluno escreve nos exercícios do próprio treino. Uma por treino e exercício;
/// vale a mais nova (o app pode mandar mudanças feitas offline, de mais de um aparelho).
/// </summary>
public sealed class StudentNotes(VolumoDbContext db)
{
    /// <summary>
    /// Aplica as observações de exercícios que fazem parte do treino; as de outros exercícios (o
    /// personal tirou o exercício enquanto o aluno estava offline) são ignoradas. Não salva.
    /// </summary>
    /// <returns><c>false</c> se o treino não é do aluno.</returns>
    public async Task<bool> ApplyAsync(Guid alunoId, Guid treinoId, IEnumerable<StudentNote> notes, CancellationToken ct)
    {
        var treino = await db.Treinos.IgnoreQueryFilters([FiltrosDeConsulta.Ativos]).AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == treinoId && t.AlunoId == alunoId, ct);
        if (treino is null)
        {
            return false;
        }

        var inPlan = treino.Itens.Select(i => i.ExercicioId).Concat(treino.Cardios.Select(c => c.ExercicioId)).ToHashSet();
        var latest = notes.Where(n => inPlan.Contains(n.ExercicioId)).GroupBy(n => n.ExercicioId).Select(g => g.MaxBy(n => n.Em)!).ToList();
        if (latest.Count == 0)
        {
            return true;
        }

        var existing = await db.AnotacoesDoAluno.Where(a => a.TreinoId == treinoId).ToDictionaryAsync(a => a.ExercicioId, ct);
        foreach (var note in latest)
        {
            if (existing.TryGetValue(note.ExercicioId, out var anotacao))
            {
                anotacao.Atualizar(note.Texto, note.Em);
            }
            else
            {
                db.AnotacoesDoAluno.Add(AnotacaoDoAluno.Criar(Guid.CreateVersion7(), alunoId, treinoId, note.ExercicioId, note.Texto, note.Em));
            }
        }

        return true;
    }

    /// <summary>Texto de cada exercício com observação (filtro de dono: o aluno e o personal com vínculo).</summary>
    public Task<Dictionary<Guid, string>> TextsAsync(Guid treinoId, CancellationToken ct) =>
        db.AnotacoesDoAluno.AsNoTracking()
            .Where(a => a.TreinoId == treinoId && a.Texto != null)
            .ToDictionaryAsync(a => a.ExercicioId, a => a.Texto!, ct);
}
