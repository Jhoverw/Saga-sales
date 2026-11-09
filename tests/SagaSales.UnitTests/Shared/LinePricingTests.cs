using SagaSales.Shared.Pricing;
using Xunit;

namespace SagaSales.UnitTests.Shared;

/// <summary>Spec 003, FR-002: prices with VAT included (PVP) and the rule used before.</summary>
public class LinePricingTests
{
    private const decimal Iva = 0.15m;

    [Fact]
    public void Two_units_of_a_pvp_of_0_40_cost_0_80()
    {
        var line = LinePricing.Compute(0.40m, 2, isTaxable: true, priceIncludesTax: true, Iva);

        Assert.Equal(new LineAmounts(0.70m, 0.10m, 0.80m), line);
    }

    [Fact]
    public void A_product_without_vat_has_no_vat_in_its_pvp()
    {
        var line = LinePricing.Compute(1.00m, 1, isTaxable: false, priceIncludesTax: true, Iva);

        Assert.Equal(new LineAmounts(1.00m, 0m, 1.00m), line);
    }

    [Fact]
    public void The_previous_rule_still_gives_what_was_charged_before()
    {
        // The case reported on 2026-11-08: 2 × 0.35 without VAT showed 0.40 each but totalled 0.81.
        var line = LinePricing.Compute(0.35m, 2, isTaxable: true, priceIncludesTax: false, Iva);

        Assert.Equal(new LineAmounts(0.70m, 0.11m, 0.81m), line);
    }

    [Fact]
    public void The_previous_rule_respects_products_without_vat()
    {
        var line = LinePricing.Compute(0.35m, 2, isTaxable: false, priceIncludesTax: false, Iva);

        Assert.Equal(new LineAmounts(0.70m, 0m, 0.70m), line);
    }

    [Fact]
    public void The_unit_price_is_rounded_to_cents_first_like_the_server()
    {
        var line = LinePricing.Compute(0.3478m, 2, isTaxable: true, priceIncludesTax: false, Iva);

        Assert.Equal(new LineAmounts(0.70m, 0.11m, 0.81m), line);
    }

    [Theory]
    [InlineData(0.40, 2, 0.80)]
    [InlineData(1.99, 3, 5.97)]
    [InlineData(0.05, 7, 0.35)]
    [InlineData(12.50, 1, 12.50)]
    public void A_pvp_line_always_totals_price_times_quantity(double price, int quantity, double expected)
    {
        var line = LinePricing.Compute((decimal)price, quantity, isTaxable: true, priceIncludesTax: true, Iva);

        Assert.Equal((decimal)expected, line.Total);
        Assert.Equal(line.Total, line.Net + line.Tax);
    }

    [Fact]
    public void The_unit_price_with_vat_is_the_pvp_or_the_previous_price_plus_vat()
    {
        Assert.Equal(0.40m, LinePricing.UnitPriceWithTax(0.40m, isTaxable: true, priceIncludesTax: true, Iva));
        Assert.Equal(0.40m, LinePricing.UnitPriceWithTax(0.35m, isTaxable: true, priceIncludesTax: false, Iva));
    }
}
