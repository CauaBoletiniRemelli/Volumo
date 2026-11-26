using Microsoft.EntityFrameworkCore;
using Volumo.Api.Tests.Support;
using Volumo.Domain.Usuarios;
using Volumo.Domain.Vinculos;
using Volumo.Infrastructure.Persistencia;

namespace Volumo.Api.Tests.Persistencia;

public sealed class VinculoPersistenciaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 16, 18, 0, 0, TimeSpan.FromHours(-3));

    [Fact]
    public async Task Aluno_nao_pode_ter_dois_vinculos_ativos()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = BancoDeTestes.NovoContexto();
        var primeiroPersonal = await Cenario.PersonalAsync(db);
        var segundoPersonal = await Cenario.PersonalAsync(db);
        var aluno = await Cenario.AlunoAsync(db);
        db.VinculosPersonalAluno.Add(VinculoPersonalAluno.Criar(Guid.CreateVersion7(), primeiroPersonal.Id, aluno.Id, Agora));
        await db.SaveChangesAsync(ct);

        db.VinculosPersonalAluno.Add(VinculoPersonalAluno.Criar(Guid.CreateVersion7(), segundoPersonal.Id, aluno.Id, Agora));

        var erro = await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync(ct));
        erro.InnerException!.Message.ShouldContain("UX_VinculosPersonalAluno_AlunoAtivo");
    }

    [Fact]
    public async Task Aluno_pode_trocar_de_personal_depois_de_encerrar_o_vinculo()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = BancoDeTestes.NovoContexto();
        var primeiroPersonal = await Cenario.PersonalAsync(db);
        var segundoPersonal = await Cenario.PersonalAsync(db);
        var aluno = await Cenario.AlunoAsync(db);
        var antigo = VinculoPersonalAluno.Criar(Guid.CreateVersion7(), primeiroPersonal.Id, aluno.Id, Agora);
        db.VinculosPersonalAluno.Add(antigo);
        await db.SaveChangesAsync(ct);

        antigo.Encerrar(Agora.AddMonths(2));
        db.VinculosPersonalAluno.Add(VinculoPersonalAluno.Criar(Guid.CreateVersion7(), segundoPersonal.Id, aluno.Id, Agora.AddMonths(2)));
        await db.SaveChangesAsync(ct);

        var vinculos = await db.VinculosPersonalAluno.AsNoTracking().Where(v => v.AlunoId == aluno.Id).ToListAsync(ct);
        vinculos.Count.ShouldBe(2);
        vinculos.Single(v => v.EncerradoEm == null).PersonalId.ShouldBe(segundoPersonal.Id);
    }

    [Fact]
    public async Task Convite_e_encontrado_pelo_hash_do_token_e_aceito()
    {
        var ct = TestContext.Current.CancellationToken;
        string token;
        Guid personalId;
        await using (var db = BancoDeTestes.NovoContexto())
        {
            personalId = (await Cenario.PersonalAsync(db)).Id;
            (var convite, token) = ConviteAluno.Criar(Guid.CreateVersion7(), personalId, "novo.aluno@volumo.test", Agora);
            db.ConvitesAluno.Add(convite);
            await db.SaveChangesAsync(ct);
        }

        await using (var db = BancoDeTestes.NovoContexto())
        {
            var aluno = await Cenario.AlunoAsync(db);
            var hash = ConviteAluno.CalcularHash(token);
            var convite = await db.ConvitesAluno.SingleAsync(c => c.TokenHash == hash, ct);

            db.VinculosPersonalAluno.Add(convite.Aceitar(token, aluno.Id, Agora.AddHours(1)));
            await db.SaveChangesAsync(ct);

            var vinculo = await db.VinculosPersonalAluno.AsNoTracking().SingleAsync(v => v.AlunoId == aluno.Id, ct);
            vinculo.PersonalId.ShouldBe(personalId);
        }
    }

    [Fact]
    public async Task Troca_de_personal_inativo_encerra_e_cria_vinculo_no_mesmo_save()
    {
        // O índice único de vínculo ativo precisa aceitar o UPDATE (encerrar) e o INSERT (novo) juntos.
        var ct = TestContext.Current.CancellationToken;
        Guid alunoId, novoPersonalId;
        await using (var db = BancoDeTestes.NovoContexto())
        {
            var personalAntigo = await Cenario.PersonalAsync(db);
            var aluno = await Cenario.AlunoAsync(db);
            novoPersonalId = (await Cenario.PersonalAsync(db)).Id;
            db.VinculosPersonalAluno.Add(VinculoPersonalAluno.Criar(Guid.CreateVersion7(), personalAntigo.Id, aluno.Id, Agora));
            personalAntigo.Inativar(Agora.AddMonths(1));
            await db.SaveChangesAsync(ct);
            alunoId = aluno.Id;
        }

        await using (var db = BancoDeTestes.NovoContexto())
        {
            var aluno = await db.PerfisAluno.SingleAsync(a => a.Id == alunoId, ct);
            var vinculoAtivo = await db.VinculosPersonalAluno.SingleAsync(v => v.AlunoId == alunoId && v.EncerradoEm == null, ct);
            var personalAtual = await db.PerfisPersonal.IgnoreQueryFilters([FiltrosDeConsulta.Ativos])
                .SingleAsync(p => p.Id == vinculoAtivo.PersonalId, ct);
            var (convite, token) = ConviteAluno.Criar(Guid.CreateVersion7(), novoPersonalId, "aluno@volumo.test", Agora.AddMonths(2));
            db.ConvitesAluno.Add(convite);

            db.VinculosPersonalAluno.Add(AceiteDeConvite.Aceitar(convite, token, aluno, vinculoAtivo, personalAtual, Agora.AddMonths(2)));
            await db.SaveChangesAsync(ct);
        }

        await using (var db = BancoDeTestes.NovoContexto())
        {
            var vinculos = await db.VinculosPersonalAluno.Where(v => v.AlunoId == alunoId).ToListAsync(ct);
            vinculos.Count.ShouldBe(2);
            vinculos.Single(v => v.Ativo).PersonalId.ShouldBe(novoPersonalId);
        }
    }

    [Fact]
    public async Task Dois_personais_nao_podem_usar_o_mesmo_cref()
    {
        await using var db = BancoDeTestes.NovoContexto();
        var cref = Cenario.NovoCref("G/SP");
        await Cenario.PersonalAsync(db, cref);

        await Should.ThrowAsync<DbUpdateException>(() => Cenario.PersonalAsync(db, cref));
    }

    [Fact]
    public async Task Cref_volta_do_banco_como_objeto_de_valor()
    {
        var ct = TestContext.Current.CancellationToken;
        Guid personalId;
        var cref = Cenario.NovoCref("P/RJ");
        await using (var db = BancoDeTestes.NovoContexto())
        {
            personalId = (await Cenario.PersonalAsync(db, cref)).Id;
        }

        await using (var db = BancoDeTestes.NovoContexto())
        {
            var personal = await db.PerfisPersonal.SingleAsync(p => p.Id == personalId, ct);
            personal.Cref.ShouldBe(cref);
        }
    }
}
