using Volumo.Domain.Comum;
using Volumo.Domain.Usuarios;
using Volumo.Domain.Vinculos;

namespace Volumo.Domain.Tests.Vinculos;

public sealed class AceiteDeConviteTests
{
    private readonly PerfilAluno _aluno = PerfilAluno.Criar(Dados.NovoId(), "Bruno");
    private readonly PerfilPersonal _personalAtual = PerfilPersonal.Criar(Dados.NovoId(), "Ana");
    private readonly PerfilPersonal _novoPersonal = PerfilPersonal.Criar(Dados.NovoId(), "Carla");

    [Fact]
    public void Aluno_sem_personal_aceita_normalmente()
    {
        var (convite, token) = ConviteDe(_novoPersonal);

        var vinculo = AceiteDeConvite.Aceitar(convite, token, _aluno, null, null, Dados.Agora);

        vinculo.PersonalId.ShouldBe(_novoPersonal.Id);
        vinculo.AlunoId.ShouldBe(_aluno.Id);
    }

    [Fact]
    public void Personal_atual_inativo_tem_o_vinculo_encerrado_automaticamente()
    {
        var vinculoAtual = VinculoAtual();
        _personalAtual.Inativar(Dados.Agora.AddDays(-10));
        var (convite, token) = ConviteDe(_novoPersonal);

        var novo = AceiteDeConvite.Aceitar(convite, token, _aluno, vinculoAtual, _personalAtual, Dados.Agora);

        vinculoAtual.Ativo.ShouldBeFalse();
        vinculoAtual.EncerradoEm.ShouldBe(Dados.Agora);
        novo.PersonalId.ShouldBe(_novoPersonal.Id);
    }

    [Fact]
    public void Personal_atual_ativo_impede_o_aceite()
    {
        var vinculoAtual = VinculoAtual();
        var (convite, token) = ConviteDe(_novoPersonal);

        Should.Throw<RegraDeNegocioException>(() =>
            AceiteDeConvite.Aceitar(convite, token, _aluno, vinculoAtual, _personalAtual, Dados.Agora));

        vinculoAtual.Ativo.ShouldBeTrue();
        convite.AceitoEm.ShouldBeNull();
    }

    [Fact]
    public void Convite_do_proprio_personal_atual_e_rejeitado()
    {
        var vinculoAtual = VinculoAtual();
        var (convite, token) = ConviteDe(_personalAtual);

        Should.Throw<RegraDeNegocioException>(() =>
            AceiteDeConvite.Aceitar(convite, token, _aluno, vinculoAtual, _personalAtual, Dados.Agora));
    }

    [Fact]
    public void Token_errado_nao_encerra_o_vinculo_atual()
    {
        var vinculoAtual = VinculoAtual();
        _personalAtual.Inativar(Dados.Agora.AddDays(-10));
        var (convite, _) = ConviteDe(_novoPersonal);

        Should.Throw<RegraDeNegocioException>(() =>
            AceiteDeConvite.Aceitar(convite, "token-errado", _aluno, vinculoAtual, _personalAtual, Dados.Agora));

        vinculoAtual.Ativo.ShouldBeTrue();
    }

    [Fact]
    public void Aluno_inativo_precisa_reativar_antes()
    {
        _aluno.Inativar(Dados.Agora.AddDays(-1));
        var (convite, token) = ConviteDe(_novoPersonal);

        Should.Throw<RegraDeNegocioException>(() =>
            AceiteDeConvite.Aceitar(convite, token, _aluno, null, null, Dados.Agora));
    }

    [Fact]
    public void Vinculo_atual_sem_o_personal_correspondente_e_erro_de_programacao()
    {
        var vinculoAtual = VinculoAtual();
        var (convite, token) = ConviteDe(_novoPersonal);

        Should.Throw<ArgumentException>(() =>
            AceiteDeConvite.Aceitar(convite, token, _aluno, vinculoAtual, null, Dados.Agora));
        Should.Throw<ArgumentException>(() =>
            AceiteDeConvite.Aceitar(convite, token, _aluno, vinculoAtual, _novoPersonal, Dados.Agora));
    }

    private VinculoPersonalAluno VinculoAtual() =>
        VinculoPersonalAluno.Criar(Dados.NovoId(), _personalAtual.Id, _aluno.Id, Dados.Agora.AddMonths(-6));

    private static (ConviteAluno Convite, string Token) ConviteDe(PerfilPersonal personal) =>
        ConviteAluno.Criar(Dados.NovoId(), personal.Id, "bruno@exemplo.com", Dados.Agora.AddDays(-1));
}
