using System.Security.Cryptography;
using Volumo.Domain.Comum;
using Volumo.Domain.Vinculos;

namespace Volumo.Domain.Tests.Vinculos;

public sealed class ConviteAlunoTests
{
    private readonly Guid _personalId = Dados.NovoId();
    private readonly Guid _alunoId = Dados.NovoId();

    [Fact]
    public void Convite_guarda_so_o_hash_do_token()
    {
        var (convite, token) = Criar();

        token.ShouldNotBeNullOrWhiteSpace();
        convite.TokenHash.Length.ShouldBe(32);
        convite.TokenHash.ShouldBe(ConviteAluno.CalcularHash(token));
        convite.ExpiraEm.ShouldBe(Dados.Agora + ConviteAluno.Validade);
    }

    [Fact]
    public void Tokens_sao_diferentes_a_cada_convite()
    {
        var (_, primeiro) = Criar();
        var (_, segundo) = Criar();

        primeiro.ShouldNotBe(segundo);
    }

    [Fact]
    public void Email_e_normalizado()
    {
        var (convite, _) = ConviteAluno.Criar(Dados.NovoId(), _personalId, "  Aluno@Exemplo.COM ", Dados.Agora);

        convite.Email.ShouldBe("aluno@exemplo.com");
    }

    [Theory]
    [InlineData("")]
    [InlineData("sem-arroba")]
    [InlineData("Nome <aluno@exemplo.com>")]
    public void Email_invalido_e_rejeitado(string email)
    {
        Should.Throw<RegraDeNegocioException>(() => ConviteAluno.Criar(Dados.NovoId(), _personalId, email, Dados.Agora));
    }

    [Fact]
    public void Aceitar_com_token_certo_cria_vinculo_ativo()
    {
        var (convite, token) = Criar();
        var momento = Dados.Agora.AddDays(1);

        var vinculo = convite.Aceitar(token, _alunoId, momento);

        vinculo.PersonalId.ShouldBe(_personalId);
        vinculo.AlunoId.ShouldBe(_alunoId);
        vinculo.IniciadoEm.ShouldBe(momento);
        vinculo.Ativo.ShouldBeTrue();
        convite.AceitoEm.ShouldBe(momento);
        convite.AlunoId.ShouldBe(_alunoId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("token-invalido")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void Aceitar_com_token_errado_falha(string tokenErrado)
    {
        var (convite, _) = Criar();

        Should.Throw<RegraDeNegocioException>(() => convite.Aceitar(tokenErrado, _alunoId, Dados.Agora));
        convite.AceitoEm.ShouldBeNull();
    }

    [Fact]
    public void Token_de_outro_convite_nao_serve()
    {
        var (convite, _) = Criar();
        var (_, tokenDeOutro) = Criar();

        Should.Throw<RegraDeNegocioException>(() => convite.Aceitar(tokenDeOutro, _alunoId, Dados.Agora));
    }

    [Fact]
    public void Convite_expirado_nao_pode_ser_aceito()
    {
        var (convite, token) = Criar();

        Should.Throw<RegraDeNegocioException>(() => convite.Aceitar(token, _alunoId, convite.ExpiraEm));
    }

    [Fact]
    public void Convite_nao_pode_ser_aceito_duas_vezes()
    {
        var (convite, token) = Criar();
        convite.Aceitar(token, _alunoId, Dados.Agora);

        Should.Throw<RegraDeNegocioException>(() => convite.Aceitar(token, Dados.NovoId(), Dados.Agora));
    }

    [Fact]
    public void Convite_revogado_nao_pode_ser_aceito()
    {
        var (convite, token) = Criar();
        convite.Revogar(Dados.Agora);

        Should.Throw<RegraDeNegocioException>(() => convite.Aceitar(token, _alunoId, Dados.Agora));
    }

    [Fact]
    public void Convite_aceito_nao_pode_ser_revogado()
    {
        var (convite, token) = Criar();
        convite.Aceitar(token, _alunoId, Dados.Agora);

        Should.Throw<RegraDeNegocioException>(() => convite.Revogar(Dados.Agora));
    }

    [Fact]
    public void Anonimizar_convite_pendente_revoga_e_apaga_o_email()
    {
        var (convite, token) = Criar();

        convite.Anonimizar(Dados.Agora);

        convite.Email.ShouldBe(ConviteAluno.EmailAnonimizado);
        convite.RevogadoEm.ShouldBe(Dados.Agora);
        Should.Throw<RegraDeNegocioException>(() => convite.Aceitar(token, _alunoId, Dados.Agora));
    }

    [Fact]
    public void Anonimizar_convite_aceito_so_apaga_o_email()
    {
        var (convite, token) = Criar();
        convite.Aceitar(token, _alunoId, Dados.Agora);

        convite.Anonimizar(Dados.Agora.AddDays(1));

        convite.Email.ShouldBe(ConviteAluno.EmailAnonimizado);
        convite.RevogadoEm.ShouldBeNull();
        convite.AceitoEm.ShouldBe(Dados.Agora);
    }

    [Fact]
    public void Situacao_acompanha_o_ciclo_de_vida_do_convite()
    {
        var (pendente, _) = Criar();
        var (aceito, token) = Criar();
        aceito.Aceitar(token, _alunoId, Dados.Agora);
        var (revogado, _) = Criar();
        revogado.Revogar(Dados.Agora);

        pendente.SituacaoEm(Dados.Agora).ShouldBe(SituacaoDoConvite.Pendente);
        pendente.SituacaoEm(pendente.ExpiraEm).ShouldBe(SituacaoDoConvite.Expirado);
        aceito.SituacaoEm(aceito.ExpiraEm.AddDays(30)).ShouldBe(SituacaoDoConvite.Aceito);
        revogado.SituacaoEm(Dados.Agora).ShouldBe(SituacaoDoConvite.Revogado);
    }

    [Fact]
    public void Hash_de_token_mal_formado_e_nulo()
    {
        ConviteAluno.CalcularHash(null).ShouldBeNull();
        ConviteAluno.CalcularHash("!!!").ShouldBeNull();
        ConviteAluno.CalcularHash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))).ShouldBeNull();
        ConviteAluno.CalcularHash(new string('A', 10_000)).ShouldBeNull();
    }

    private (ConviteAluno Convite, string Token) Criar() =>
        ConviteAluno.Criar(Dados.NovoId(), _personalId, "aluno@exemplo.com", Dados.Agora);
}
