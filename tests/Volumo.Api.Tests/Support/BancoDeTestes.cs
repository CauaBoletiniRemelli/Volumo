using Microsoft.EntityFrameworkCore;
using Volumo.Api.Tests.Support;
using Volumo.Infrastructure.Persistencia;

[assembly: AssemblyFixture(typeof(BancoDeTestes))]

namespace Volumo.Api.Tests.Support;

/// <summary>
/// Banco real no LocalDB, recriado no início de cada execução com as migrations do projeto
/// (não o EF InMemory, que esconde diferenças do SQL Server). Fica no LocalDB ao final para
/// poder ser inspecionado.
/// <para>
/// Não há limpeza entre testes: cada teste cria os próprios usuários com Ids novos, e toda
/// consulta da aplicação é filtrada pelo dono, então os testes não enxergam dados uns dos outros
/// e podem rodar em paralelo.
/// </para>
/// </summary>
public sealed class BancoDeTestes : IAsyncLifetime
{
    public const string ConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=Volumo_Testes;Trusted_Connection=True";

    /// <summary>Contexto sem usuário: os filtros de dono não se aplicam (visão do sistema).</summary>
    public static VolumoDbContext NovoContexto() =>
        new(new DbContextOptionsBuilder<VolumoDbContext>().UseSqlServer(ConnectionString).Options);

    /// <summary>Contexto como se a requisição fosse deste usuário: filtros de dono ligados (por padrão, sessão com 2FA).</summary>
    public static VolumoDbContext NovoContexto(Guid usuarioId, bool comDoisFatores = true) =>
        new(new DbContextOptionsBuilder<VolumoDbContext>().UseSqlServer(ConnectionString).Options, new AcessoFixo(usuarioId, comDoisFatores));

    private sealed record AcessoFixo(Guid? UsuarioId, bool ComDoisFatores) : IContextoDeAcesso;

    public async ValueTask InitializeAsync()
    {
        await using var contexto = NovoContexto();
        await contexto.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        await contexto.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
