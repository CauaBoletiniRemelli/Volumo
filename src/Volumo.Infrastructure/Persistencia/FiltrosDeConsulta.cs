namespace Volumo.Infrastructure.Persistencia;

/// <summary>Nomes dos filtros globais do EF, para desligar um filtro específico com <c>IgnoreQueryFilters([...])</c>.</summary>
public static class FiltrosDeConsulta
{
    /// <summary>Esconde registros inativos (<c>InativadoEm</c> preenchido).</summary>
    public const string Ativos = "Ativos";
}
