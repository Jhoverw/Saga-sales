using SagaSales.Shared.Enums;
using SagaSales.Shared.Exceptions;
using SagaSales.Sales.Domain;
using Xunit;

namespace SagaSales.UnitTests.Domain;

public class OrderStatusTransitionsTests
{
    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Confirmed, true)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Processing, true)]
    [InlineData(OrderStatus.Processing, OrderStatus.Shipped, true)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Delivered, true)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Refunded, true)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.Pending, OrderStatus.Delivered, false)]
    [InlineData(OrderStatus.Pending, OrderStatus.Pending, false)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Confirmed, false)]
    [InlineData(OrderStatus.Refunded, OrderStatus.Pending, false)]
    public void IsAllowed_MatchesTheDocumentedStateMachine(
        OrderStatus from,
        OrderStatus to,
        bool expected)
    {
        Assert.Equal(expected, OrderStatusTransitions.IsAllowed(from, to));
    }

    [Fact]
    public void AllowedFrom_TerminalStates_HaveNoTransitions()
    {
        Assert.Empty(OrderStatusTransitions.AllowedFrom(OrderStatus.Cancelled));
        Assert.Empty(OrderStatusTransitions.AllowedFrom(OrderStatus.Refunded));
    }
}

public class OrderTests
{
    private static Order NewOrder() => new()
    {
        TenantId = Guid.NewGuid(),
        CustomerId = Guid.NewGuid(),
        Subtotal = 199.98m,
        ShippingAddress = "Av. Siempre Viva 742",
    };

    [Fact]
    public void NewOrder_StartsPending_WithNoPayment()
    {
        var order = NewOrder();

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(0m, order.PaidAmount);
        Assert.Null(order.PaidDate);
    }

    [Fact]
    public void ChangeStatus_FollowingTheChain_Succeeds()
    {
        var order = NewOrder();

        order.ChangeStatus(OrderStatus.Confirmed);
        order.ChangeStatus(OrderStatus.Processing);

        Assert.Equal(OrderStatus.Processing, order.Status);
    }

    [Fact]
    public void ChangeStatus_SkippingAState_Throws_AndKeepsTheStatus()
    {
        var order = NewOrder();

        Assert.Throws<ConflictException>(() => order.ChangeStatus(OrderStatus.Delivered));
        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Fact]
    public void ChangeStatus_FromTerminalState_Throws()
    {
        var order = NewOrder();
        order.ChangeStatus(OrderStatus.Cancelled);

        Assert.Throws<ConflictException>(() => order.ChangeStatus(OrderStatus.Confirmed));
    }

    [Fact]
    public void ChangeStatus_ToProcessing_StampsPaidDateOnlyOnce()
    {
        var order = NewOrder();

        order.ChangeStatus(OrderStatus.Confirmed);
        order.ChangeStatus(OrderStatus.Processing);
        var firstPaidDate = order.PaidDate;

        Assert.NotNull(firstPaidDate);

        order.ChangeStatus(OrderStatus.Shipped);
        order.ChangeStatus(OrderStatus.Delivered);
        order.ChangeStatus(OrderStatus.Refunded);

        Assert.Equal(firstPaidDate, order.PaidDate);
    }

    [Fact]
    public void RegisterPayment_AddsToTheTotal_AndConfirmsTheOrder()
    {
        var order = NewOrder();

        order.RegisterPayment(199.98m);

        Assert.Equal(199.98m, order.PaidAmount);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
    }

    [Fact]
    public void RegisterPayment_NegativeAmount_Throws()
    {
        var order = NewOrder();

        Assert.Throws<ArgumentOutOfRangeException>(() => order.RegisterPayment(-1m));
    }
}
