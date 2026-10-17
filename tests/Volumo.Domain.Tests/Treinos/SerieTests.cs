using Volumo.Domain.Comum;
using Volumo.Domain.Treinos;

namespace Volumo.Domain.Tests.Treinos;

public sealed class SerieTests
{
    [Fact]
    public void Serie_valida_guarda_os_valores()
    {
        var serie = Serie.Criar(TipoSerie.Aquecimento, 12, 15, 60, 10.5m);

        serie.Tipo.ShouldBe(TipoSerie.Aquecimento);
        serie.RepeticoesMinimas.ShouldBe(12);
        serie.RepeticoesMaximas.ShouldBe(15);
        serie.DescansoSegundos.ShouldBe(60);
        serie.CargaKg.ShouldBe(10.5m);
    }

    [Fact]
    public void Carga_e_opcional()
    {
        Serie.Criar(TipoSerie.Trabalho, 10, 10, 60).CargaKg.ShouldBeNull();
    }

    [Theory]
    [InlineData(0, 10, 60, 10)]
    [InlineData(12, 8, 60, 10)]
    [InlineData(8, 101, 60, 10)]
    [InlineData(8, 12, -1, 10)]
    [InlineData(8, 12, 901, 10)]
    [InlineData(8, 12, 60, -1)]
    [InlineData(8, 12, 60, 1000.01)]
    [InlineData(8, 12, 60, 10.555)]
    public void Valores_fora_dos_limites_sao_rejeitados(int minimas, int maximas, int descanso, double carga)
    {
        Should.Throw<RegraDeNegocioException>(() =>
            Serie.Criar(TipoSerie.Trabalho, minimas, maximas, descanso, (decimal)carga));
    }
}
