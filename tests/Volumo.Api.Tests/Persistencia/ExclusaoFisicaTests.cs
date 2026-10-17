using Microsoft.EntityFrameworkCore;
using Volumo.Api.Tests.Support;
using Volumo.Domain.Sessoes;
using Volumo.Domain.Treinos;

namespace Volumo.Api.Tests.Persistencia;

/// <summary>Regra do projeto: nada importante é apagado do banco, só inativado ou anonimizado.</summary>
public sealed class ExclusaoFisicaTests
{
    [Fact]
    public async Task Apagar_perfil_e_bloqueado()
    {
        await using var db = BancoDeTestes.NovoContexto();
        var aluno = await Cenario.AlunoAsync(db);

        db.PerfisAluno.Remove(aluno);

        await DeveSerBloqueado(db, nameof(Domain.Usuarios.PerfilAluno));
    }

    [Fact]
    public async Task Apagar_treino_template_exercicio_ou_sessao_e_bloqueado()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = BancoDeTestes.NovoContexto();
        var personal = await Cenario.PersonalAsync(db);
        var aluno = await Cenario.AlunoAsync(db);
        var exercicio = await Cenario.ExercicioAsync(db);
        var template = TemplateTreino.Criar(Guid.CreateVersion7(), personal.Id, "Modelo");
        var treino = Treino.CriarPeloAluno(Guid.CreateVersion7(), aluno.Id, "Treino");
        var sessao = SessaoTreino.Iniciar(Guid.CreateVersion7(), aluno.Id, null, DateTimeOffset.UtcNow);
        db.AddRange(template, treino, sessao);
        await db.SaveChangesAsync(ct);

        db.RemoveRange(template, treino, exercicio, sessao);

        await DeveSerBloqueado(db, nameof(TemplateTreino), nameof(Treino), nameof(Domain.Exercicios.Exercicio), nameof(SessaoTreino));
    }

    [Fact]
    public async Task Nem_o_banco_aceita_apagar_um_usuario_com_perfil()
    {
        // Segunda linha de defesa: as FKs são NO ACTION, então um DELETE direto no banco também falha.
        var ct = TestContext.Current.CancellationToken;
        await using var db = BancoDeTestes.NovoContexto();
        var aluno = await Cenario.AlunoAsync(db);

        await Should.ThrowAsync<Microsoft.Data.SqlClient.SqlException>(() =>
            db.Database.ExecuteSqlAsync($"DELETE FROM AspNetUsers WHERE Id = {aluno.Id}", ct));
    }

    private static async Task DeveSerBloqueado(DbContext db, params string[] tipos)
    {
        var erro = await Should.ThrowAsync<InvalidOperationException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        foreach (var tipo in tipos)
        {
            erro.Message.ShouldContain(tipo);
        }
    }
}
