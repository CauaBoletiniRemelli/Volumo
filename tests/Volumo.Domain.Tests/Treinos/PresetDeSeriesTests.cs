using Volumo.Domain.Comum;
using Volumo.Domain.Treinos;

namespace Volumo.Domain.Tests.Treinos;

public sealed class PresetDeSeriesTests
{
    private static Serie[] SemCarga(int quantidade) => [.. Enumerable.Range(0, quantidade).Select(_ => Dados.SerieDeTrabalho(cargaKg: null))];

    [Fact]
    public void Preset_guarda_nome_dono_e_series_na_ordem()
    {
        var dono = Dados.NovoId();

        var preset = PresetDeSeries.Criar(Dados.NovoId(), dono, "  Pirâmide  ", [Dados.SerieDeTrabalho(10, 10, null), Dados.SerieDeTrabalho(5, 5, null)]);

        preset.Nome.ShouldBe("Pirâmide");
        preset.DonoId.ShouldBe(dono);
        preset.Pronto.ShouldBeFalse();
        preset.Series.Select(s => (s.Ordem, s.RepeticoesMinimas)).ShouldBe([(1, 10), (2, 5)]);
    }

    [Fact]
    public void Preset_guarda_so_a_estrutura_sem_carga()
    {
        Should.Throw<RegraDeNegocioException>(() => PresetDeSeries.Criar(Dados.NovoId(), Dados.NovoId(), "Com carga", [Dados.SerieDeTrabalho(cargaKg: 20m)]))
            .Message.ShouldContain("sem carga");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void Quantidade_de_series_segue_o_limite_do_exercicio(int quantidade)
    {
        Should.Throw<RegraDeNegocioException>(() => PresetDeSeries.Criar(Dados.NovoId(), Dados.NovoId(), "Preset", SemCarga(quantidade)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Nome_e_obrigatorio(string nome)
    {
        Should.Throw<RegraDeNegocioException>(() => PresetDeSeries.Criar(Dados.NovoId(), Dados.NovoId(), nome, SemCarga(3)));
    }

    [Fact]
    public void Nome_tem_tamanho_maximo()
    {
        Should.Throw<RegraDeNegocioException>(() =>
            PresetDeSeries.Criar(Dados.NovoId(), Dados.NovoId(), new string('x', PresetDeSeries.NomeMaximo + 1), SemCarga(3)));
    }

    [Fact]
    public void Trocar_as_series_substitui_todas()
    {
        var preset = PresetDeSeries.Criar(Dados.NovoId(), Dados.NovoId(), "Preset", SemCarga(3));

        preset.DefinirSeries(SemCarga(5));

        preset.Series.Count.ShouldBe(5);
        preset.Series.Select(s => s.Ordem).ShouldBe([1, 2, 3, 4, 5]);
    }

    [Fact]
    public void Series_invalidas_nao_mudam_nada()
    {
        var preset = PresetDeSeries.Criar(Dados.NovoId(), Dados.NovoId(), "Preset", SemCarga(3));

        Should.Throw<RegraDeNegocioException>(() => preset.DefinirSeries([Dados.SerieDeTrabalho(cargaKg: 10m)]));

        preset.Series.Count.ShouldBe(3);
    }

    [Fact]
    public void Preset_inativo_nao_pode_ser_alterado()
    {
        var preset = PresetDeSeries.Criar(Dados.NovoId(), Dados.NovoId(), "Preset", SemCarga(3));
        preset.Inativar(Dados.Agora);

        Should.Throw<RegraDeNegocioException>(() => preset.Renomear("Outro"));
        Should.Throw<RegraDeNegocioException>(() => preset.DefinirSeries(SemCarga(2)));
    }

    [Fact]
    public void Prontos_sao_validos_sem_dono_e_com_ids_unicos()
    {
        PresetDeSeries.Prontos.ShouldNotBeEmpty();
        PresetDeSeries.Prontos.ShouldAllBe(p => p.Pronto && p.DonoId == null && p.Ativo);
        PresetDeSeries.Prontos.Select(p => p.Id).Distinct().Count().ShouldBe(PresetDeSeries.Prontos.Count);
        PresetDeSeries.Prontos.Select(p => p.Id.Version).ShouldAllBe(v => v == 7);
    }

    [Fact]
    public void Piramide_crescente_desce_as_repeticoes()
    {
        var piramide = PresetDeSeries.Prontos.Single(p => p.Nome == "Pirâmide crescente");

        piramide.Series.Select(s => s.RepeticoesMaximas).ShouldBe([10, 8, 5, 3]);
    }
}
