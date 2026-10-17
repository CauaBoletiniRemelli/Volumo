using System.Net.Mail;

namespace Volumo.Domain.Comum;

/// <summary>Validações comuns. Toda falha vira <see cref="RegraDeNegocioException"/>.</summary>
internal static class Guarda
{
    public const decimal CargaMaximaKg = 1000m;
    public const int EmailMaximo = 256;

    public static Guid IdNovo(Guid id, string campo)
    {
        if (id == Guid.Empty || id.Version != 7)
        {
            throw new RegraDeNegocioException($"{campo} precisa ser um GUID versão 7.");
        }

        return id;
    }

    public static Guid Referencia(Guid id, string campo)
    {
        if (id == Guid.Empty)
        {
            throw new RegraDeNegocioException($"{campo} é obrigatório.");
        }

        return id;
    }

    public static string Texto(string? valor, string campo, int tamanhoMaximo)
    {
        var limpo = valor?.Trim();
        if (string.IsNullOrEmpty(limpo))
        {
            throw new RegraDeNegocioException($"{campo} é obrigatório.");
        }

        if (limpo.Length > tamanhoMaximo)
        {
            throw new RegraDeNegocioException($"{campo} pode ter no máximo {tamanhoMaximo} caracteres.");
        }

        return limpo;
    }

    public static string? TextoOpcional(string? valor, string campo, int tamanhoMaximo) =>
        string.IsNullOrWhiteSpace(valor) ? null : Texto(valor, campo, tamanhoMaximo);

    public static int Intervalo(int valor, int minimo, int maximo, string campo)
    {
        if (valor < minimo || valor > maximo)
        {
            throw new RegraDeNegocioException($"{campo} precisa estar entre {minimo} e {maximo}.");
        }

        return valor;
    }

    public static decimal Carga(decimal valor, string campo)
    {
        if (valor < 0 || valor > CargaMaximaKg)
        {
            throw new RegraDeNegocioException($"{campo} precisa estar entre 0 e {CargaMaximaKg:0} kg.");
        }

        if (decimal.Round(valor, 2) != valor)
        {
            throw new RegraDeNegocioException($"{campo} pode ter no máximo duas casas decimais.");
        }

        return valor;
    }

    public static T EnumValido<T>(T valor, string campo)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(valor))
        {
            throw new RegraDeNegocioException($"{campo} inválido.");
        }

        return valor;
    }

    /// <summary>Valida e normaliza (sem espaços, minúsculo) um e-mail.</summary>
    public static string Email(string? valor)
    {
        var limpo = valor?.Trim();
        if (string.IsNullOrEmpty(limpo)
            || limpo.Length > EmailMaximo
            || !MailAddress.TryCreate(limpo, out var endereco)
            || endereco.Address != limpo)
        {
            throw new RegraDeNegocioException("E-mail inválido.");
        }

        return limpo.ToLowerInvariant();
    }
}
