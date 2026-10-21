using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Volumo.Client;
using Volumo.Client.Infra;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = new Uri(builder.Configuration["Api:BaseUrl"] ?? throw new InvalidOperationException("Api:BaseUrl não configurada."));
builder.Services.AddSingleton(new ConfiguracaoDoFront(apiBaseUrl, builder.Configuration["Turnstile:SiteKey"] ?? string.Empty));

builder.Services.AddSingleton<ArmazenamentoLocal>();

// Sessão e renovação usam um HttpClient sem o anexo do token (evita renovação em laço).
builder.Services.AddSingleton(sp => new SessaoDoUsuario(new HttpClient { BaseAddress = apiBaseUrl }, sp.GetRequiredService<ArmazenamentoLocal>()));
builder.Services.AddSingleton<AuthenticationStateProvider>(sp => sp.GetRequiredService<SessaoDoUsuario>());
builder.Services.AddSingleton(sp => new ApiVolumo(
    new HttpClient(new AnexarToken(sp.GetRequiredService<SessaoDoUsuario>()) { InnerHandler = new HttpClientHandler() })
    {
        BaseAddress = apiBaseUrl,
    },
    sp.GetRequiredService<SessaoDoUsuario>()));

builder.Services.AddSingleton<SincronizacaoDeSessoes>();
builder.Services.AddSingleton<TreinosDoAluno>();

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

await builder.Build().RunAsync();
