using Volumo.Domain.Sessoes;

namespace Volumo.Domain.Tests.Sessoes;

public sealed class AnaliseDeEstagnacaoTests
{
    private static readonly DateTimeOffset Agora = Dados.Agora;

    [Fact]
    public void Por_tempo_aponta_quem_esta_ha_15_dias_sem_subir_peso()
    {
        var resultado = Avaliar(CriterioDeEstagnacao.Tempo, 15, (20, 40m), (13, 40m), (6, 40m), (1, 37.5m));

        resultado.ShouldNotBeNull();
        resultado.MaiorCargaKg.ShouldBe(40m);
        resultado.UltimoAumentoEm.ShouldBe(Agora.AddDays(-20));
        resultado.SessoesDesdeOAumento.ShouldBe(3);
    }

    [Fact]
    public void Subir_peso_zera_a_contagem()
    {
        Avaliar(CriterioDeEstagnacao.Tempo, 15, (30, 40m), (20, 40m), (10, 40m), (2, 42.5m)).ShouldBeNull();
        Avaliar(CriterioDeEstagnacao.Sessoes, 3, (12, 40m), (9, 40m), (6, 40m), (3, 42.5m)).ShouldBeNull();
    }

    [Fact]
    public void Repetir_a_maior_carga_nao_conta_como_subir()
    {
        var resultado = Avaliar(CriterioDeEstagnacao.Sessoes, 3, (12, 40m), (9, 40m), (6, 40m), (3, 40m));

        resultado.ShouldNotBeNull();
        resultado.SessoesDesdeOAumento.ShouldBe(3);
    }

    [Fact]
    public void Por_sessoes_espera_o_numero_configurado()
    {
        Avaliar(CriterioDeEstagnacao.Sessoes, 4, (12, 40m), (9, 40m), (6, 40m), (3, 40m)).ShouldBeNull();
        Avaliar(CriterioDeEstagnacao.Sessoes, 3, (12, 40m), (9, 40m), (6, 40m), (3, 40m)).ShouldNotBeNull();
    }

    [Fact]
    public void Por_tempo_ignora_exercicio_que_o_aluno_parou_de_fazer()
    {
        // Última vez há 40 dias: não está "parado na carga", só deixou de fazer o exercício.
        Avaliar(CriterioDeEstagnacao.Tempo, 15, (60, 40m), (50, 40m), (40, 40m)).ShouldBeNull();
    }

    [Fact]
    public void Por_tempo_precisa_de_uma_sessao_depois_do_ultimo_aumento()
    {
        // Subiu há 20 dias e não fez mais o exercício desde então: nada a dizer.
        Avaliar(CriterioDeEstagnacao.Tempo, 30, (40, 35m), (20, 40m)).ShouldBeNull();
    }

    [Fact]
    public void Peso_corporal_e_poucas_sessoes_nao_entram()
    {
        Avaliar(CriterioDeEstagnacao.Sessoes, 2, (9, 0m), (6, 0m), (3, 0m)).ShouldBeNull();
        Avaliar(CriterioDeEstagnacao.Sessoes, 2, (3, 40m)).ShouldBeNull();
    }

    [Theory]
    [InlineData(CriterioDeEstagnacao.Tempo, 7)]
    [InlineData(CriterioDeEstagnacao.Tempo, 90)]
    [InlineData(CriterioDeEstagnacao.Sessoes, 2)]
    [InlineData(CriterioDeEstagnacao.Sessoes, 10)]
    public void Limites_validos(CriterioDeEstagnacao criterio, int limite) =>
        AnaliseDeEstagnacao.ValidarLimite(criterio, limite).ShouldBe(limite);

    [Theory]
    [InlineData(CriterioDeEstagnacao.Tempo, 6)]
    [InlineData(CriterioDeEstagnacao.Tempo, 91)]
    [InlineData(CriterioDeEstagnacao.Sessoes, 1)]
    [InlineData(CriterioDeEstagnacao.Sessoes, 11)]
    [InlineData((CriterioDeEstagnacao)99, 15)]
    public void Limites_invalidos_sao_recusados(CriterioDeEstagnacao criterio, int limite) =>
        Should.Throw<Comum.RegraDeNegocioException>(() => AnaliseDeEstagnacao.ValidarLimite(criterio, limite));

    private static Estagnacao? Avaliar(CriterioDeEstagnacao criterio, int limite, params (int DiasAtras, decimal Carga)[] sessoes) =>
        AnaliseDeEstagnacao.Avaliar([.. sessoes.Select(s => new CargaNaSessao(Agora.AddDays(-s.DiasAtras), s.Carga))], criterio, limite, Agora);
}
