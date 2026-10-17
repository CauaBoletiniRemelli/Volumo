using Microsoft.EntityFrameworkCore;
using Volumo.Api.Tests.Support;
using Volumo.Domain.Exercicios;
using Volumo.Domain.Sessoes;
using Volumo.Domain.Treinos;
using Volumo.Domain.Vinculos;
using Volumo.Infrastructure.Persistencia;

namespace Volumo.Api.Tests.Persistencia;

/// <summary>
/// Filtros de dono (proteção contra IDOR no banco). Cenário: personais A e B; aluno X com vínculo
/// ativo com A; aluno Y com B; aluno Z que já foi aluno de A (vínculo encerrado).
/// </summary>
public sealed class FiltrosDeDonoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 16, 18, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Personal_ve_so_os_proprios_dados_e_os_dos_alunos_com_vinculo_ativo()
    {
        var c = await CriarCenarioAsync();
        await using var db = BancoDeTestes.NovoContexto(c.PersonalA);

        (await db.TemplatesTreino.Select(t => t.Id).ToListAsync(Ct)).ShouldBe([c.TemplateA]);
        (await db.Treinos.Select(t => t.Id).ToListAsync(Ct)).ShouldBe([c.TreinoX]);
        (await db.SessoesTreino.Select(s => s.Id).ToListAsync(Ct)).ShouldBe([c.SessaoX]);
        (await db.RegistrosSerie.Select(r => r.SessaoId).Distinct().ToListAsync(Ct)).ShouldBe([c.SessaoX]);
        (await db.PerfisAluno.Select(a => a.Id).ToListAsync(Ct)).ShouldBe([c.AlunoX]);
        (await db.PerfisPersonal.Select(p => p.Id).ToListAsync(Ct)).ShouldBe([c.PersonalA]);
        (await db.ConvitesAluno.Select(cv => cv.Id).ToListAsync(Ct)).ShouldBe([c.ConviteA]);
        // Sem ordenar no banco: o SQL Server ordena uniqueidentifier pelos últimos bytes, o .NET pelos primeiros.
        (await db.VinculosPersonalAluno.Select(v => v.AlunoId).ToListAsync(Ct)).ShouldBe([c.AlunoX, c.AlunoZ], ignoreOrder: true);

        var exercicios = await db.Exercicios.Select(e => e.Id).ToListAsync(Ct);
        exercicios.ShouldContain(c.ExercicioDoCatalogo);
        exercicios.ShouldContain(c.ExercicioDeA);
        exercicios.ShouldNotContain(c.ExercicioDeB);
    }

    [Fact]
    public async Task Personal_perde_acesso_aos_dados_do_aluno_quando_o_vinculo_acaba()
    {
        var c = await CriarCenarioAsync();
        await using var db = BancoDeTestes.NovoContexto(c.PersonalA);

        (await db.Treinos.AnyAsync(t => t.Id == c.TreinoZ, Ct)).ShouldBeFalse();
        (await db.PerfisAluno.AnyAsync(a => a.Id == c.AlunoZ, Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Aluno_ve_os_proprios_treinos_e_os_exercicios_e_o_perfil_do_seu_personal()
    {
        var c = await CriarCenarioAsync();
        await using var db = BancoDeTestes.NovoContexto(c.AlunoX);

        (await db.Treinos.Select(t => t.Id).ToListAsync(Ct)).ShouldBe([c.TreinoX]);
        (await db.SessoesTreino.Select(s => s.Id).ToListAsync(Ct)).ShouldBe([c.SessaoX]);
        (await db.TemplatesTreino.AnyAsync(Ct)).ShouldBeFalse();
        (await db.PerfisPersonal.Select(p => p.Id).ToListAsync(Ct)).ShouldBe([c.PersonalA]);
        (await db.PerfisAluno.Select(a => a.Id).ToListAsync(Ct)).ShouldBe([c.AlunoX]);

        var exercicios = await db.Exercicios.Select(e => e.Id).ToListAsync(Ct);
        exercicios.ShouldContain(c.ExercicioDeA);
        exercicios.ShouldNotContain(c.ExercicioDeB);
    }

    [Fact]
    public async Task Ex_aluno_continua_vendo_o_proprio_historico_e_quem_o_treinou()
    {
        var c = await CriarCenarioAsync();
        await using var db = BancoDeTestes.NovoContexto(c.AlunoZ);

        (await db.Treinos.Select(t => t.Id).ToListAsync(Ct)).ShouldBe([c.TreinoZ]);
        (await db.Exercicios.AnyAsync(e => e.Id == c.ExercicioDeA, Ct)).ShouldBeTrue();
        (await db.PerfisPersonal.Select(p => p.Id).ToListAsync(Ct)).ShouldBe([c.PersonalA]);
    }

    [Fact]
    public async Task Sem_usuario_o_sistema_ve_tudo_e_ignorar_o_filtro_e_explicito()
    {
        var c = await CriarCenarioAsync();
        await using (var sistema = BancoDeTestes.NovoContexto())
        {
            (await sistema.Treinos.CountAsync(t => t.Id == c.TreinoX || t.Id == c.TreinoY || t.Id == c.TreinoZ, Ct)).ShouldBe(3);
        }

        await using var comoPersonalB = BancoDeTestes.NovoContexto(c.PersonalB);
        (await comoPersonalB.Treinos.AnyAsync(t => t.Id == c.TreinoX, Ct)).ShouldBeFalse();
        (await comoPersonalB.Treinos.IgnoreQueryFilters([FiltrosDeConsulta.Dono]).AnyAsync(t => t.Id == c.TreinoX, Ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task Buscar_pelo_id_de_outro_dono_nao_encontra_nada()
    {
        // O ataque de IDOR clássico: trocar o Id na URL. O filtro faz o Find/Single voltar vazio.
        var c = await CriarCenarioAsync();
        await using var db = BancoDeTestes.NovoContexto(c.PersonalB);

        (await db.TemplatesTreino.SingleOrDefaultAsync(t => t.Id == c.TemplateA, Ct)).ShouldBeNull();
        (await db.SessoesTreino.SingleOrDefaultAsync(s => s.Id == c.SessaoX, Ct)).ShouldBeNull();
        (await db.ConvitesAluno.SingleOrDefaultAsync(cv => cv.Id == c.ConviteA, Ct)).ShouldBeNull();
    }

    private sealed record Ids(
        Guid PersonalA, Guid PersonalB, Guid AlunoX, Guid AlunoY, Guid AlunoZ,
        Guid ExercicioDoCatalogo, Guid ExercicioDeA, Guid ExercicioDeB,
        Guid TemplateA, Guid TreinoX, Guid TreinoY, Guid TreinoZ, Guid SessaoX, Guid ConviteA);

    private static async Task<Ids> CriarCenarioAsync()
    {
        await using var db = BancoDeTestes.NovoContexto();
        var a = await Cenario.PersonalAsync(db);
        var b = await Cenario.PersonalAsync(db);
        var x = await Cenario.AlunoAsync(db);
        var y = await Cenario.AlunoAsync(db);
        var z = await Cenario.AlunoAsync(db);
        var catalogo = await Cenario.ExercicioAsync(db);
        var exA = Exercicio.CriarPersonalizado(Guid.CreateVersion7(), a.Id, "Exercício de A", GrupoMuscular.Costas);
        var exB = Exercicio.CriarPersonalizado(Guid.CreateVersion7(), b.Id, "Exercício de B", GrupoMuscular.Costas);

        var vinculoZ = VinculoPersonalAluno.Criar(Guid.CreateVersion7(), a.Id, z.Id, Agora.AddMonths(-6));
        vinculoZ.Encerrar(Agora.AddMonths(-1));

        var templateA = TemplateTreino.Criar(Guid.CreateVersion7(), a.Id, "Template de A");
        var templateB = TemplateTreino.Criar(Guid.CreateVersion7(), b.Id, "Template de B");
        var treinoX = Treino.CriarPeloPersonal(Guid.CreateVersion7(), a.Id, x.Id, "Treino de X");
        treinoX.AdicionarItem(exA.Id, [Serie.Criar(TipoSerie.Trabalho, 8, 12, 60)]);
        var treinoY = Treino.CriarPeloPersonal(Guid.CreateVersion7(), b.Id, y.Id, "Treino de Y");
        var treinoZ = Treino.CriarPeloPersonal(Guid.CreateVersion7(), a.Id, z.Id, "Treino de Z");
        var sessaoX = SessaoTreino.Iniciar(Guid.CreateVersion7(), x.Id, treinoX.Id, Agora);
        sessaoX.RegistrarSerie(Guid.CreateVersion7(), exA.Id, 10, 30m, 2, Agora.AddMinutes(5));
        var sessaoY = SessaoTreino.Iniciar(Guid.CreateVersion7(), y.Id, treinoY.Id, Agora);
        sessaoY.RegistrarSerie(Guid.CreateVersion7(), catalogo.Id, 10, 30m, 2, Agora.AddMinutes(5));
        var (conviteA, _) = ConviteAluno.Criar(Guid.CreateVersion7(), a.Id, "convidado-a@volumo.test", Agora);
        var (conviteB, _) = ConviteAluno.Criar(Guid.CreateVersion7(), b.Id, "convidado-b@volumo.test", Agora);

        db.AddRange(exA, exB, vinculoZ, templateA, templateB, treinoX, treinoY, treinoZ, sessaoX, sessaoY, conviteA, conviteB);
        db.AddRange(
            VinculoPersonalAluno.Criar(Guid.CreateVersion7(), a.Id, x.Id, Agora.AddMonths(-2)),
            VinculoPersonalAluno.Criar(Guid.CreateVersion7(), b.Id, y.Id, Agora.AddMonths(-2)));
        await db.SaveChangesAsync(Ct);

        return new Ids(
            a.Id, b.Id, x.Id, y.Id, z.Id, catalogo.Id, exA.Id, exB.Id,
            templateA.Id, treinoX.Id, treinoY.Id, treinoZ.Id, sessaoX.Id, conviteA.Id);
    }
}
