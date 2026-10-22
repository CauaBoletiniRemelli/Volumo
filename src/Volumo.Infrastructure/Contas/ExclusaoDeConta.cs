using Microsoft.EntityFrameworkCore;
using Volumo.Infrastructure.Persistencia;

namespace Volumo.Infrastructure.Contas;

/// <summary>
/// Exclusão de conta pedida pelo usuário (LGPD, art. 18). Nada é apagado do banco: os dados pessoais
/// são anonimizados e o resto é inativado, para o histórico de treinos continuar consistente sem
/// identificar ninguém. Tudo vai num único SaveChanges (uma transação) e pode ser repetido sem efeito.
/// Operação do sistema: ignora os filtros de dono (precisa achar convites e vínculos dos dois lados).
/// </summary>
public sealed class ExclusaoDeConta(VolumoDbContext db, TimeProvider relogio)
{
    /// <returns><c>false</c> se a conta não existe.</returns>
    public async Task<bool> AnonimizarAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        var usuario = await db.Users.SingleOrDefaultAsync(u => u.Id == usuarioId, cancellationToken);
        if (usuario is null)
        {
            return false;
        }

        var agora = relogio.GetUtcNow();
        usuario.Anonimizar(agora);
        await RemoverCredenciaisAsync(usuarioId, cancellationToken);
        await AnonimizarPersonalAsync(usuarioId, agora, cancellationToken);
        await AnonimizarAlunoAsync(usuarioId, agora, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task RemoverCredenciaisAsync(Guid usuarioId, CancellationToken ct)
    {
        db.UserLogins.RemoveRange(await db.UserLogins.Where(l => l.UserId == usuarioId).ToListAsync(ct));
        db.UserTokens.RemoveRange(await db.UserTokens.Where(t => t.UserId == usuarioId).ToListAsync(ct));
        db.UserClaims.RemoveRange(await db.UserClaims.Where(c => c.UserId == usuarioId).ToListAsync(ct));
        db.UserRoles.RemoveRange(await db.UserRoles.Where(r => r.UserId == usuarioId).ToListAsync(ct));

        // Derruba todas as sessões abertas (refresh tokens); o access token vence em minutos.
        var agora = relogio.GetUtcNow();
        foreach (var token in await db.TokensDeAtualizacao.Where(t => t.UsuarioId == usuarioId && t.RevogadoEm == null).ToListAsync(ct))
        {
            token.Revogar(agora);
        }
    }

    private async Task AnonimizarPersonalAsync(Guid personalId, DateTimeOffset agora, CancellationToken ct)
    {
        var personal = await db.PerfisPersonal.IgnoreQueryFilters([FiltrosDeConsulta.Ativos, FiltrosDeConsulta.Dono])
            .SingleOrDefaultAsync(p => p.Id == personalId, ct);
        if (personal is null)
        {
            return;
        }

        personal.Anonimizar(agora);

        // Os alunos ficam sem personal, mas mantêm os treinos que ele prescreveu.
        foreach (var vinculo in await db.VinculosPersonalAluno.IgnoreQueryFilters([FiltrosDeConsulta.Dono]).Where(v => v.PersonalId == personalId && v.EncerradoEm == null).ToListAsync(ct))
        {
            vinculo.Encerrar(agora);
        }

        foreach (var convite in await db.ConvitesAluno.IgnoreQueryFilters([FiltrosDeConsulta.Dono]).Where(c => c.PersonalId == personalId).ToListAsync(ct))
        {
            convite.Anonimizar(agora);
        }

        foreach (var template in await db.TemplatesTreino.IgnoreQueryFilters([FiltrosDeConsulta.Dono]).Where(t => t.PersonalId == personalId).ToListAsync(ct))
        {
            template.Inativar(agora);
        }

        foreach (var exercicio in await db.Exercicios.IgnoreQueryFilters([FiltrosDeConsulta.Dono]).Where(e => e.PersonalId == personalId).ToListAsync(ct))
        {
            exercicio.Inativar(agora);
        }
    }

    private async Task AnonimizarAlunoAsync(Guid alunoId, DateTimeOffset agora, CancellationToken ct)
    {
        var aluno = await db.PerfisAluno.IgnoreQueryFilters([FiltrosDeConsulta.Ativos, FiltrosDeConsulta.Dono])
            .SingleOrDefaultAsync(a => a.Id == alunoId, ct);
        if (aluno is null)
        {
            return;
        }

        aluno.Anonimizar(agora);

        foreach (var vinculo in await db.VinculosPersonalAluno.IgnoreQueryFilters([FiltrosDeConsulta.Dono]).Where(v => v.AlunoId == alunoId && v.EncerradoEm == null).ToListAsync(ct))
        {
            vinculo.Encerrar(agora);
        }

        foreach (var convite in await db.ConvitesAluno.IgnoreQueryFilters([FiltrosDeConsulta.Dono]).Where(c => c.AlunoId == alunoId).ToListAsync(ct))
        {
            convite.Anonimizar(agora);
        }

        // Sessões e séries registradas ficam: sem dados pessoais, servem só para estatística.
        foreach (var treino in await db.Treinos.IgnoreQueryFilters([FiltrosDeConsulta.Dono]).Where(t => t.AlunoId == alunoId).ToListAsync(ct))
        {
            treino.Inativar(agora);
        }

        foreach (var anotacao in await db.AnotacoesDoAluno.IgnoreQueryFilters([FiltrosDeConsulta.Dono]).Where(a => a.AlunoId == alunoId).ToListAsync(ct))
        {
            anotacao.Anonimizar();
        }
    }
}
