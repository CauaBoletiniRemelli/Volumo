using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Volumo.Api.Tests.Support;
using Volumo.Shared.Convites;
using static Volumo.Api.Tests.Support.ContasDeTeste;

namespace Volumo.Api.Tests.Convites;

public sealed class ConvitesTests(VolumoApiFactory factory) : IClassFixture<VolumoApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Personal_convida_e_o_aluno_recebe_o_link_por_email()
    {
        using var client = factory.CreateHttpsClient();
        var (_, _, token) = await PersonalComDoisFatoresAsync(client, factory);
        var emailAluno = NovoEmail("aluno");

        using var response = await ConvidarAsync(client, token, emailAluno);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var convite = await response.Content.ReadFromJsonAsync<ConviteEnviado>(Ct);
        convite!.Situacao.ShouldBe("Pendente");
        factory.Emails.Para(emailAluno).Single().Assunto.ShouldContain("convidou você");
        factory.Emails.TokenDoConvite(emailAluno).ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Listagem_mostra_so_os_convites_do_proprio_personal()
    {
        using var client = factory.CreateHttpsClient();
        var (_, _, meuToken) = await PersonalComDoisFatoresAsync(client, factory);
        var (_, _, outroToken) = await PersonalComDoisFatoresAsync(client, factory);
        var meuAluno = NovoEmail("aluno");
        (await ConvidarAsync(client, meuToken, meuAluno)).Dispose();
        (await ConvidarAsync(client, outroToken, NovoEmail("aluno"))).Dispose();

        using var response = await EnviarAsync(client, HttpMethod.Get, "/convites", meuToken);

        var convites = await response.Content.ReadFromJsonAsync<List<ConviteEnviado>>(Ct);
        convites!.Select(c => c.Email).ShouldBe([meuAluno]);
    }

    [Fact]
    public async Task Convidar_de_novo_o_mesmo_email_invalida_o_link_anterior()
    {
        using var client = factory.CreateHttpsClient();
        var (_, _, token) = await PersonalComDoisFatoresAsync(client, factory);
        var emailAluno = NovoEmail("aluno");
        (await ConvidarAsync(client, token, emailAluno)).Dispose();
        var linkAntigo = factory.Emails.TokenDoConvite(emailAluno);

        (await ConvidarAsync(client, token, emailAluno)).Dispose();

        (await CadastrarAlunoAsync(client, linkAntigo)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CadastrarAlunoAsync(client, factory.Emails.TokenDoConvite(emailAluno))).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Convidar_quem_ja_e_seu_aluno_responde_409()
    {
        using var client = factory.CreateHttpsClient();
        var (emailAluno, _, _, tokenPersonal) = await AlunoViaConviteAsync(client, factory);

        using var response = await ConvidarAsync(client, tokenPersonal, emailAluno);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Revogar_invalida_o_link()
    {
        using var client = factory.CreateHttpsClient();
        var (_, _, token) = await PersonalComDoisFatoresAsync(client, factory);
        var emailAluno = NovoEmail("aluno");
        using var criado = await ConvidarAsync(client, token, emailAluno);
        var convite = await criado.Content.ReadFromJsonAsync<ConviteEnviado>(Ct);

        using var revogacao = await EnviarAsync(client, HttpMethod.Post, $"/convites/{convite!.Id}/revogar", token);

        revogacao.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await CadastrarAlunoAsync(client, factory.Emails.TokenDoConvite(emailAluno))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Matriz_de_autorizacao_dos_convites()
    {
        using var client = factory.CreateHttpsClient();
        var (_, _, tokenDono) = await PersonalComDoisFatoresAsync(client, factory);
        var (_, _, tokenOutroPersonal) = await PersonalComDoisFatoresAsync(client, factory);
        var emailPersonalSemDoisFatores = await PersonalConfirmadoAsync(client, factory);
        var (tokenSemDoisFatores, _) = await EntrarAsync(client, emailPersonalSemDoisFatores);
        var (_, tokenAluno, _, _) = await AlunoViaConviteAsync(client, factory);
        using var criado = await ConvidarAsync(client, tokenDono, NovoEmail("aluno"));
        var convite = await criado.Content.ReadFromJsonAsync<ConviteEnviado>(Ct);
        var revogar = $"/convites/{convite!.Id}/revogar";

        // Anônimo → 401
        (await EnviarAsync(client, HttpMethod.Post, revogar, null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await EnviarAsync(client, HttpMethod.Get, "/convites", null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Papel ou 2FA errado → 403
        (await EnviarAsync(client, HttpMethod.Post, revogar, tokenAluno)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await EnviarAsync(client, HttpMethod.Post, revogar, tokenSemDoisFatores)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ConvidarAsync(client, tokenAluno, NovoEmail())).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Recurso de outro dono → 404 (não revela que existe)
        (await EnviarAsync(client, HttpMethod.Post, revogar, tokenOutroPersonal)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await EnviarAsync(client, HttpMethod.Post, $"/convites/{Guid.CreateVersion7()}/revogar", tokenDono)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Dono → 204
        (await EnviarAsync(client, HttpMethod.Post, revogar, tokenDono)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Consultar_convite_mostra_quem_convidou_e_se_ja_existe_conta()
    {
        using var client = factory.CreateHttpsClient();
        var (_, _, token) = await PersonalComDoisFatoresAsync(client, factory);
        var novo = NovoEmail("aluno");
        var jaTemConta = await PersonalConfirmadoAsync(client, factory);
        (await ConvidarAsync(client, token, novo)).Dispose();
        (await ConvidarAsync(client, token, jaTemConta)).Dispose();

        using var r1 = await client.PostAsJsonAsync("/convites/consultar", new TokenDeConvite(factory.Emails.TokenDoConvite(novo)), Ct);
        using var r2 = await client.PostAsJsonAsync("/convites/consultar", new TokenDeConvite(factory.Emails.TokenDoConvite(jaTemConta)), Ct);
        using var invalido = await client.PostAsJsonAsync("/convites/consultar", new TokenDeConvite("token-invalido"), Ct);

        var resumo1 = await r1.Content.ReadFromJsonAsync<ResumoDoConvite>(Ct);
        resumo1!.NomeDoPersonal.ShouldBe("Personal de Teste");
        resumo1.Email.ShouldBe(novo);
        resumo1.ContaExistente.ShouldBeFalse();
        (await r2.Content.ReadFromJsonAsync<ResumoDoConvite>(Ct))!.ContaExistente.ShouldBeTrue();
        invalido.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Banco_guarda_so_o_hash_do_token_do_convite()
    {
        using var client = factory.CreateHttpsClient();
        var (_, _, token) = await PersonalComDoisFatoresAsync(client, factory);
        var emailAluno = NovoEmail("aluno");
        (await ConvidarAsync(client, token, emailAluno)).Dispose();
        var tokenDoLink = factory.Emails.TokenDoConvite(emailAluno);

        await using var db = BancoDeTestes.NovoContexto();
        var convite = await db.ConvitesAluno.SingleAsync(c => c.Email == emailAluno, Ct);
        convite.TokenHash.ShouldBe(Domain.Vinculos.ConviteAluno.CalcularHash(tokenDoLink));
        Convert.ToBase64String(convite.TokenHash).ShouldNotContain(tokenDoLink);
    }
}
