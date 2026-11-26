using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volumo.Api.Tests.Support;
using Volumo.Domain.Sessoes;
using Volumo.Domain.Treinos;
using Volumo.Domain.Usuarios;
using Volumo.Domain.Vinculos;
using Volumo.Infrastructure.Contas;
using Volumo.Infrastructure.Persistencia;

namespace Volumo.Api.Tests.Persistencia;

/// <summary>Exclusão de conta (LGPD): dados pessoais anonimizados, nada apagado, histórico preservado.</summary>
public sealed class ExclusaoDeContaTests(VolumoApiFactory factory) : IClassFixture<VolumoApiFactory>
{
    private static readonly DateTimeOffset Antes = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Excluir_conta_de_personal_anonimiza_e_preserva_o_que_os_alunos_usam()
    {
        var ct = TestContext.Current.CancellationToken;
        Guid personalId, alunoId, treinoId, templateId, conviteAceitoId, convitePendenteId, exercicioProprioId, presetId;
        await using (var db = BancoDeTestes.NovoContexto())
        {
            var personal = await Cenario.PersonalAsync(db, Cenario.NovoCref("G/SP"));
            var aluno = await Cenario.AlunoAsync(db);
            var exercicio = await Cenario.ExercicioAsync(db);
            var exercicioProprio = Domain.Exercicios.Exercicio.CriarPersonalizado(
                Guid.CreateVersion7(), personal.Id, "Remada do personal", Domain.Exercicios.GrupoMuscular.Costas);
            var template = TemplateTreino.Criar(Guid.CreateVersion7(), personal.Id, "Modelo");
            template.AdicionarItem(exercicio.Id, [Serie.Criar(TipoSerie.Trabalho, 8, 12, 90)]);
            var treino = template.GerarTreinoPara(Guid.CreateVersion7(), aluno.Id);
            var (conviteAceito, token) = ConviteAluno.Criar(Guid.CreateVersion7(), personal.Id, "aluno@volumo.test", Antes);
            var vinculo = conviteAceito.Aceitar(token, aluno.Id, Antes);
            var (convitePendente, _) = ConviteAluno.Criar(Guid.CreateVersion7(), personal.Id, "outro@volumo.test", Antes);
            var preset = PresetDeSeries.Criar(Guid.CreateVersion7(), personal.Id, "Preset do personal", [Serie.Criar(TipoSerie.Trabalho, 10, 10, 60)]);
            db.AddRange(exercicioProprio, template, treino, conviteAceito, vinculo, convitePendente, preset);
            db.UserClaims.Add(new IdentityUserClaim<Guid> { UserId = personal.Id, ClaimType = "teste", ClaimValue = "x" });
            await db.SaveChangesAsync(ct);
            (personalId, alunoId, treinoId, templateId) = (personal.Id, aluno.Id, treino.Id, template.Id);
            (conviteAceitoId, convitePendenteId, exercicioProprioId, presetId) = (conviteAceito.Id, convitePendente.Id, exercicioProprio.Id, preset.Id);
        }

        (await Excluir(personalId)).ShouldBeTrue();

        await using (var db = BancoDeTestes.NovoContexto())
        {
            var usuario = await db.Users.SingleAsync(u => u.Id == personalId, ct);
            usuario.Email.ShouldBeNull();
            usuario.UserName.ShouldBe($"removido-{personalId:N}");
            usuario.PasswordHash.ShouldBeNull();
            usuario.LockoutEnd.ShouldBe(DateTimeOffset.MaxValue);
            usuario.AnonimizadoEm.ShouldNotBeNull();
            (await db.UserClaims.AnyAsync(c => c.UserId == personalId, ct)).ShouldBeFalse();

            (await db.PerfisPersonal.AnyAsync(p => p.Id == personalId, ct)).ShouldBeFalse("perfil anonimizado some das consultas comuns");
            var perfil = await db.PerfisPersonal.IgnoreQueryFilters([FiltrosDeConsulta.Ativos]).SingleAsync(p => p.Id == personalId, ct);
            perfil.Nome.ShouldBe(Perfil.NomeAnonimizado);
            perfil.Cref.ShouldBeNull();

            (await db.VinculosPersonalAluno.SingleAsync(v => v.AlunoId == alunoId, ct)).Ativo.ShouldBeFalse();
            var convites = await db.ConvitesAluno.Where(c => c.PersonalId == personalId).ToListAsync(ct);
            convites.ShouldAllBe(c => c.Email == ConviteAluno.EmailAnonimizado);
            convites.Single(c => c.Id == convitePendenteId).RevogadoEm.ShouldNotBeNull();
            convites.Single(c => c.Id == conviteAceitoId).RevogadoEm.ShouldBeNull();

            (await db.TemplatesTreino.AnyAsync(t => t.Id == templateId, ct)).ShouldBeFalse();
            (await db.Exercicios.AnyAsync(e => e.Id == exercicioProprioId, ct)).ShouldBeFalse();
            (await db.PresetsDeSeries.AnyAsync(p => p.Id == presetId, ct)).ShouldBeFalse();

            // O aluno continua com o treino que o personal prescreveu.
            (await db.Treinos.SingleAsync(t => t.Id == treinoId, ct)).Ativo.ShouldBeTrue();
            (await db.PerfisAluno.AnyAsync(a => a.Id == alunoId, ct)).ShouldBeTrue();
        }
    }

    [Fact]
    public async Task Excluir_conta_de_aluno_anonimiza_e_mantem_as_series_registradas()
    {
        var ct = TestContext.Current.CancellationToken;
        Guid alunoId, treinoId, sessaoId, conviteId;
        await using (var db = BancoDeTestes.NovoContexto())
        {
            var personal = await Cenario.PersonalAsync(db);
            var aluno = await Cenario.AlunoAsync(db);
            var exercicio = await Cenario.ExercicioAsync(db);
            var (convite, token) = ConviteAluno.Criar(Guid.CreateVersion7(), personal.Id, "aluno@volumo.test", Antes);
            var vinculo = convite.Aceitar(token, aluno.Id, Antes);
            var treino = Treino.CriarPeloPersonal(Guid.CreateVersion7(), personal.Id, aluno.Id, "Treino A");
            treino.AdicionarItem(exercicio.Id, [Serie.Criar(TipoSerie.Trabalho, 8, 12, 90)]);
            var sessao = SessaoTreino.Iniciar(Guid.CreateVersion7(), aluno.Id, treino.Id, Antes.AddDays(1));
            sessao.RegistrarSerie(Guid.CreateVersion7(), exercicio.Id, 10, 30m, 2, Antes.AddDays(1).AddMinutes(5));
            db.AddRange(convite, vinculo, treino, sessao);
            await db.SaveChangesAsync(ct);
            (alunoId, treinoId, sessaoId, conviteId) = (aluno.Id, treino.Id, sessao.Id, convite.Id);
        }

        (await Excluir(alunoId)).ShouldBeTrue();

        await using (var db = BancoDeTestes.NovoContexto())
        {
            var perfil = await db.PerfisAluno.IgnoreQueryFilters([FiltrosDeConsulta.Ativos]).SingleAsync(a => a.Id == alunoId, ct);
            perfil.Nome.ShouldBe(Perfil.NomeAnonimizado);
            perfil.Anonimizado.ShouldBeTrue();

            (await db.Users.SingleAsync(u => u.Id == alunoId, ct)).Email.ShouldBeNull();
            (await db.VinculosPersonalAluno.SingleAsync(v => v.AlunoId == alunoId, ct)).Ativo.ShouldBeFalse();
            (await db.ConvitesAluno.SingleAsync(c => c.Id == conviteId, ct)).Email.ShouldBe(ConviteAluno.EmailAnonimizado);
            (await db.Treinos.AnyAsync(t => t.Id == treinoId, ct)).ShouldBeFalse();

            var sessao = await db.SessoesTreino.Include(s => s.Registros).SingleAsync(s => s.Id == sessaoId, ct);
            sessao.Registros.Single().CargaKg.ShouldBe(30m);
        }
    }

    [Fact]
    public async Task Excluir_de_novo_nao_muda_nada_e_conta_inexistente_devolve_false()
    {
        Guid alunoId;
        await using (var db = BancoDeTestes.NovoContexto())
        {
            alunoId = (await Cenario.AlunoAsync(db)).Id;
        }

        (await Excluir(alunoId)).ShouldBeTrue();
        DateTimeOffset? primeiraVez;
        await using (var db = BancoDeTestes.NovoContexto())
        {
            primeiraVez = (await db.PerfisAluno.IgnoreQueryFilters([FiltrosDeConsulta.Ativos])
                .SingleAsync(a => a.Id == alunoId, TestContext.Current.CancellationToken)).AnonimizadoEm;
        }

        (await Excluir(alunoId)).ShouldBeTrue();
        (await Excluir(Guid.CreateVersion7())).ShouldBeFalse();

        await using (var db = BancoDeTestes.NovoContexto())
        {
            var perfil = await db.PerfisAluno.IgnoreQueryFilters([FiltrosDeConsulta.Ativos])
                .SingleAsync(a => a.Id == alunoId, TestContext.Current.CancellationToken);
            perfil.AnonimizadoEm.ShouldBe(primeiraVez);
        }
    }

    private async Task<bool> Excluir(Guid usuarioId)
    {
        using var escopo = factory.Services.CreateScope();
        return await escopo.ServiceProvider.GetRequiredService<ExclusaoDeConta>()
            .AnonimizarAsync(usuarioId, TestContext.Current.CancellationToken);
    }
}
