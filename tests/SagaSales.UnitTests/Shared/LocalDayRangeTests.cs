using SagaSales.Shared.Time;
using Xunit;

namespace SagaSales.UnitTests.Shared;

/// <summary>Spec 006, FR-001: "hoy" es el día local del cajero, no el día UTC.</summary>
public class LocalDayRangeTests
{
    private const int Ecuador = -300;

    [Fact]
    public void At_22h_in_Ecuador_today_is_still_the_local_day()
    {
        // 2026-11-10 03:00 UTC = 2026-11-09 22:00 en Ecuador.
        var range = LocalDayRange.LastDays(1, Ecuador, new DateTime(2026, 11, 10, 3, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateOnly(2026, 11, 9), range.LastDay);
        Assert.Equal(new DateTime(2026, 11, 9, 5, 0, 0, DateTimeKind.Utc), range.FromUtc);
        Assert.Equal(new DateTime(2026, 11, 10, 5, 0, 0, DateTimeKind.Utc), range.ToUtc);
    }

    [Fact]
    public void Fifteen_days_include_today_and_end_at_local_midnight()
    {
        var range = LocalDayRange.LastDays(15, Ecuador, new DateTime(2026, 11, 9, 15, 0, 0, DateTimeKind.Utc));

        Assert.Equal(15, range.EachDay().Count());
        Assert.Equal(new DateOnly(2026, 10, 26), range.FirstDay);
        Assert.Equal(new DateOnly(2026, 11, 9), range.LastDay);
        Assert.Equal(new DateTime(2026, 10, 26, 5, 0, 0, DateTimeKind.Utc), range.FromUtc);

        var today = range.Today();
        Assert.Equal(new DateTime(2026, 11, 9, 5, 0, 0, DateTimeKind.Utc), today.FromUtc);
        Assert.Equal(range.ToUtc, today.ToUtc);
    }

    [Theory]
    [InlineData(0, Ecuador)]
    [InlineData(63, Ecuador)]
    [InlineData(1, 900)]
    public void Out_of_range_values_are_refused(int days, int offset)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LocalDayRange.LastDays(days, offset, DateTime.UtcNow));
    }
}
