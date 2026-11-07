using Inventory.Core.Application.CachedContrast;
using Inventory.Core.Application.RepositoryContrast;
using Inventory.Infrastructure.Data;
using Inventory.Infrastructure.Redis.Cached;
using Inventory.Infrastructure.Redis.Data;
using Inventory.Infrastructure.Repository;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Inventory.Infrastructure;


public static class DependencyInjection
{
    public static IServiceCollection GetInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<InventoryDBContext>();
        services.AddScoped<IStockRepository,StockRepository>();
        services.AddScoped<IStocksCached, StocksCached>();
        services.AddSingleton<Client>();

        services.AddMassTransit(_=>
        {
            _.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter("inventory", includeNamespace: false));
            _.AddConsumers(typeof(DependencyInjection).Assembly);

            _.AddEntityFrameworkOutbox<InventoryDBContext>(q=>q.UsePostgres());

            _.AddConfigureEndpointsCallback((context, name, cfg) =>
            {
                cfg.UseEntityFrameworkOutbox<InventoryDBContext>(context);
            });

            _.UsingRabbitMq((ctx,cof)=>
            {
                var rmqSeteings=configuration.GetSection("RabbitMq");
                cof.Host(rmqSeteings["Host"], "/", h =>
                {
                    h.Username(rmqSeteings["Username"]!);
                    h.Password(rmqSeteings["Password"]!);
                });

                cof.UseMessageRetry(r=>r.Interval(3, TimeSpan.FromSeconds(1)));
                cof.ConfigureEndpoints(ctx);
            });
        });

        return services;
    }
}
