using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Volumo.Api.Tests.Support;
using Volumo.Shared.Autenticacao;
using Volumo.Shared.Convites;
using static Volumo.Api.Tests.Support.ContasDeTeste;

namespace Volumo.Api.Tests.Convites;

/// <summary>Cadastro de aluno pelo convite e aceite por quem já tem conta.</summary>
public sealed class AceiteDeConviteTests(VolumoApiFactory factory) : IClassFixture<VolumoApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Cadastro_pelo_convite_cria_aluno_confirmado_ja_vinculado_e_logado()
    {
        using var client = factory.CreateHttpsClient();
        var (emailPersonal, _, tokenPersonal) = await PersonalComDoisFatoresAsync(client, factory);
        var emailAluno = NovoEmail("aluno");
        (await ConvidarAsync(client, tokenPersonal, emailAluno)).Dispose();

        using var response = await CadastrarAlunoAsync(client, factory.Emails.TokenDoConvite(emailAluno));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        CookieDeAtualizacao(response).ShouldNotBeNull();
        var sessao = await response.Content.ReadFromJsonAsync<RespostaDeLogin>(Ct);
        var conta = await (await GetContaAsync(client, sessao!.TokenDeAcesso)).Content.ReadFromJsonAsync<DadosDaConta>(Ct);
        conta!.Email.ShouldBe(emailAluno);
        conta.Papeis.ShouldBe(["Aluno"]);
        (await EnviarAsync(client, HttpMethod.Get, "/__test/politicas/aluno", sessao.TokenDeAcesso)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var alunoId = await IdDoUsuarioAsync(emailAluno);
        var personalId = await IdDoUsuarioAsync(emailPersonal);
        await using var db = BancoDeTestes.NovoContexto();
        (await db.Users.SingleAsync(u => u.Id == alunoId, Ct)).EmailConfirmed.ShouldBeTrue();
        (await db.VinculosPersonalAluno.SingleAsync(v => v.AlunoId == alunoId, Ct)).PersonalId.ShouldBe(personalId);
        (await db.ConvitesAluno.SingleAsync(c => c.Email == emailAluno, Ct)).AlunoId.ShouldBe(alunoId);
    }

    [Fact]
    public async Task Link_do_convite_vale_uma_vez_so()
    {
        using var client = factory.CreateHttpsClient();
        var (_, _, tokenPersonal) = await PersonalComDoisFatoresAsync(client, factory);
        var emailAluno = NovoEmail("aluno");
        (await ConvidarAsync(client, tokenPersonal, emailAluno)).Dispose();
        var link = factory.Emails.TokenDoConvite(emailAluno);
        (await CadastrarAlunoAsync(client, link)).Dispose();

        (await CadastrarAlunoAsync(client, link)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Convite_vencido_nao_serve()
    {
        using var client = factory.CreateHttpsClient();
        var (_, _, tokenPersonal) = await PersonalComDoisFatoresAsync(client, factory);
        var emailAluno = NovoEmail("aluno");
        (await ConvidarAsync(client, tokenPersonal, emailAluno)).Dispose();
        await using (var db = BancoDeTestes.NovoContexto())
        {
            await db.ConvitesAluno.Where(c => c.Email == emailAluno)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.ExpiraEm, DateTimeOffset.UtcNow.AddMinutes(-1)), Ct);
        }

        (await CadastrarAlunoAsync(client, factory.Emails.TokenDoConvite(emailAluno))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Senha_fraca_nao_consome_o_convite()
    {
        using var client = factory.CreateHttpsClient();
        var (_, _, tokenPersonal) = await PersonalComDoisFatoresAsync(client, factory);
        var emailAluno = NovoEmail("aluno");
        (await ConvidarAsync(client, tokenPersonal, emailAluno)).Dispose();
        var link = factory.Emails.TokenDoConvite(emailAluno);

        (await CadastrarAlunoAsync(client, link, senha: "curta")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CadastrarAlunoAsync(client, link)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Cadastro_com_email_que_ja_tem_conta_pede_para_entrar()
    {
        using var client = factory.CreateHttpsClient();
        var (_, _, tokenPersonal) = await PersonalComDoisFatoresAsync(client, factory);
        var jaTemConta = await PersonalConfirmadoAsync(client, factory);
        (await ConvidarAsync(client, tokenPersonal, jaTemConta)).Dispose();

        (await CadastrarAlunoAsync(client, factory.Emails.TokenDoConvite(jaTemConta))).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Convite_de_personal_que_ficou_inativo_nao_serve()
    {
        using var client = factory.CreateHttpsClient();
        var (emailPersonal, _, tokenPersonal) = await PersonalComDoisFatoresAsync(client, factory);
        var emailAluno = NovoEmail("aluno");
        (await ConvidarAsync(client, tokenPersonal, emailAluno)).Dispose();
        await InativarPerfilDoPersonalAsync(emailPersonal);

        (await CadastrarAlunoAsync(client, factory.Emails.TokenDoConvite(emailAluno))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Aluno_de_personal_inativo_aceita_convite_de_outro_e_troca_de_personal()
    {
        using var client = factory.CreateHttpsClient();
        var (emailAluno, tokenAluno, emailPersonalAntigo, _) = await AlunoViaConviteAsync(client, factory);
        var (emailNovoPersonal, _, tokenNovoPersonal) = await PersonalComDoisFatoresAsync(client, factory);
        await InativarPerfilDoPersonalAsync(emailPersonalAntigo);
        (await ConvidarAsync(client, tokenNovoPersonal, emailAluno)).Dispose();

        using var response = await EnviarAsync(
            client, HttpMethod.Post, "/convites/aceitar", tokenAluno, corpo: new TokenDeConvite(factory.Emails.TokenDoConvite(emailAluno)));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var alunoId = await IdDoUsuarioAsync(emailAluno);
        var novoPersonalId = await IdDoUsuarioAsync(emailNovoPersonal);
        await using var db = BancoDeTestes.NovoContexto();
        var vinculos = await db.VinculosPersonalAluno.Where(v => v.AlunoId == alunoId).ToListAsync(Ct);
        vinculos.Count.ShouldBe(2);
        vinculos.Single(v => v.EncerradoEm == null).PersonalId.ShouldBe(novoPersonalId);
    }

    [Fact]
    public async Task Aluno_com_personal_ativo_precisa_encerrar_antes_de_aceitar_outro()
    {
        using var client = factory.CreateHttpsClient();
        var (emailAluno, tokenAluno, _, _) = await AlunoViaConviteAsync(client, factory);
        var (_, _, tokenNovoPersonal) = await PersonalComDoisFatoresAsync(client, factory);
        (await ConvidarAsync(client, tokenNovoPersonal, emailAluno)).Dispose();

        using var response = await EnviarAsync(
            client, HttpMethod.Post, "/convites/aceitar", tokenAluno, corpo: new TokenDeConvite(factory.Emails.TokenDoConvite(emailAluno)));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("Encerre o vínculo atual");
    }

    [Fact]
    public async Task Convite_enviado_para_outro_email_nao_pode_ser_aceito()
    {
        using var client = factory.CreateHttpsClient();
        var (_, tokenAluno, _, _) = await AlunoViaConviteAsync(client, factory);
        var (_, _, tokenOutroPersonal) = await PersonalComDoisFatoresAsync(client, factory);
        var outroEmail = NovoEmail("aluno");
        (await ConvidarAsync(client, tokenOutroPersonal, outroEmail)).Dispose();

        using var response = await EnviarAsync(
            client, HttpMethod.Post, "/convites/aceitar", tokenAluno, corpo: new TokenDeConvite(factory.Emails.TokenDoConvite(outroEmail)));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static async Task InativarPerfilDoPersonalAsync(string email)
    {
        var id = await IdDoUsuarioAsync(email);
        await using var db = BancoDeTestes.NovoContexto();
        var perfil = await db.PerfisPersonal.SingleAsync(p => p.Id == id, Ct);
        perfil.Inativar(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(Ct);
    }
}
