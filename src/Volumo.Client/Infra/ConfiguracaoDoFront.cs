namespace Volumo.Client.Infra;

/// <summary>Configuração lida de wwwroot/appsettings.json (público: nada de segredo aqui).</summary>
public sealed record ConfiguracaoDoFront(Uri ApiBaseUrl, string TurnstileSiteKey);
