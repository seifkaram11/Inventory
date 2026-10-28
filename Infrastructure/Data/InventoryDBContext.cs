using Inventory.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Inventory.Infrastructure.Data;

public class InventoryDBContext : DbContext
{
    public DbSet<StockItem> stockItems { get; set; }
    public DbSet<StockReservation> stockReservations { get; set; }
    public DbSet<OrderItem> orderItems { get; set; }
    public DbSet<Order> orders { get; set; }
    IConfiguration _configuration;

    public InventoryDBContext(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        string connstr=_configuration.GetConnectionString("Postgres")!;

        optionsBuilder.UseNpgsql(connstr)
            .UseLazyLoadingProxies();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InventoryDBContext).Assembly);
    }
}
