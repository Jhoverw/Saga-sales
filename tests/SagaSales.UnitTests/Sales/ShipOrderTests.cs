using Moq;
using SagaSales.BuildingBlocks.EventBus;
using SagaSales.Sales.Commands;
using SagaSales.Sales.Data;
using SagaSales.Sales.Domain;
using SagaSales.Sales.Infrastructure;
using SagaSales.Shared.DTOs;
using SagaSales.Shared.Enums;
using SagaSales.Shared.Exceptions;
using Xunit;

namespace SagaSales.UnitTests.Sales;

/// <summary>Spec 007, FR-003: un pedido se envía solo con forma de pago y comprobantes.</summary>
public class ShipOrderTests
{
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IPaymentClient> _payments = new();
    private readonly Guid _orderId = Guid.NewGuid();

    public ShipOrderTests()
    {
        _orders.Setup(r => r.GetAsync(_orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Order { Id = _orderId, Status = OrderStatus.Processing });
        _orders.Setup(r => r.ShipAsync(_orderId, It.IsAny<ShipmentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Order { Id = _orderId, Status = OrderStatus.Shipped });
    }

    [Fact]
    public async Task Shipping_without_saying_how_is_a_validation_error()
    {
        await Assert.ThrowsAsync<ValidationException>(() => Handle(shipment: null));
        _orders.Verify(r => r.ShipAsync(It.IsAny<Guid>(), It.IsAny<ShipmentRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task An_order_without_payments_cannot_ship()
    {
        Proof(payments: 0, withoutProof: 0);

        var exception = await Assert.ThrowsAsync<ConflictException>(() => Handle(Courier()));
        Assert.Contains("forma de pago", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_transfer_without_its_receipt_blocks_shipping()
    {
        Proof(payments: 2, withoutProof: 1);

        var exception = await Assert.ThrowsAsync<ConflictException>(() => Handle(Courier()));
        Assert.Contains("comprobante", exception.Message, StringComparison.Ordinal);
        _orders.Verify(r => r.ShipAsync(It.IsAny<Guid>(), It.IsAny<ShipmentRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task With_every_receipt_the_order_ships()
    {
        Proof(payments: 2, withoutProof: 0);

        var result = await Handle(Courier());

        Assert.Equal("Shipped", result.Value!.Status);
        _orders.Verify(r => r.ShipAsync(_orderId, It.Is<ShipmentRequest>(s => s.TrackingNumber == "SV-123456"), It.IsAny<CancellationToken>()), Times.Once);
        _orders.Verify(r => r.ChangeStatusAsync(It.IsAny<Guid>(), It.IsAny<OrderStatus>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Other_steps_do_not_ask_for_receipts()
    {
        _orders.Setup(r => r.ChangeStatusAsync(_orderId, OrderStatus.Delivered, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Order { Id = _orderId, Status = OrderStatus.Delivered });
        _orders.Setup(r => r.GetAsync(_orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Order { Id = _orderId, Status = OrderStatus.Shipped });

        await CreateHandler().Handle(new UpdateOrderStatusCommand(_orderId, OrderStatus.Delivered), CancellationToken.None);

        _payments.Verify(p => p.GetProofStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ShipmentRequest Courier() => new()
    {
        ShippingMethod = 1,
        CourierCompany = "Servientrega",
        TrackingNumber = "SV-123456",
    };

    private void Proof(int payments, int withoutProof) =>
        _payments.Setup(p => p.GetProofStatusAsync(_orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderProofStatusDto { Payments = payments, WithoutProof = withoutProof });

    private Task<SagaSales.Shared.Contracts.Result<OrderDto>> Handle(ShipmentRequest? shipment) =>
        CreateHandler().Handle(new UpdateOrderStatusCommand(_orderId, OrderStatus.Shipped, shipment), CancellationToken.None);

    private UpdateOrderStatusCommandHandler CreateHandler() =>
        new(_orders.Object, Mock.Of<IEventBus>(), _payments.Object);
}
