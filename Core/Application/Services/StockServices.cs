using Inventory.Core.Application.CachedContrast;
using Inventory.Core.Application.DTOs;
using Inventory.Core.Application.RepositoryContrast;
using Inventory.Core.Application.ServicesContrast;
using Inventory.Core.Domain.Entities;
using Inventory.Core.Domain.Enums;
using Inventory.Core.Domain.Exceptions;

namespace Inventory.Core.Application.Services;

public sealed class StockService : IStockServices
{
    private readonly IStockRepository _repository;
    private readonly IStocksCached _stocksCached;

    public StockService(IStockRepository repository, IStocksCached stocksCached)
    {
        _repository = repository;
        _stocksCached = stocksCached;
    }

    public async Task<OperationResult> CancelReservationAsync(Guid reservationId, CancellationToken ct = default)
    {
        var stockReservation = await _repository.GetReservationAsync(reservationId, ct);
        if (stockReservation is null) return OperationResult.Failure(OperationFailureReason.NotFound);

        var result = await ExecuteAsync(() => _repository.CancelReservationAsync(stockReservation, ct));
        if (!result.IsSuccess) return result;

        return await RestoreCachedStockAsync(stockReservation, result);
    }

    public Task<OperationResult> FulfillReservationAsync(Guid reservationId, CancellationToken ct = default) =>
        ExecuteAsync(() => _repository.FulfillReservationAsync(reservationId, ct));

    public async Task<OperationResult> ReleaseReservationAsync(Guid reservationId, CancellationToken ct = default)
    {
        var stockReservation = await _repository.GetReservationAsync(reservationId, ct);
        if (stockReservation is null) return OperationResult.Failure(OperationFailureReason.NotFound);

        var result = await ExecuteAsync(() => _repository.ReleaseReservationAsync(stockReservation, ct));
        if (!result.IsSuccess) return result;

        return await RestoreCachedStockAsync(stockReservation, result);
    }

    public async Task<bool> ReserveOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        var values = (await _repository.GetOrderReservationsAsync(orderId, ct))
            .Select(r => new { r.ProductId, r.Quantity })
            .ToList();

        var reserved = await _repository.ReserveOrderAsync(orderId, ct);
        if (!reserved) return false;

        foreach (var value in values)
        {
            await _stocksCached.removeNumOfStocksAsync(value.ProductId, value.Quantity);
        }

        return true;
    }

    public async Task<Order?> GetOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await _stocksCached.GetOrderAsync(orderId);
        if (order is not null) return order;
        return await _repository.GetOrderAsync(orderId, ct);
    }

    public Task<IEnumerable<OrderItem>> GetOrderItemsAsync(Guid orderId, CancellationToken ct = default) =>
        _stocksCached.GetOrderItemsAsync(orderId);

    public Task<IEnumerable<StockReservation>> GetOrderReservationsAsync(Guid orderId, CancellationToken ct = default) =>
        _stocksCached.GetOrderReservationsAsync(orderId);

    public Task<StockReservation?> GetReservationAsync(Guid reservationId, CancellationToken ct = default) =>
        _stocksCached.GetReservationAsync(reservationId);

    public Task<IEnumerable<StockItem>> GetStockItemsAsync() =>
        _stocksCached.GetStockItemsAsync();

    private async Task<OperationResult> RestoreCachedStockAsync(StockReservation reservation, OperationResult result)
    {
        var cached = await _stocksCached.addNewNumOfStocksAsync(reservation.ProductId, reservation.Quantity);
        return cached ? result : OperationResult.Failure(OperationFailureReason.NotCached);
    }

    private static async Task<OperationResult> ExecuteAsync(Func<Task> action)
    {
        try
        {
            await action();
            return OperationResult.Success();
        }
        catch (StockReservationNotFoundException ex)
        {
            return OperationResult.Failure(OperationFailureReason.NotFound, ex.Message);
        }
        catch (InvalidReservationStateException ex)
        {
            return OperationResult.Failure(OperationFailureReason.InvalidState, ex.Message);
        }
        catch (ConcurrentModificationException ex)
        {
            return OperationResult.Failure(OperationFailureReason.ConcurrentModification, ex.Message);
        }
    }
}
