using Volumo.Domain.Comum;
using Volumo.Domain.Treinos;

namespace Volumo.Domain.Tests.Treinos;

public sealed class AnotacaoDoAlunoTests
{
    [Fact]
    public void Atualizar_com_mudanca_mais_nova_troca_o_texto()
    {
        var anotacao = Criar("Banco na posição 3");

        anotacao.Atualizar("Banco na 4", Dados.Agora.AddMinutes(1)).ShouldBeTrue();

        anotacao.Texto.ShouldBe("Banco na 4");
        anotacao.AtualizadaEm.ShouldBe(Dados.Agora.AddMinutes(1));
    }

    [Fact]
    public void Atualizar_com_mudanca_mais_antiga_nao_muda_nada()
    {
        var anotacao = Criar("Banco na posição 3");

        anotacao.Atualizar("Texto antigo", Dados.Agora.AddMinutes(-1)).ShouldBeFalse();

        anotacao.Texto.ShouldBe("Banco na posição 3");
    }

    [Fact]
    public void Texto_em_branco_limpa_e_texto_longo_demais_e_recusado()
    {
        var anotacao = Criar("Banco na posição 3");

        anotacao.Atualizar("   ", Dados.Agora.AddMinutes(1));

        anotacao.Texto.ShouldBeNull();
        Should.Throw<RegraDeNegocioException>(() => anotacao.Atualizar(new string('x', AnotacaoDoAluno.TextoMaximo + 1), Dados.Agora.AddMinutes(2)));
    }

    private static AnotacaoDoAluno Criar(string texto) =>
        AnotacaoDoAluno.Criar(Dados.NovoId(), Dados.NovoId(), Dados.NovoId(), Dados.NovoId(), texto, Dados.Agora);
}
