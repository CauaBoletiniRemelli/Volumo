using Microsoft.EntityFrameworkCore;
using Volumo.Api.Tests.Support;
using Volumo.Domain.Sessoes;
using Volumo.Domain.Treinos;

namespace Volumo.Api.Tests.Persistencia;

public sealed class SessaoPersistenciaTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 10, 16, 7, 0, 0, TimeSpan.FromHours(-3));

    [Fact]
    public async Task Series_registradas_em_sessao_ja_salva_sao_inseridas()
    {
        // Os Ids vêm do app. Se o EF tratasse o Id como gerado pelo banco, acharia que o
        // registro novo já existe e tentaria um UPDATE em vez de INSERT.
        var ct = TestContext.Current.CancellationToken;
        Guid sessaoId, exercicioId;
        await using (var db = BancoDeTestes.NovoContexto())
        {
            var aluno = await Cenario.AlunoAsync(db);
            exercicioId = (await Cenario.ExercicioAsync(db)).Id;
            var sessao = SessaoTreino.Iniciar(Guid.CreateVersion7(), aluno.Id, null, Inicio);
            sessao.RegistrarSerie(Guid.CreateVersion7(), exercicioId, 12, 40m, 3, Inicio.AddMinutes(5));
            db.SessoesTreino.Add(sessao);
            await db.SaveChangesAsync(ct);
            sessaoId = sessao.Id;
        }

        await using (var db = BancoDeTestes.NovoContexto())
        {
            var sessao = await db.SessoesTreino.Include(s => s.Registros).SingleAsync(s => s.Id == sessaoId, ct);
            sessao.RegistrarSerie(Guid.CreateVersion7(), exercicioId, 10, 42.5m, 1, Inicio.AddMinutes(8));
            sessao.Finalizar(Inicio.AddMinutes(50));
            await db.SaveChangesAsync(ct);
        }

        await using (var db = BancoDeTestes.NovoContexto())
        {
            var sessao = await db.SessoesTreino.Include(s => s.Registros).SingleAsync(s => s.Id == sessaoId, ct);

            sessao.FinalizadaEm.ShouldBe(Inicio.AddMinutes(50));
            sessao.Registros.Select(r => (r.Repeticoes, r.CargaKg, r.Rir)).ShouldBe([(12, 40m, (int?)3), (10, 42.5m, (int?)1)]);
        }
    }

    [Fact]
    public async Task Treino_inativado_mantem_o_historico_de_sessoes()
    {
        var ct = TestContext.Current.CancellationToken;
        Guid sessaoId;
        await using (var db = BancoDeTestes.NovoContexto())
        {
            var aluno = await Cenario.AlunoAsync(db);
            var exercicio = await Cenario.ExercicioAsync(db);
            var treino = Treino.CriarPeloAluno(Guid.CreateVersion7(), aluno.Id, "Meu treino");
            treino.AdicionarItem(exercicio.Id, [Serie.Criar(TipoSerie.Trabalho, 10, 12, 60)]);
            var sessao = SessaoTreino.Iniciar(Guid.CreateVersion7(), aluno.Id, treino.Id, Inicio);
            sessao.RegistrarSerie(Guid.CreateVersion7(), exercicio.Id, 10, 20m, null, Inicio.AddMinutes(3));
            db.AddRange(treino, sessao);
            await db.SaveChangesAsync(ct);
            sessaoId = sessao.Id;

            treino.Inativar(Inicio.AddDays(30));
            await db.SaveChangesAsync(ct);
        }

        await using (var db = BancoDeTestes.NovoContexto())
        {
            var sessao = await db.SessoesTreino.Include(s => s.Registros).SingleAsync(s => s.Id == sessaoId, ct);

            sessao.TreinoId.ShouldNotBeNull();
            sessao.Registros.Count.ShouldBe(1);
        }
    }
}
