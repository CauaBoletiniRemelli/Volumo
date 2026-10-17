using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Volumo.Api.Tests.Support;
using Volumo.Shared.Autenticacao;
using static Volumo.Api.Tests.Support.ContasDeTeste;

namespace Volumo.Api.Tests.Auth;

public sealed class CadastroTests(VolumoApiFactory factory) : IClassFixture<VolumoApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Cadastro_valido_cria_conta_perfil_e_papel_e_envia_link_de_confirmacao()
    {
        using var client = factory.CreateHttpsClient();
        var email = NovoEmail();

        using var response = await CadastrarPersonalAsync(client, email, cref: Cenario.NovoCref("G/SP").Valor);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        factory.Emails.Para(email).Single().Assunto.ShouldContain("Confirme");

        await using var db = BancoDeTestes.NovoContexto();
        var usuario = await db.Users.SingleAsync(u => u.Email == email, Ct);
        usuario.EmailConfirmed.ShouldBeFalse();
        usuario.Id.Version.ShouldBe(7);
        (await db.PerfisPersonal.SingleAsync(p => p.Id == usuario.Id, Ct)).Cref.ShouldNotBeNull();
        (await db.UserRoles.CountAsync(r => r.UserId == usuario.Id, Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task Email_ja_cadastrado_recebe_a_mesma_resposta_e_o_dono_e_avisado()
    {
        using var client = factory.CreateHttpsClient();
        var email = NovoEmail();
        (await CadastrarPersonalAsync(client, email)).Dispose();

        using var segunda = await CadastrarPersonalAsync(client, email.ToUpperInvariant(), senha: "outra senha qualquer");

        segunda.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        factory.Emails.Para(email).Select(m => m.Assunto).ShouldBe(["Confirme seu e-mail no Volumo", "Tentativa de cadastro no Volumo"]);
        await using var db = BancoDeTestes.NovoContexto();
        (await db.Users.CountAsync(u => u.NormalizedEmail == email.ToUpperInvariant(), Ct)).ShouldBe(1);
    }

    [Theory]
    [InlineData("curta")]
    [InlineData("aaaaaaaaaaaa")]
    public async Task Senha_fraca_responde_400_e_nao_cria_conta(string senha)
    {
        using var client = factory.CreateHttpsClient();
        var email = NovoEmail();

        using var response = await CadastrarPersonalAsync(client, email, senha);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problema = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Ct);
        problema!.Errors.Keys.ShouldContain("Senha");
        factory.Emails.Para(email).ShouldBeEmpty();
    }

    [Fact]
    public async Task Senha_fraca_com_email_existente_tambem_responde_400()
    {
        // A validação da senha vem antes da checagem do e-mail: a resposta não revela se ele existe.
        using var client = factory.CreateHttpsClient();
        var email = NovoEmail();
        (await CadastrarPersonalAsync(client, email)).Dispose();

        using var response = await CadastrarPersonalAsync(client, email, "curta");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cref_invalido_responde_400_com_a_mensagem_do_dominio()
    {
        using var client = factory.CreateHttpsClient();

        using var response = await CadastrarPersonalAsync(client, NovoEmail(), cref: "123");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))!.Detail!.ShouldContain("CREF inválido");
    }

    [Fact]
    public async Task Cref_ja_cadastrado_responde_409()
    {
        using var client = factory.CreateHttpsClient();
        var cref = Cenario.NovoCref("G/RJ").Valor;
        (await CadastrarPersonalAsync(client, NovoEmail(), cref: cref)).Dispose();

        using var response = await CadastrarPersonalAsync(client, NovoEmail(), cref: cref);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Campos_obrigatorios_ausentes_respondem_400()
    {
        using var client = factory.CreateHttpsClient();

        using var response = await client.PostAsJsonAsync("/auth/cadastro/personal", new { email = "nao-e-email" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Confirmar_email_com_link_valido_funciona_e_repetir_nao_falha()
    {
        using var client = factory.CreateHttpsClient();
        var email = NovoEmail();
        (await CadastrarPersonalAsync(client, email)).Dispose();

        await ConfirmarEmailAsync(client, factory, email);
        await ConfirmarEmailAsync(client, factory, email);

        await using var db = BancoDeTestes.NovoContexto();
        (await db.Users.SingleAsync(u => u.Email == email, Ct)).EmailConfirmed.ShouldBeTrue();
    }

    [Fact]
    public async Task Confirmar_email_com_token_adulterado_ou_usuario_errado_responde_400()
    {
        using var client = factory.CreateHttpsClient();
        var email = NovoEmail();
        (await CadastrarPersonalAsync(client, email)).Dispose();
        var (usuarioId, token) = factory.Emails.LinkDeConfirmacao(email);

        using var adulterado = await client.PostAsJsonAsync("/auth/confirmar-email", new ConfirmacaoDeEmail(usuarioId, token[..^4] + "AAAA"), Ct);
        using var outroUsuario = await client.PostAsJsonAsync("/auth/confirmar-email", new ConfirmacaoDeEmail(Guid.CreateVersion7(), token), Ct);
        using var lixo = await client.PostAsJsonAsync("/auth/confirmar-email", new ConfirmacaoDeEmail(usuarioId, "%%%"), Ct);

        adulterado.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        outroUsuario.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        lixo.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reenviar_confirmacao_responde_202_sempre_e_so_envia_para_conta_nao_confirmada()
    {
        using var client = factory.CreateHttpsClient();
        var naoConfirmado = NovoEmail();
        (await CadastrarPersonalAsync(client, naoConfirmado)).Dispose();
        var inexistente = NovoEmail();

        using var r1 = await client.PostAsJsonAsync("/auth/reenviar-confirmacao", new PedidoDeReenvioDeConfirmacao(naoConfirmado), Ct);
        using var r2 = await client.PostAsJsonAsync("/auth/reenviar-confirmacao", new PedidoDeReenvioDeConfirmacao(inexistente), Ct);

        r1.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        r2.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        factory.Emails.Para(naoConfirmado).Count.ShouldBe(2);
        factory.Emails.Para(inexistente).ShouldBeEmpty();
    }
}
