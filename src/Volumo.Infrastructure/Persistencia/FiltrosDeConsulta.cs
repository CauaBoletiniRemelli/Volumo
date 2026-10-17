namespace Volumo.Infrastructure.Persistencia;

/// <summary>Nomes dos filtros globais do EF, para desligar um filtro específico com <c>IgnoreQueryFilters([...])</c>.</summary>
public static class FiltrosDeConsulta
{
    /// <summary>Esconde registros inativos (<c>InativadoEm</c> preenchido).</summary>
    public const string Ativos = "Ativos";

    /// <summary>
    /// Mostra só o que pertence ao usuário da requisição (proteção contra IDOR). Desligar só em
    /// operações que precisam enxergar dados de outra conta de forma controlada, como aceitar um
    /// convite pelo token ou a anonimização da conta.
    /// </summary>
    public const string Dono = "Dono";
}
