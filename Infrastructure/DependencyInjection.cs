using Inventory.Core.Application.CachedContrast;
using Inventory.Core.Application.RepositoryContrast;
using Inventory.Infrastructure.Data;
using Inventory.Infrastructure.Redis.Cached;
using Inventory.Infrastructure.Redis.Data;
using Inventory.Infrastructure.Repository;
using Microsoft.Extensions.DependencyInjection;

namespace Inventory.Infrastructure;


public static class DependencyInjection
{
    public static IServiceCollection GetInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<InventoryDBContext>();
        services.AddScoped<IStockRepository,StockRepository>();
        services.AddScoped<IStocksCached, StocksCached>();
        services.AddSingleton<Client>();
        return services;
    }
}
