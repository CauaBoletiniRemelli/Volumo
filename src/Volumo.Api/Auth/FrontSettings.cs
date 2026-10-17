namespace Volumo.Api.Auth;

/// <summary>Endereço do front (Client), usado nos links dos e-mails (confirmação, convite, senha).</summary>
public sealed class FrontSettings
{
    public const string SectionName = "Front";

    public Uri BaseUrl { get; private init; } = null!;

    public static FrontSettings Load(IConfiguration configuration)
    {
        var value = configuration[$"{SectionName}:BaseUrl"];
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && !uri.IsLoopback)
            || uri.AbsolutePath != "/")
        {
            throw new InvalidOperationException("Front:BaseUrl precisa ser uma URL HTTPS sem caminho (ex.: https://app.volumo.com.br).");
        }

        return new FrontSettings { BaseUrl = uri };
    }

    public Uri Link(string path, IReadOnlyDictionary<string, string> query) =>
        new(BaseUrl, path + QueryString.Create(query!).ToUriComponent());
}
