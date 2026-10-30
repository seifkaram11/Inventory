using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace Inventory.Infrastructure.Redis.Data;


public class Client
{
    readonly ConnectionMultiplexer? _connection;
    IConfiguration _configuration;

    public Client(IConfiguration configuration)
    {
        _configuration = configuration;
        string connStr = _configuration.GetConnectionString("Redis")!;

        _connection = ConnectionMultiplexer.Connect(connStr);
    }

    public IDatabase GetDatabase(int dbNum = 0) => _connection!.GetDatabase(dbNum);
}
