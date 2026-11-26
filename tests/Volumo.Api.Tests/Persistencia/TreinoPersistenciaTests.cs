using Microsoft.EntityFrameworkCore;
using Volumo.Api.Tests.Support;
using Volumo.Domain.Treinos;
using Volumo.Infrastructure.Persistencia;

namespace Volumo.Api.Tests.Persistencia;

public sealed class TreinoPersistenciaTests
{
    [Fact]
    public async Task Treino_com_itens_e_series_volta_do_banco_na_ordem_certa()
    {
        var ct = TestContext.Current.CancellationToken;
        Guid treinoId;
        Guid supinoId, crucifixoId;
        await using (var db = BancoDeTestes.NovoContexto())
        {
            var personal = await Cenario.PersonalAsync(db);
            var aluno = await Cenario.AlunoAsync(db);
            supinoId = (await Cenario.ExercicioAsync(db, "Supino")).Id;
            crucifixoId = (await Cenario.ExercicioAsync(db, "Crucifixo")).Id;

            var treino = Treino.CriarPeloPersonal(Guid.CreateVersion7(), personal.Id, aluno.Id, "Peito e tríceps");
            var crucifixo = treino.AdicionarItem(crucifixoId, [Serie.Criar(TipoSerie.Trabalho, 12, 15, 60)], "Amplitude total");
            var supino = treino.AdicionarItem(supinoId,
            [
                Serie.Criar(TipoSerie.Aquecimento, 15, 15, 60, 20m),
                Serie.Criar(TipoSerie.Trabalho, 8, 10, 120, 62.5m),
                Serie.Criar(TipoSerie.Trabalho, 8, 10, 120, 62.5m),
            ]);
            treino.ReordenarItens([supino.Id, crucifixo.Id]);
            treinoId = treino.Id;

            db.Treinos.Add(treino);
            await db.SaveChangesAsync(ct);
        }

        await using (var db = BancoDeTestes.NovoContexto())
        {
            var lido = await db.Treinos.SingleAsync(t => t.Id == treinoId, ct);

            lido.Nome.ShouldBe("Peito e tríceps");
            lido.Itens.Select(i => i.ExercicioId).ShouldBe([supinoId, crucifixoId]);
            lido.Itens[0].Series.Select(s => (s.Tipo, s.CargaKg)).ShouldBe(
                [(TipoSerie.Aquecimento, 20m), (TipoSerie.Trabalho, 62.5m), (TipoSerie.Trabalho, 62.5m)]);
            lido.Itens[1].Observacao.ShouldBe("Amplitude total");
            lido.Itens[1].Series.Single().CargaKg.ShouldBeNull();
        }
    }

    [Fact]
    public async Task Alterar_series_e_remover_itens_de_um_treino_salvo_persiste()
    {
        var ct = TestContext.Current.CancellationToken;
        Guid treinoId, mantidoId;
        await using (var db = BancoDeTestes.NovoContexto())
        {
            var personal = await Cenario.PersonalAsync(db);
            var aluno = await Cenario.AlunoAsync(db);
            var exercicio = await Cenario.ExercicioAsync(db);
            var treino = Treino.CriarPeloPersonal(Guid.CreateVersion7(), personal.Id, aluno.Id, "Treino B");
            var removido = treino.AdicionarItem(exercicio.Id, [Serie.Criar(TipoSerie.Trabalho, 10, 10, 60)]);
            mantidoId = treino.AdicionarItem(exercicio.Id, [Serie.Criar(TipoSerie.Trabalho, 10, 10, 60)]).Id;
            db.Treinos.Add(treino);
            await db.SaveChangesAsync(ct);
            treinoId = treino.Id;

            treino.RemoverItem(removido.Id);
            await db.SaveChangesAsync(ct);
        }

        await using (var db = BancoDeTestes.NovoContexto())
        {
            var treino = await db.Treinos.SingleAsync(t => t.Id == treinoId, ct);
            treino.DefinirSeries(mantidoId,
            [
                Serie.Criar(TipoSerie.Trabalho, 6, 8, 180, 100m),
                Serie.Criar(TipoSerie.Trabalho, 6, 8, 180, 100m),
            ]);
            treino.AdicionarItem(treino.Itens[0].ExercicioId, [Serie.Criar(TipoSerie.Trabalho, 15, 20, 45)]);
            await db.SaveChangesAsync(ct);
        }

        await using (var db = BancoDeTestes.NovoContexto())
        {
            var treino = await db.Treinos.SingleAsync(t => t.Id == treinoId, ct);

            treino.Itens.Count.ShouldBe(2);
            treino.Itens[0].Id.ShouldBe(mantidoId);
            treino.Itens[0].Ordem.ShouldBe(1);
            treino.Itens[0].Series.Select(s => s.CargaKg).ShouldBe([100m, 100m]);
            treino.Itens[1].Series.Single().RepeticoesMaximas.ShouldBe(20);
        }
    }

    [Fact]
    public async Task Treino_gerado_do_template_nao_muda_quando_o_template_muda()
    {
        var ct = TestContext.Current.CancellationToken;
        Guid templateId, treinoId;
        await using (var db = BancoDeTestes.NovoContexto())
        {
            var personal = await Cenario.PersonalAsync(db);
            var aluno = await Cenario.AlunoAsync(db);
            var exercicio = await Cenario.ExercicioAsync(db);
            var template = TemplateTreino.Criar(Guid.CreateVersion7(), personal.Id, "Iniciante A");
            template.AdicionarItem(exercicio.Id, [Serie.Criar(TipoSerie.Trabalho, 10, 12, 60, 10m)]);
            var treino = template.GerarTreinoPara(Guid.CreateVersion7(), aluno.Id);
            db.AddRange(template, treino);
            await db.SaveChangesAsync(ct);
            (templateId, treinoId) = (template.Id, treino.Id);
        }

        await using (var db = BancoDeTestes.NovoContexto())
        {
            var template = await db.TemplatesTreino.SingleAsync(t => t.Id == templateId, ct);
            template.DefinirSeries(template.Itens[0].Id, [Serie.Criar(TipoSerie.Trabalho, 10, 12, 60, 30m)]);
            await db.SaveChangesAsync(ct);
        }

        await using (var db = BancoDeTestes.NovoContexto())
        {
            var treino = await db.Treinos.SingleAsync(t => t.Id == treinoId, ct);

            treino.TemplateOrigemId.ShouldBe(templateId);
            treino.Itens.Single().Series.Single().CargaKg.ShouldBe(10m);
        }
    }

    [Fact]
    public async Task Template_inativado_some_das_consultas_mas_os_treinos_gerados_continuam()
    {
        var ct = TestContext.Current.CancellationToken;
        Guid templateId, treinoId;
        await using (var db = BancoDeTestes.NovoContexto())
        {
            var personal = await Cenario.PersonalAsync(db);
            var aluno = await Cenario.AlunoAsync(db);
            var exercicio = await Cenario.ExercicioAsync(db);
            var template = TemplateTreino.Criar(Guid.CreateVersion7(), personal.Id, "Temporário");
            template.AdicionarItem(exercicio.Id, [Serie.Criar(TipoSerie.Trabalho, 10, 12, 60)]);
            var treino = template.GerarTreinoPara(Guid.CreateVersion7(), aluno.Id);
            db.AddRange(template, treino);
            await db.SaveChangesAsync(ct);
            (templateId, treinoId) = (template.Id, treino.Id);

            template.Inativar(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(ct);
        }

        await using (var db = BancoDeTestes.NovoContexto())
        {
            (await db.TemplatesTreino.AnyAsync(t => t.Id == templateId, ct)).ShouldBeFalse();

            var inativo = await db.TemplatesTreino.IgnoreQueryFilters([FiltrosDeConsulta.Ativos]).SingleAsync(t => t.Id == templateId, ct);
            inativo.Ativo.ShouldBeFalse();
            inativo.Itens.Count.ShouldBe(1);

            var treino = await db.Treinos.SingleAsync(t => t.Id == treinoId, ct);
            treino.TemplateOrigemId.ShouldBe(templateId);
            treino.Itens.Count.ShouldBe(1);
        }
    }
}
