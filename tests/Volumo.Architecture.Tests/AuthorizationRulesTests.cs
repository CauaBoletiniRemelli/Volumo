using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Volumo.Architecture.Tests;

/// <summary>
/// Toda ação de controller declara quem pode acessá-la ([Authorize] com ou sem política, ou
/// [AllowAnonymous]), sem depender só da política padrão; e rotas anônimas só existem onde foram
/// aprovadas. Assim uma rota nova não fica aberta (nem fechada) por esquecimento.
/// </summary>
public sealed class AuthorizationRulesTests
{
    /// <summary>Únicos lugares onde rota anônima é permitida.</summary>
    private static readonly HashSet<string> AnonymousAllowed =
    [
        "AuthController.*",
        "ConvitesController.Preview",

        // Arquivos do armazenamento em disco (dev/testes): o token cifrado e com prazo na URL é a
        // autorização, como a URL assinada do R2 (tags <img>/<video> não mandam o token de acesso).
        "ArquivosController.Get",
    ];

    private static readonly Assembly Api = typeof(Api.Auth.Policies).Assembly;

    [Fact]
    public void Toda_acao_declara_quem_pode_acessar()
    {
        var semDeclaracao = Actions()
            .Where(a => !Has<IAuthorizeData>(a) && !Has<IAllowAnonymous>(a))
            .Select(Name)
            .ToList();

        semDeclaracao.ShouldBeEmpty("Ações sem [Authorize] nem [AllowAnonymous]: " + string.Join(", ", semDeclaracao));
    }

    [Fact]
    public void Rota_anonima_so_onde_foi_aprovada()
    {
        var naoAprovadas = Actions()
            .Where(a => Has<IAllowAnonymous>(a))
            .Select(Name)
            .Where(nome => !AnonymousAllowed.Contains(nome) && !AnonymousAllowed.Contains(nome.Split('.')[0] + ".*"))
            .ToList();

        naoAprovadas.ShouldBeEmpty("Rotas anônimas não aprovadas: " + string.Join(", ", naoAprovadas));
    }

    private static IEnumerable<MethodInfo> Actions() =>
        Api.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttributes().OfType<Microsoft.AspNetCore.Mvc.Routing.IRouteTemplateProvider>().Any());

    private static bool Has<T>(MethodInfo action) =>
        action.GetCustomAttributes(inherit: true).OfType<T>().Any()
        || action.DeclaringType!.GetCustomAttributes(inherit: true).OfType<T>().Any();

    private static string Name(MethodInfo action) => $"{action.DeclaringType!.Name}.{action.Name}";
}
