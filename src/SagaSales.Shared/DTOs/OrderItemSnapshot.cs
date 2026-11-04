namespace SagaSales.Shared.DTOs;

/// <summary>
/// Immutable snapshot of an order line, taken when the order was placed.
/// </summary>
public record OrderItemSnapshot(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice);
