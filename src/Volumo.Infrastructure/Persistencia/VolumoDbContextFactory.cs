using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Volumo.Infrastructure.Persistencia;

/// <summary>
/// Usada só pelo <c>dotnet ef</c> (migrations). Aponta para o LocalDB de desenvolvimento,
/// a menos que a variável de ambiente <c>ConnectionStrings__Volumo</c> esteja definida.
/// </summary>
public sealed class VolumoDbContextFactory : IDesignTimeDbContextFactory<VolumoDbContext>
{
    private const string LocalDb = @"Server=(localdb)\MSSQLLocalDB;Database=Volumo;Trusted_Connection=True";

    public VolumoDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Volumo") is { Length: > 0 } valor
            ? valor
            : LocalDb;

        return new VolumoDbContext(new DbContextOptionsBuilder<VolumoDbContext>().UseSqlServer(connectionString).Options);
    }
}
