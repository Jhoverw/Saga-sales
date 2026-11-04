using SagaSales.Shared.Enums;
using SagaSales.Shared.Exceptions;
using PaymentRecord = SagaSales.Payment.Domain.Payment;
using Xunit;

namespace SagaSales.UnitTests.Domain;

public class PaymentTests
{
    private static PaymentRecord NewPayment(decimal amount = 100m) => new()
    {
        TenantId = Guid.NewGuid(),
        OrderId = Guid.NewGuid(),
        CustomerId = Guid.NewGuid(),
        Method = PaymentMethod.CreditCard,
        Amount = amount,
        Currency = "USD",
    };

    [Fact]
    public void NewPayment_StartsPending_AndFullyRefundable()
    {
        var payment = NewPayment(100m);

        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(0m, payment.RefundedAmount);
        Assert.Equal(100m, payment.RefundableAmount);
    }

    [Fact]
    public void MarkAuthorized_StoresTheReferenceAndTimestamp()
    {
        var payment = NewPayment();

        payment.MarkAuthorized("TX-123", "{\"ok\":true}");

        Assert.Equal(PaymentStatus.Authorized, payment.Status);
        Assert.Equal("TX-123", payment.TransactionReference);
        Assert.NotNull(payment.PaidAt);
    }

    [Fact]
    public void MarkCaptured_FromAuthorized_Succeeds()
    {
        var payment = NewPayment();
        payment.MarkAuthorized("TX-1", null);

        payment.MarkCaptured();

        Assert.Equal(PaymentStatus.Captured, payment.Status);
        Assert.NotNull(payment.CapturedAt);
    }

    [Fact]
    public void MarkCaptured_FromPending_Throws()
    {
        var payment = NewPayment();

        Assert.Throws<ConflictException>(payment.MarkCaptured);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    [Fact]
    public void Refund_OfTheFullAmount_MarksThePaymentRefunded()
    {
        var payment = NewPayment(100m);
        payment.MarkAuthorized("TX-1", null);
        payment.MarkCaptured();

        payment.Refund(100m);

        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal(0m, payment.RefundableAmount);
        Assert.NotNull(payment.RefundedAt);
    }

    [Fact]
    public void Refund_TwoPartialRefunds_EndInRefunded()
    {
        var payment = NewPayment(100m);
        payment.MarkAuthorized("TX-1", null);

        payment.Refund(30m);
        Assert.Equal(PaymentStatus.PartiallyRefunded, payment.Status);

        payment.Refund(70m);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal(100m, payment.RefundedAmount);
    }

    [Fact]
    public void Refund_MoreThanTheAmount_Throws_AndLeavesThePaymentUntouched()
    {
        var payment = NewPayment(50m);
        payment.MarkAuthorized("TX-1", null);

        Assert.Throws<ConflictException>(() => payment.Refund(60m));
        Assert.Equal(0m, payment.RefundedAmount);
        Assert.Equal(PaymentStatus.Authorized, payment.Status);
    }

    [Fact]
    public void Refund_FromPending_Throws()
    {
        var payment = NewPayment();

        Assert.Throws<ConflictException>(() => payment.Refund(10m));
    }

    [Fact]
    public void Refund_ZeroOrNegative_Throws()
    {
        var payment = NewPayment();
        payment.MarkAuthorized("TX-1", null);

        Assert.Throws<ArgumentOutOfRangeException>(() => payment.Refund(0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => payment.Refund(-5m));
    }

    [Fact]
    public void MarkFailed_RecordsTheReason_AndBlocksRefunds()
    {
        var payment = NewPayment();

        payment.MarkFailed("card_declined");

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Contains("card_declined", payment.GatewayResponse);
        Assert.Throws<ConflictException>(() => payment.Refund(1m));
    }
}
