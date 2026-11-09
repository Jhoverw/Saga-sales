using SagaSales.Shared.Purchasing;
using Xunit;

namespace SagaSales.UnitTests.Shared;

/// <summary>Spec 009 FR-004/FR-005: totales de la factura de compra y costo real por unidad de stock.</summary>
public class PurchaseInvoiceCalculatorTests
{
    [Fact]
    public void A_box_enters_as_units_and_the_cost_is_per_unit_with_vat()
    {
        // 2 cajas x 12 a 6.00 sin IVA, IVA 15 %.
        var totals = PurchaseInvoiceCalculator.Compute([new InvoiceLineInput(2, 12, 6.00m, true)], 0.15m);

        Assert.Equal(12.00m, totals.SubtotalTaxed);
        Assert.Equal(1.80m, totals.Tax);
        Assert.Equal(13.80m, totals.Total);
        Assert.Equal(24, totals.Lines[0].StockQuantity);
        Assert.Equal(0.5750m, totals.Lines[0].EffectiveUnitCost);
    }

    [Fact]
    public void Discounts_ice_vat_and_others_are_spread_and_add_up_to_the_total()
    {
        var totals = PurchaseInvoiceCalculator.Compute(
            [
                new InvoiceLineInput(10, 1, 1.00m, true),   // A: 10.00 con IVA
                new InvoiceLineInput(5, 1, 2.00m, false),   // B: 10.00 tarifa 0
                new InvoiceLineInput(3, 1, 10.00m, true),   // C: 30.00 con IVA
            ],
            vatRate: 0.15m,
            discountZero: 1.00m,
            discountTaxed: 4.00m,
            ice: 3.60m,
            others: 2.25m);

        Assert.Equal(10.00m, totals.SubtotalZero);
        Assert.Equal(40.00m, totals.SubtotalTaxed);
        Assert.Equal(5.94m, totals.Tax);                  // (36.00 + 3.60) × 15 %
        Assert.Equal(56.79m, totals.Total);

        Assert.Equal(1.00m, totals.Lines[0].Discount);
        Assert.Equal(1.00m, totals.Lines[1].Discount);
        Assert.Equal(3.00m, totals.Lines[2].Discount);
        Assert.Equal(0m, totals.Lines[1].Ice);            // ICE solo en líneas con IVA
        Assert.Equal(0m, totals.Lines[1].Tax);
        Assert.Equal(11.84m, totals.Lines[0].LineTotal);
        Assert.Equal(9.45m, totals.Lines[1].LineTotal);
        Assert.Equal(35.50m, totals.Lines[2].LineTotal);  // se lleva el centavo del redondeo del IVA
        Assert.Equal(totals.Total, totals.Lines.Sum(l => l.LineTotal));
    }

    [Fact]
    public void The_rounding_cent_goes_to_one_line_so_nothing_is_lost()
    {
        var totals = PurchaseInvoiceCalculator.Compute(
            [new InvoiceLineInput(1, 1, 1.00m, false), new InvoiceLineInput(1, 1, 1.00m, false), new InvoiceLineInput(1, 1, 1.00m, false)],
            vatRate: 0.15m,
            others: 1.00m);

        Assert.Equal(1.00m, totals.Lines.Sum(l => l.Others));
        Assert.Equal(4.00m, totals.Total);
        Assert.Equal(totals.Total, totals.Lines.Sum(l => l.LineTotal));
    }

    [Fact]
    public void Fractional_stock_units_are_refused()
        => Assert.Throws<ArgumentException>(
            () => PurchaseInvoiceCalculator.Compute([new InvoiceLineInput(2.5m, 1, 1.00m, true)], 0.15m));

    [Fact]
    public void Half_a_box_of_twelve_is_six_units()
        => Assert.Equal(6, PurchaseInvoiceCalculator.Compute([new InvoiceLineInput(0.5m, 12, 6.00m, true)], 0.15m).Lines[0].StockQuantity);

    [Fact]
    public void A_discount_larger_than_its_subtotal_is_refused()
        => Assert.Throws<ArgumentException>(
            () => PurchaseInvoiceCalculator.Compute([new InvoiceLineInput(1, 1, 1.00m, true)], 0.15m, discountTaxed: 2.00m));

    [Fact]
    public void An_empty_invoice_is_refused()
        => Assert.Throws<ArgumentException>(() => PurchaseInvoiceCalculator.Compute([], 0.15m));
}
