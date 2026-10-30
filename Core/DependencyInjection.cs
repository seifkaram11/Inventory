using Inventory.Core.Application.Services;
using Inventory.Core.Application.ServicesContrast;
using Microsoft.Extensions.DependencyInjection;

namespace Inventory.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        services.AddScoped<IStockServices, StockServices>();

        return services;
    }
}
