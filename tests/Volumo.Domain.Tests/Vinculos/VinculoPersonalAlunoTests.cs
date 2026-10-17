using Volumo.Domain.Comum;
using Volumo.Domain.Vinculos;

namespace Volumo.Domain.Tests.Vinculos;

public sealed class VinculoPersonalAlunoTests
{
    [Fact]
    public void Encerrar_desativa_o_vinculo()
    {
        var vinculo = Criar();

        vinculo.Encerrar(Dados.Agora.AddMonths(3));

        vinculo.Ativo.ShouldBeFalse();
        vinculo.EncerradoEm.ShouldBe(Dados.Agora.AddMonths(3));
    }

    [Fact]
    public void Vinculo_nao_pode_ser_encerrado_duas_vezes()
    {
        var vinculo = Criar();
        vinculo.Encerrar(Dados.Agora.AddDays(1));

        Should.Throw<RegraDeNegocioException>(() => vinculo.Encerrar(Dados.Agora.AddDays(2)));
    }

    [Fact]
    public void Encerramento_antes_do_inicio_e_rejeitado()
    {
        var vinculo = Criar();

        Should.Throw<RegraDeNegocioException>(() => vinculo.Encerrar(Dados.Agora.AddMinutes(-1)));
    }

    [Fact]
    public void Vinculo_sem_aluno_e_rejeitado()
    {
        Should.Throw<RegraDeNegocioException>(() =>
            VinculoPersonalAluno.Criar(Dados.NovoId(), Dados.NovoId(), Guid.Empty, Dados.Agora));
    }

    private static VinculoPersonalAluno Criar() =>
        VinculoPersonalAluno.Criar(Dados.NovoId(), Dados.NovoId(), Dados.NovoId(), Dados.Agora);
}
