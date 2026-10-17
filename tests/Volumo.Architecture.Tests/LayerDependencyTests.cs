using System.Reflection;
using NetArchTest.Rules;

namespace Volumo.Architecture.Tests;

/// <summary>Garante as referências entre camadas definidas na seção 8 do CLAUDE.md.</summary>
public sealed class LayerDependencyTests
{
    private static readonly Assembly Domain = typeof(Domain.AssemblyReference).Assembly;
    private static readonly Assembly Shared = typeof(Shared.AssemblyReference).Assembly;
    private static readonly Assembly Infrastructure = typeof(Infrastructure.AssemblyReference).Assembly;

    [Fact]
    public void Domain_nao_depende_de_nenhuma_outra_camada_nem_de_frameworks()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Volumo.Api",
                "Volumo.Infrastructure",
                "Volumo.Shared",
                "Volumo.Client",
                "Microsoft.AspNetCore",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Failures(result));
    }

    [Fact]
    public void Shared_nao_depende_do_servidor()
    {
        // Shared é carregado no navegador (Blazor WASM): nada de servidor pode vazar para lá.
        var result = Types.InAssembly(Shared)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Volumo.Api",
                "Volumo.Infrastructure",
                "Volumo.Domain",
                "Microsoft.AspNetCore",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Failures(result));
    }

    [Fact]
    public void Infrastructure_nao_depende_da_api()
    {
        var result = Types.InAssembly(Infrastructure)
            .ShouldNot()
            .HaveDependencyOn("Volumo.Api")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Failures(result));
    }

    private static string Failures(NetArchTest.Rules.TestResult result) =>
        "Tipos que violam a regra: " + string.Join(", ", result.FailingTypeNames ?? []);
}
