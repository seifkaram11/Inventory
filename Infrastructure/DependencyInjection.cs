using Inventory.Core.Application.RepositoryContrast;
using Inventory.Infrastructure.Data;
using Inventory.Infrastructure.Repository;
using Microsoft.Extensions.DependencyInjection;

namespace Inventory.Infrastructure;


public static class DependencyInjection
{
    public static IServiceCollection GetInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<InventoryDBContext>();
        services.AddScoped<IStockRepository,StockRepository>();
        return services;
    }
}
