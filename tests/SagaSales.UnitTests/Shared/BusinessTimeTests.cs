using SagaSales.Shared.Time;
using Xunit;

namespace SagaSales.UnitTests.Shared;

/// <summary>Lo que el servidor escribe como texto va en hora de Ecuador (GMT−5).</summary>
public class BusinessTimeTests
{
    [Fact]
    public void A_sale_at_02h_utc_belongs_to_the_previous_local_day()
    {
        var local = BusinessTime.ToLocal(new DateTime(2026, 11, 10, 2, 30, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 11, 9, 21, 30, 0), local);
    }

    [Fact]
    public void Database_values_without_zone_are_read_as_utc()
    {
        var local = BusinessTime.ToLocal(new DateTime(2026, 11, 9, 17, 0, 0, DateTimeKind.Unspecified));

        Assert.Equal(12, local.Hour);
    }
}
