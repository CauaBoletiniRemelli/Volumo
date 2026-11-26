using System.Collections.Frozen;
using System.Text.RegularExpressions;
using Volumo.Domain.Comum;

namespace Volumo.Domain.Usuarios;

/// <summary>
/// Registro no Conselho Regional de Educação Física, no formato <c>000000-G/UF</c>
/// (G = graduado, P = provisionado). Aqui só se valida o formato; a consulta ao conselho fica para depois.
/// </summary>
public sealed partial record Cref
{
    public const int TamanhoMaximo = 11;

    private static readonly FrozenSet<string> Ufs = new[]
    {
        "AC", "AL", "AM", "AP", "BA", "CE", "DF", "ES", "GO", "MA", "MG", "MS", "MT", "PA",
        "PB", "PE", "PI", "PR", "RJ", "RN", "RO", "RR", "RS", "SC", "SE", "SP", "TO",
    }.ToFrozenSet();

    private Cref(string valor) => Valor = valor;

    public string Valor { get; }

    public static Cref Criar(string? valor)
    {
        var normalizado = (valor ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        var partes = Formato().Match(normalizado);
        if (!partes.Success || !Ufs.Contains(partes.Groups["uf"].Value))
        {
            throw new RegraDeNegocioException("CREF inválido. Use o formato 000000-G/UF.");
        }

        var numero = partes.Groups["numero"].Value.PadLeft(6, '0');
        return new Cref($"{numero}-{partes.Groups["categoria"].Value}/{partes.Groups["uf"].Value}");
    }

    public override string ToString() => Valor;

    [GeneratedRegex("^(?<numero>[0-9]{1,6})-(?<categoria>[GP])/(?<uf>[A-Z]{2})$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex Formato();
}
