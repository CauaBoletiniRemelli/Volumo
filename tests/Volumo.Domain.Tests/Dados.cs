using Volumo.Domain.Treinos;

namespace Volumo.Domain.Tests;

/// <summary>Valores prontos para os testes do domínio.</summary>
internal static class Dados
{
    public static readonly DateTimeOffset Agora = new(2026, 10, 16, 18, 0, 0, TimeSpan.FromHours(-3));

    public static Guid NovoId() => Guid.CreateVersion7();

    public static Serie SerieDeTrabalho(int repeticoesMinimas = 8, int repeticoesMaximas = 12, decimal? cargaKg = 20m) =>
        Serie.Criar(TipoSerie.Trabalho, repeticoesMinimas, repeticoesMaximas, descansoSegundos: 90, cargaKg);

    public static Serie[] Series(int quantidade) => [.. Enumerable.Range(0, quantidade).Select(_ => SerieDeTrabalho())];
}
