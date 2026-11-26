using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;

namespace Volumo.Api.Tests.Support;

/// <summary>
/// Gera códigos TOTP (RFC 6238) como um aplicativo autenticador, para os testes do 2FA.
/// HMAC-SHA1 é o algoritmo do padrão TOTP e o que o Identity e os aplicativos usam.
/// </summary>
internal static class Totp
{
    private const string Alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Codigo(string chaveBase32, DateTimeOffset? quando = null)
    {
        var chave = DecodificarBase32(chaveBase32);
        Span<byte> contador = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(contador, (quando ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds() / 30);

#pragma warning disable CA5350 // TOTP (RFC 6238) é definido sobre HMAC-SHA1; usado aqui só para gerar códigos de teste.
        var hash = HMACSHA1.HashData(chave, contador);
#pragma warning restore CA5350

        var deslocamento = hash[^1] & 0x0F;
        var binario = ((hash[deslocamento] & 0x7F) << 24)
            | (hash[deslocamento + 1] << 16)
            | (hash[deslocamento + 2] << 8)
            | hash[deslocamento + 3];
        return (binario % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    private static byte[] DecodificarBase32(string texto)
    {
        var limpo = texto.Replace(" ", string.Empty, StringComparison.Ordinal).TrimEnd('=').ToUpperInvariant();
        var bytes = new List<byte>(limpo.Length * 5 / 8);
        int buffer = 0, bits = 0;
        foreach (var c in limpo)
        {
            buffer = (buffer << 5) | Alfabeto.IndexOf(c, StringComparison.Ordinal);
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                bytes.Add((byte)(buffer >> bits));
            }
        }

        return [.. bytes];
    }
}
