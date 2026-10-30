using Inventory.Core.Application.CachedContrast;
using Inventory.Core.Application.RepositoryContrast;
using Inventory.Core.Domain.Entities;
using Inventory.Core.Domain.Enums;
using Inventory.Infrastructure.Redis.Data;
using StackExchange.Redis;

namespace Inventory.Infrastructure.Redis.Cached;

public class StocksCached : IStocksCached
{
    private readonly IStockRepository _stockRepository;
    private readonly IDatabase _db;

    public StocksCached(Client client, IStockRepository stockRepository)
    {
        _db = client.GetDatabase();
        _stockRepository = stockRepository;
    }

    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan HashTtl = CacheTtl + TimeSpan.FromMinutes(1);

    private const string StockItemsSetKey = "StockItems:Ids";

    private readonly string _productKey = "product#";
    private readonly string _orderKey = "order#";
    private readonly string _itemsKey = "items#";
    private readonly string _reservationKey = "reservation#";
    private readonly string _stockItemKey = "stockItem#";

    private string MakeTheProductKey(string value) => _productKey + value;
    private string MakeTheOrderKey(string value) => _orderKey + value;
    private string MakeTheOrderItemsKey(string value) => _orderKey + value + ":items";
    private string MakeTheOrderReservationsKey(string orderId) => _orderKey + orderId + ":reservations";
    private string MakeTheItemsKey(string itemId) => _itemsKey + itemId;
    private string MakeTheReservationKey(string key) => _reservationKey + key;
    private string MakeThestockItemKey(string stockItem) => _stockItemKey + stockItem;

    public async Task<bool> addNewNumOfStocksAsync(string productId, long Quantity)
    {
        string key = MakeTheProductKey(productId);
        if (await _db.KeyExistsAsync(key))
            return await SetTheProductAsync(key, Quantity);

        return await _db.StringIncrementAsync(key, Quantity) > 0;
    }

    private async Task<bool> SetTheProductAsync(string key, long quantity)
    {
        return await _db.StringSetAsync(key, quantity);
    }

    public async Task<bool> removeNumOfStocksAsync(string productId, long Quantity)
    {
        string DecreaseScript = @"
        local r = tonumber(redis.call('DECRBY', KEYS[1], ARGV[1])) or -1
        if r <= 0 then
            redis.call('DEL', KEYS[1])
        end
        return r";
        string key = MakeTheProductKey(productId);
        if (await _db.KeyExistsAsync(key))
        {
            var result = await _db.ScriptEvaluateAsync(
                DecreaseScript,
                new RedisKey[] { key },
                new RedisValue[] { Quantity });

            return !result.IsNull;
        }
        return false;
    }

    public async Task<bool> RefreshTheValue(string productId, long Quantity)
    {
        string key = MakeTheProductKey(productId);
        return await _db.StringSetAsync(key, Quantity);
    }

    public async Task<Core.Domain.Entities.Order?> GetOrderAsync(Guid orderId)
    {
        var key = MakeTheOrderKey(orderId.ToString());
        var entries = await _db.HashGetAllAsync(key);
        if (entries.Length == 0) return null;

        var map = entries.ToDictionary(h => h.Name.ToString(), h => h.Value);

        return new Core.Domain.Entities.Order
        {
            OrderId = orderId,
            ReceivedAt = DateTimeOffset.Parse(map[nameof(Core.Domain.Entities.Order.ReceivedAt)].ToString()),
            UpdatedAt = DateTimeOffset.Parse(map[nameof(Core.Domain.Entities.Order.UpdatedAt)].ToString()),
            Status = Enum.Parse<OrderStatus>(map[nameof(Core.Domain.Entities.Order.Status)].ToString())
        };
    }

    public async Task<IEnumerable<OrderItem>> GetOrderItemsAsync(Guid orderId)
    {
        string key = MakeTheOrderItemsKey(orderId.ToString());
        string pattern = MakeTheItemsKey("*");

        var items = (RedisValue[]?)await _db.ExecuteAsync("SORT", key, "BY", "nosort",
            "GET", $"{pattern}->ProductId",
            "GET", $"{pattern}->Quantity");

        if (items is null || items.Length == 0)
            return await LoadOrderItemsAsync(orderId);

        var orderItems = new List<OrderItem>(items.Length / 2);

        for (int i = 0; i + 2 <= items.Length; i += 2)
        {
            if (items[i].IsNullOrEmpty || items[i + 1].IsNull)
                return await LoadOrderItemsAsync(orderId);

            orderItems.Add(new OrderItem
            {
                OrderId = orderId,
                ProductId = items[i].ToString(),
                Quantity = (int)items[i + 1]
            });
        }

        return orderItems;
    }

    private async Task<IEnumerable<OrderItem>> LoadOrderItemsAsync(Guid orderId)
    {
        string key = MakeTheOrderItemsKey(orderId.ToString());

        var orderItems = (await _stockRepository.GetOrderItemsAsync(orderId)).ToList();

        var tran = _db.CreateTransaction();
        var tasks = new List<Task>
        {
            tran.KeyDeleteAsync(key)
        };

        if (orderItems.Count > 0)
        {
            var members = new List<RedisValue>(orderItems.Count);

            foreach (var item in orderItems)
            {
                string member = $"{item.OrderId}__{item.ProductId}";
                members.Add(member);

                string itemKey = MakeTheItemsKey(member);
                tasks.Add(tran.HashSetAsync(itemKey, new HashEntry[]
                {
                    new("ProductId", item.ProductId),
                    new("Quantity", item.Quantity)
                }));
                tasks.Add(tran.KeyExpireAsync(itemKey, HashTtl));
            }

            tasks.Add(tran.SetAddAsync(key, members.ToArray()));
            tasks.Add(tran.KeyExpireAsync(key, CacheTtl));
        }

        await tran.ExecuteAsync();
        await Task.WhenAll(tasks);

        return orderItems;
    }

    public async Task<IEnumerable<StockReservation>> GetOrderReservationsAsync(Guid orderId)
    {
        string orderKey = MakeTheOrderReservationsKey(orderId.ToString());
        string pattern = MakeTheReservationKey("*");

        var values = (RedisValue[]?)await _db.ExecuteAsync("SORT", orderKey, "BY", "nosort",
            "GET", "#",
            "GET", $"{pattern}->OrderId",
            "GET", $"{pattern}->ProductId",
            "GET", $"{pattern}->Quantity",
            "GET", $"{pattern}->Status",
            "GET", $"{pattern}->ReservedAt",
            "GET", $"{pattern}->UpdatedAt",
            "GET", $"{pattern}->ReleasedAt",
            "GET", $"{pattern}->FulfilledAt");

        if (values is null || values.Length == 0)
            return await LoadOrderReservationsAsync(orderId);

        const int fieldsCount = 9;
        var reservations = new List<StockReservation>(values.Length / fieldsCount);

        for (int i = 0; i + fieldsCount <= values.Length; i += fieldsCount)
        {
            if (values[i + 1].IsNullOrEmpty)
                return await LoadOrderReservationsAsync(orderId);

            reservations.Add(new StockReservation
            {
                ReservationId = Guid.Parse(values[i].ToString()),
                OrderId = Guid.Parse(values[i + 1].ToString()),
                ProductId = values[i + 2].ToString(),
                Quantity = (int)values[i + 3],
                Status = Enum.Parse<ReservationStatus>(values[i + 4].ToString()),
                ReservedAt = DateTimeOffset.Parse(values[i + 5].ToString()),
                UpdatedAt = DateTimeOffset.Parse(values[i + 6].ToString()),
                ReleasedAt = values[i + 7].IsNullOrEmpty ? null : DateTimeOffset.Parse(values[i + 7].ToString()),
                FulfilledAt = values[i + 8].IsNullOrEmpty ? null : DateTimeOffset.Parse(values[i + 8].ToString()),
            });
        }

        return reservations;
    }

    private async Task<IEnumerable<StockReservation>> LoadOrderReservationsAsync(Guid orderId)
    {
        var reservations = (await _stockRepository.GetOrderReservationsAsync(orderId)).ToList();
        if (reservations.Count == 0) return reservations;

        string orderKey = MakeTheOrderReservationsKey(orderId.ToString());

        var tran = _db.CreateTransaction();
        var tasks = new List<Task>
        {
            tran.KeyDeleteAsync(orderKey)
        };

        foreach (var r in reservations)
        {
            string resKey = MakeTheReservationKey(r.ReservationId.ToString());

            tasks.Add(tran.HashSetAsync(resKey, ToHashEntries(r)));
            tasks.Add(tran.KeyExpireAsync(resKey, HashTtl));
        }

        tasks.Add(tran.SetAddAsync(orderKey,
            reservations.Select(r => (RedisValue)r.ReservationId.ToString()).ToArray()));
        tasks.Add(tran.KeyExpireAsync(orderKey, CacheTtl));

        await tran.ExecuteAsync();
        await Task.WhenAll(tasks);

        return reservations;
    }

    private static HashEntry[] ToHashEntries(StockReservation r)
    {
        var entries = new List<HashEntry>
        {
            new(nameof(StockReservation.OrderId), r.OrderId.ToString()),
            new(nameof(StockReservation.ProductId), r.ProductId),
            new(nameof(StockReservation.Quantity), r.Quantity),
            new(nameof(StockReservation.Status), r.Status.ToString()),
            new(nameof(StockReservation.ReservedAt), r.ReservedAt.ToString("O")),
            new(nameof(StockReservation.UpdatedAt), r.UpdatedAt.ToString("O")),
        };

        if (r.ReleasedAt.HasValue)
            entries.Add(new(nameof(StockReservation.ReleasedAt), r.ReleasedAt.Value.ToString("O")));
        if (r.FulfilledAt.HasValue)
            entries.Add(new(nameof(StockReservation.FulfilledAt), r.FulfilledAt.Value.ToString("O")));

        return entries.ToArray();
    }

    public async Task<StockReservation?> GetReservationAsync(Guid reservationId)
    {
        string key = MakeTheReservationKey(reservationId.ToString());

        var hash = await _db.HashGetAllAsync(key);
        if (hash.Length > 0)
        {
            var map = hash.ToDictionary(h => h.Name.ToString(), h => h.Value);

            return new StockReservation
            {
                ReservationId = reservationId,
                OrderId = Guid.Parse(map[nameof(StockReservation.OrderId)].ToString()),
                ProductId = map[nameof(StockReservation.ProductId)].ToString(),
                Quantity = (int)map[nameof(StockReservation.Quantity)],
                Status = Enum.Parse<ReservationStatus>(map[nameof(StockReservation.Status)].ToString()),
                ReservedAt = DateTimeOffset.Parse(map[nameof(StockReservation.ReservedAt)].ToString()),
                UpdatedAt = DateTimeOffset.Parse(map[nameof(StockReservation.UpdatedAt)].ToString()),
                ReleasedAt = map.TryGetValue(nameof(StockReservation.ReleasedAt), out var r)
                    ? DateTimeOffset.Parse(r.ToString()) : null,
                FulfilledAt = map.TryGetValue(nameof(StockReservation.FulfilledAt), out var f)
                    ? DateTimeOffset.Parse(f.ToString()) : null,
            };
        }

        var reservation = await _stockRepository.GetReservationAsync(reservationId);
        if (reservation is null) return null;

        await LoadReservationAsync(reservation);
        return reservation;
    }

    private async Task LoadReservationAsync(StockReservation reservation)
    {
        string key = MakeTheReservationKey(reservation.ReservationId.ToString());

        await _db.HashSetAsync(key, ToHashEntries(reservation));
        await _db.KeyExpireAsync(key, HashTtl);
    }

    public async Task<IEnumerable<StockItem>> GetStockItemsAsync()
    {
        var ids = await _db.SetMembersAsync(StockItemsSetKey);
        if (ids.Length == 0)
            return await LoadStockItems();

        var batch = _db.CreateBatch();
        var tasks = ids.Select(id => batch.HashGetAllAsync(MakeThestockItemKey(id.ToString()))).ToList();
        batch.Execute();
        var results = await Task.WhenAll(tasks);

        var stockItems = new List<StockItem>(ids.Length);

        for (int i = 0; i < ids.Length; i++)
        {
            if (results[i].Length == 0)
                return await LoadStockItems();

            var map = results[i].ToDictionary(h => h.Name.ToString(), h => h.Value);

            stockItems.Add(new StockItem
            {
                ProductId = ids[i].ToString(),
                QuantityOnHand = (int)map["QuantityOnHand"],
                ReservedQuantity = (int)map["ReservedQuantity"],
                Version = (int)map["Version"],
                UpdatedAt = DateTimeOffset.Parse(map["UpdatedAt"].ToString())
            });
        }

        return stockItems;
    }

    private async Task<IEnumerable<StockItem>> LoadStockItems()
    {
        var stockItems = (await _stockRepository.GetAllStockItemsAsync()).ToList();

        var tran = _db.CreateTransaction();
        var tasks = new List<Task>
        {
            tran.KeyDeleteAsync(StockItemsSetKey)
        };

        if (stockItems.Count > 0)
        {
            foreach (var stockItem in stockItems)
            {
                string key = MakeThestockItemKey(stockItem.ProductId);
                tasks.Add(tran.HashSetAsync(key, ToHashEntriesStockItems(stockItem)));
                tasks.Add(tran.KeyExpireAsync(key, HashTtl));
            }

            tasks.Add(tran.SetAddAsync(StockItemsSetKey,
                stockItems.Select(s => (RedisValue)s.ProductId).ToArray()));
            tasks.Add(tran.KeyExpireAsync(StockItemsSetKey, CacheTtl));
        }

        await tran.ExecuteAsync();
        await Task.WhenAll(tasks);

        return stockItems;
    }

    private static HashEntry[] ToHashEntriesStockItems(StockItem r) => new HashEntry[]
    {
        new("QuantityOnHand", r.QuantityOnHand),
        new("ReservedQuantity", r.ReservedQuantity),
        new("Version", r.Version),
        new("UpdatedAt", r.UpdatedAt.ToString("O"))
    };

    public async Task<StockItem?> GetStockItemAsync(string productId)
    {
        string key = MakeThestockItemKey(productId);

        var hashEntries = await _db.HashGetAllAsync(key);
        if (hashEntries.Length == 0)
            return await LoadStockItemAsync(productId);

        var map = hashEntries.ToDictionary(h => h.Name.ToString(), h => h.Value);

        return new StockItem
        {
            ProductId = productId,
            QuantityOnHand = (int)map["QuantityOnHand"],
            ReservedQuantity = (int)map["ReservedQuantity"],
            Version = (int)map["Version"],
            UpdatedAt = DateTimeOffset.Parse(map["UpdatedAt"].ToString())
        };
    }

    private async Task<StockItem?> LoadStockItemAsync(string productId)
    {
        var stockItem = (await _stockRepository.GetStockItemsAsync(productId)).FirstOrDefault();
        if (stockItem is null) return null;

        string key = MakeThestockItemKey(productId);

        await _db.HashSetAsync(key, ToHashEntriesStockItems(stockItem));
        await _db.KeyExpireAsync(key, HashTtl);

        return stockItem;
    }
}
