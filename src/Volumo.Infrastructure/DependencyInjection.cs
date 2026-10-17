using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volumo.Infrastructure.Contas;
using Volumo.Infrastructure.Persistencia;

namespace Volumo.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Volumo";

    /// <summary>
    /// Registra o banco e os serviços da infraestrutura. Sem connection string a API não sobe,
    /// para o erro aparecer no deploy e não na primeira requisição.
    /// </summary>
    public static IServiceCollection AddVolumoInfrastructure(this IServiceCollection services, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{ConnectionStringName} não configurada. Em produção, use a variável de ambiente ConnectionStrings__{ConnectionStringName}.");
        }

        services.AddDbContext<VolumoDbContext>(options => options.UseSqlServer(connectionString));
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ExclusaoDeConta>();
        return services;
    }
}
