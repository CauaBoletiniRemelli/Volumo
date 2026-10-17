using Volumo.Domain.Comum;
using Volumo.Domain.Usuarios;

namespace Volumo.Domain.Tests.Usuarios;

public sealed class PerfisTests
{
    [Theory]
    [InlineData("123456-G/SP", "123456-G/SP")]
    [InlineData(" 123456-g/sp ", "123456-G/SP")]
    [InlineData("1234-P/RJ", "001234-P/RJ")]
    [InlineData("012345 - G / MG", "012345-G/MG")]
    public void Cref_valido_e_normalizado(string entrada, string esperado)
    {
        Cref.Criar(entrada).Valor.ShouldBe(esperado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123456")]
    [InlineData("123456-X/SP")]
    [InlineData("123456-G/XX")]
    [InlineData("1234567-G/SP")]
    [InlineData("١٢٣٤٥٦-G/SP")]
    [InlineData("123456-G/SP; DROP TABLE")]
    public void Cref_invalido_e_rejeitado(string? entrada)
    {
        Should.Throw<RegraDeNegocioException>(() => Cref.Criar(entrada));
    }

    [Fact]
    public void Crefs_com_mesmo_valor_sao_iguais()
    {
        Cref.Criar("1234-G/SP").ShouldBe(Cref.Criar("001234-G/SP"));
    }

    [Fact]
    public void Perfil_usa_o_id_do_usuario_e_limpa_o_nome()
    {
        var usuarioId = Dados.NovoId();

        var perfil = PerfilPersonal.Criar(usuarioId, "  Ana Souza  ", Cref.Criar("123456-G/SP"));

        perfil.Id.ShouldBe(usuarioId);
        perfil.Nome.ShouldBe("Ana Souza");
        perfil.Cref!.Valor.ShouldBe("123456-G/SP");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Perfil_sem_nome_e_rejeitado(string nome)
    {
        Should.Throw<RegraDeNegocioException>(() => PerfilAluno.Criar(Dados.NovoId(), nome));
    }

    [Fact]
    public void Perfil_com_nome_longo_demais_e_rejeitado()
    {
        Should.Throw<RegraDeNegocioException>(() => PerfilAluno.Criar(Dados.NovoId(), new string('a', PerfilAluno.NomeMaximo + 1)));
    }

    [Fact]
    public void Anonimizar_apaga_os_dados_pessoais_e_inativa()
    {
        var perfil = PerfilPersonal.Criar(Dados.NovoId(), "Ana Souza", Cref.Criar("123456-G/SP"));

        perfil.Anonimizar(Dados.Agora);

        perfil.Nome.ShouldBe(Perfil.NomeAnonimizado);
        perfil.Cref.ShouldBeNull();
        perfil.AnonimizadoEm.ShouldBe(Dados.Agora);
        perfil.InativadoEm.ShouldBe(Dados.Agora);
    }

    [Fact]
    public void Anonimizar_de_novo_mantem_a_data_original()
    {
        var perfil = PerfilAluno.Criar(Dados.NovoId(), "Bruno");
        perfil.Anonimizar(Dados.Agora);

        perfil.Anonimizar(Dados.Agora.AddDays(1));

        perfil.AnonimizadoEm.ShouldBe(Dados.Agora);
    }

    [Fact]
    public void Perfil_anonimizado_nao_pode_ser_reativado_nem_alterado()
    {
        var perfil = PerfilPersonal.Criar(Dados.NovoId(), "Ana");
        perfil.Anonimizar(Dados.Agora);

        Should.Throw<RegraDeNegocioException>(perfil.Reativar);
        Should.Throw<RegraDeNegocioException>(() => perfil.Renomear("Ana de volta"));
        Should.Throw<RegraDeNegocioException>(() => perfil.DefinirCref(Cref.Criar("123456-G/SP")));
    }

    [Fact]
    public void Perfil_apenas_inativado_pode_ser_reativado()
    {
        var perfil = PerfilAluno.Criar(Dados.NovoId(), "Bruno");
        perfil.Inativar(Dados.Agora);

        perfil.Reativar();

        perfil.Ativo.ShouldBeTrue();
        perfil.Nome.ShouldBe("Bruno");
    }

    [Fact]
    public void Id_que_nao_e_guid_v7_e_rejeitado()
    {
        Should.Throw<RegraDeNegocioException>(() => PerfilAluno.Criar(Guid.NewGuid(), "Bruno"));
        Should.Throw<RegraDeNegocioException>(() => PerfilAluno.Criar(Guid.Empty, "Bruno"));
    }
}
