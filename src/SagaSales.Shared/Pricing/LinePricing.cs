namespace SagaSales.Shared.Pricing;

/// <summary>Amounts of one order line: base without VAT, VAT and total with VAT.</summary>
public readonly record struct LineAmounts(decimal Net, decimal Tax, decimal Total);

/// <summary>
/// The rule that turns a price and a quantity into line amounts (spec 003, FR-002). The same rule is
/// written in <c>sp_order_create</c> (db/sales/V026), so the till and the server agree to the cent.
/// </summary>
public static class LinePricing
{
    /// <summary>
    /// PVP (price includes VAT): total = price × quantity; the base is taken back out of the total.
    /// Before spec 003 the price was without VAT: base = price × quantity and VAT is added on top.
    /// A product that does not carry VAT has no VAT either way.
    /// </summary>
    public static LineAmounts Compute(
        decimal unitPrice,
        int quantity,
        bool isTaxable,
        bool priceIncludesTax,
        decimal taxRate)
    {
        // The server stores the unit price in cents (decimal(18,2)); round it the same way first.
        var price = Round(unitPrice);

        if (priceIncludesTax)
        {
            var total = Round(price * quantity);
            var net = isTaxable ? Round(total / (1m + taxRate)) : total;
            return new LineAmounts(net, total - net, total);
        }

        var legacyNet = Round(price * quantity);
        var tax = isTaxable ? Round(legacyNet * taxRate) : 0m;
        return new LineAmounts(legacyNet, tax, legacyNet + tax);
    }

    /// <summary>What one unit costs the buyer, VAT included.</summary>
    public static decimal UnitPriceWithTax(decimal unitPrice, bool isTaxable, bool priceIncludesTax, decimal taxRate)
        => Compute(unitPrice, 1, isTaxable, priceIncludesTax, taxRate).Total;

    // SQL Server ROUND on decimals rounds half away from zero; so does this.
    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
