using Inventory.Core.Application.DTOs;
using Inventory.Core.Application.ServicesContrast;
using Inventory.Core.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace API.controller;

[ApiController]
[Route("api/V1/Stock")]
public class StockController : ControllerBase
{
    private readonly IStockServices _stockService;

    public StockController(IStockServices stockService)
    {
        _stockService = stockService;
    }

    [HttpGet("items")]
    public async Task<IActionResult> GetStockItems()
    {
        var items = await _stockService.GetStockItemsAsync();
        return Ok(items);
    }

    [HttpGet("orders/{orderId:guid}")]
    public async Task<IActionResult> GetOrder(Guid orderId, CancellationToken ct)
    {
        var order = await _stockService.GetOrderAsync(orderId, ct);
        if (order is null) return NotFound(new { Message = $"Order with ID {orderId} was not found." });
        return Ok(order);
    }

    [HttpGet("orders/{orderId:guid}/items")]
    public async Task<IActionResult> GetOrderItems(Guid orderId, CancellationToken ct)
    {
        var items = await _stockService.GetOrderItemsAsync(orderId, ct);
        return Ok(items);
    }

    [HttpGet("orders/{orderId:guid}/reservations")]
    public async Task<IActionResult> GetOrderReservations(Guid orderId, CancellationToken ct)
    {
        var reservations = await _stockService.GetOrderReservationsAsync(orderId, ct);
        return Ok(reservations);
    }

    [HttpGet("reservations/{reservationId:guid}")]
    public async Task<IActionResult> GetReservation(Guid reservationId, CancellationToken ct)
    {
        var reservation = await _stockService.GetReservationAsync(reservationId, ct);
        if (reservation is null) return NotFound(new { Message = $"Reservation with ID {reservationId} was not found." });
        return Ok(reservation);
    }

    [HttpPost("orders/{orderId:guid}/reserve")]
    public async Task<IActionResult> ReserveOrder(Guid orderId, CancellationToken ct)
    {
        var success = await _stockService.ReserveOrderAsync(orderId, ct);
        if (!success) return BadRequest(new { Message = "Failed to reserve the order. Stock might be insufficient." });

        return Ok(new { Message = "Order reserved successfully." });
    }

    [HttpPatch("reservations/{reservationId:guid}/cancel")]
    public async Task<IActionResult> CancelReservation(Guid reservationId, CancellationToken ct)
    {
        var result = await _stockService.CancelReservationAsync(reservationId, ct);
        return HandleOperationResult(result);
    }

    [HttpPatch("reservations/{reservationId:guid}/fulfill")]
    public async Task<IActionResult> FulfillReservation(Guid reservationId, CancellationToken ct)
    {
        var result = await _stockService.FulfillReservationAsync(reservationId, ct);
        return HandleOperationResult(result);
    }

    [HttpPatch("reservations/{reservationId:guid}/release")]
    public async Task<IActionResult> ReleaseReservation(Guid reservationId, CancellationToken ct)
    {
        var result = await _stockService.ReleaseReservationAsync(reservationId, ct);
        return HandleOperationResult(result);
    }


    private IActionResult HandleOperationResult(OperationResult result)
    {
        if (result.IsSuccess) return NoContent();

        return result.FailureReason switch
        {
            OperationFailureReason.NotFound => NotFound(new { Message = result.ErrorMessage ?? "Resource not found." }),
            OperationFailureReason.InvalidState => BadRequest(new { Message = result.ErrorMessage ?? "Invalid state operation." }),
            OperationFailureReason.ConcurrentModification => Conflict(new { Message = result.ErrorMessage ?? "The resource was modified by another process." }),
            OperationFailureReason.NotCached => StatusCode(500, new { Message = "Failed to synchronize with cache." }),
            _ => BadRequest(new { Message = result.ErrorMessage ?? "An error occurred." })
        };
    }
}
