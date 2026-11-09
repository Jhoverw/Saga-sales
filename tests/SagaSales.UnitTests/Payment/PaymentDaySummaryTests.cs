using SagaSales.Payment.Data;
using SagaSales.Shared.DTOs;
using Xunit;

namespace SagaSales.UnitTests.Payment;

/// <summary>Spec 006, FR-004 y la decisión de arqueo: efectivo esperado = efectivo − caja chica vigente.</summary>
public class PaymentDaySummaryTests
{
    [Fact]
    public void Methods_are_grouped_into_cash_card_and_other()
    {
        var summary = MyDayRepository.BuildSummary(
            [
                new MyDayRepository.MethodRow { Method = 5, Payments = 3, Amount = 30.00m },
                new MyDayRepository.MethodRow { Method = 1, Payments = 1, Amount = 12.50m },
                new MyDayRepository.MethodRow { Method = 4, Payments = 1, Amount = 7.00m },
                new MyDayRepository.MethodRow { Method = 9, Payments = 1, Amount = 3.00m },
            ],
            []);

        Assert.Equal(30.00m, summary.Cash);
        Assert.Equal(12.50m, summary.Card);
        Assert.Equal(10.00m, summary.Other);
        Assert.Equal(52.50m, summary.Total);
        Assert.Equal(6, summary.Payments);
    }

    [Fact]
    public void Petty_cash_is_subtracted_from_cash_unless_voided()
    {
        var summary = MyDayRepository.BuildSummary(
            [new MyDayRepository.MethodRow { Method = 5, Payments = 2, Amount = 40.00m }],
            [
                new PettyCashExpenseDto { Amount = 5.00m },
                new PettyCashExpenseDto { Amount = 2.50m },
                new PettyCashExpenseDto { Amount = 10.00m, IsVoided = true },
            ]);

        Assert.Equal(7.50m, summary.PettyCash);
        Assert.Equal(32.50m, summary.ExpectedCash);
    }
}
