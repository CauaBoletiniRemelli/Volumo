using Volumo.Domain.Comum;
using Volumo.Domain.Sessoes;

namespace Volumo.Domain.Tests.Sessoes;

public sealed class SessaoTreinoTests
{
    private readonly Guid _exercicioId = Dados.NovoId();

    [Fact]
    public void Registrar_serie_adiciona_o_registro()
    {
        var sessao = Iniciar();

        var registro = sessao.RegistrarSerie(Dados.NovoId(), _exercicioId, 10, 40m, rir: 2, Dados.Agora.AddMinutes(5));

        sessao.Registros.ShouldBe([registro]);
        registro.SessaoId.ShouldBe(sessao.Id);
        registro.Rir.ShouldBe(2);
    }

    [Fact]
    public void Registrar_a_mesma_serie_duas_vezes_nao_duplica()
    {
        var sessao = Iniciar();
        var registroId = Dados.NovoId();

        var primeiro = sessao.RegistrarSerie(registroId, _exercicioId, 10, 40m, null, Dados.Agora.AddMinutes(5));
        var reenvio = sessao.RegistrarSerie(registroId, _exercicioId, 10, 40m, null, Dados.Agora.AddMinutes(5));

        reenvio.ShouldBeSameAs(primeiro);
        sessao.Registros.Count.ShouldBe(1);
    }

    [Fact]
    public void Registros_ficam_em_ordem_cronologica()
    {
        var sessao = Iniciar();
        var depois = sessao.RegistrarSerie(Dados.NovoId(), _exercicioId, 8, 40m, null, Dados.Agora.AddMinutes(10));
        var antes = sessao.RegistrarSerie(Dados.NovoId(), _exercicioId, 10, 40m, null, Dados.Agora.AddMinutes(5));

        sessao.Registros.ShouldBe([antes, depois]);
    }

    [Fact]
    public void Serie_antes_do_inicio_da_sessao_e_rejeitada()
    {
        Should.Throw<RegraDeNegocioException>(() =>
            Iniciar().RegistrarSerie(Dados.NovoId(), _exercicioId, 10, 40m, null, Dados.Agora.AddMinutes(-1)));
    }

    [Theory]
    [InlineData(-1, 40, null)]
    [InlineData(201, 40, null)]
    [InlineData(10, -1, null)]
    [InlineData(10, 1001, null)]
    [InlineData(10, 40, 11)]
    [InlineData(10, 40, -1)]
    public void Valores_fora_dos_limites_sao_rejeitados(int repeticoes, int carga, int? rir)
    {
        Should.Throw<RegraDeNegocioException>(() =>
            Iniciar().RegistrarSerie(Dados.NovoId(), _exercicioId, repeticoes, carga, rir, Dados.Agora));
    }

    [Fact]
    public void Sessao_finalizada_nao_aceita_novas_series()
    {
        var sessao = Iniciar();
        sessao.Finalizar(Dados.Agora.AddHours(1));

        Should.Throw<RegraDeNegocioException>(() =>
            sessao.RegistrarSerie(Dados.NovoId(), _exercicioId, 10, 40m, null, Dados.Agora.AddMinutes(30)));
    }

    [Fact]
    public void Reenvio_de_serie_ja_registrada_funciona_mesmo_depois_de_finalizar()
    {
        var sessao = Iniciar();
        var registroId = Dados.NovoId();
        sessao.RegistrarSerie(registroId, _exercicioId, 10, 40m, null, Dados.Agora.AddMinutes(5));
        sessao.Finalizar(Dados.Agora.AddHours(1));

        Should.NotThrow(() => sessao.RegistrarSerie(registroId, _exercicioId, 10, 40m, null, Dados.Agora.AddMinutes(5)));
    }

    [Fact]
    public void Finalizar_de_novo_mantem_o_primeiro_horario()
    {
        var sessao = Iniciar();
        sessao.Finalizar(Dados.Agora.AddHours(1));

        sessao.Finalizar(Dados.Agora.AddHours(2));

        sessao.FinalizadaEm.ShouldBe(Dados.Agora.AddHours(1));
    }

    [Fact]
    public void Fim_antes_do_inicio_ou_das_series_e_rejeitado()
    {
        var sessao = Iniciar();
        sessao.RegistrarSerie(Dados.NovoId(), _exercicioId, 10, 40m, null, Dados.Agora.AddMinutes(30));

        Should.Throw<RegraDeNegocioException>(() => sessao.Finalizar(Dados.Agora.AddMinutes(-1)));
        Should.Throw<RegraDeNegocioException>(() => sessao.Finalizar(Dados.Agora.AddMinutes(10)));
        sessao.Finalizada.ShouldBeFalse();
    }

    [Fact]
    public void Id_do_registro_precisa_ser_guid_v7()
    {
        Should.Throw<RegraDeNegocioException>(() =>
            Iniciar().RegistrarSerie(Guid.NewGuid(), _exercicioId, 10, 40m, null, Dados.Agora));
    }

    [Fact]
    public void Cardio_fica_separado_das_series_e_nao_duplica_no_reenvio()
    {
        var sessao = Iniciar();
        var cardioId = Dados.NovoId();

        var cardio = sessao.RegistrarCardio(cardioId, _exercicioId, 20, Dados.Agora.AddMinutes(25));
        sessao.RegistrarCardio(cardioId, _exercicioId, 20, Dados.Agora.AddMinutes(25));

        sessao.Cardios.ShouldBe([cardio]);
        sessao.Registros.ShouldBeEmpty();
        cardio.SessaoId.ShouldBe(sessao.Id);
        cardio.DuracaoMinutos.ShouldBe(20);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(RegistroCardio.DuracaoMaximaMinutos + 1)]
    public void Cardio_com_duracao_fora_dos_limites_falha(int minutos) =>
        Should.Throw<RegraDeNegocioException>(() => Iniciar().RegistrarCardio(Dados.NovoId(), _exercicioId, minutos, Dados.Agora.AddMinutes(5)));

    [Fact]
    public void Cardio_antes_do_inicio_ou_em_sessao_finalizada_falha()
    {
        var sessao = Iniciar();
        Should.Throw<RegraDeNegocioException>(() => sessao.RegistrarCardio(Dados.NovoId(), _exercicioId, 10, Dados.Agora.AddMinutes(-1)));

        sessao.Finalizar(Dados.Agora.AddMinutes(30));
        Should.Throw<RegraDeNegocioException>(() => sessao.RegistrarCardio(Dados.NovoId(), _exercicioId, 10, Dados.Agora.AddMinutes(10)));
    }

    [Fact]
    public void Fim_antes_do_ultimo_cardio_falha()
    {
        var sessao = Iniciar();
        sessao.RegistrarCardio(Dados.NovoId(), _exercicioId, 20, Dados.Agora.AddMinutes(40));

        Should.Throw<RegraDeNegocioException>(() => sessao.Finalizar(Dados.Agora.AddMinutes(30)));
    }

    private static SessaoTreino Iniciar() => SessaoTreino.Iniciar(Dados.NovoId(), Dados.NovoId(), Dados.NovoId(), Dados.Agora);
}
