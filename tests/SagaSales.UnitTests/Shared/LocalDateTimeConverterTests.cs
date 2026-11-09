using System.Text.Json;
using SagaSales.Shared.Time;
using Xunit;

namespace SagaSales.UnitTests.Shared;

/// <summary>El panel muestra la hora local: los instantes UTC se convierten, las fechas de calendario no.</summary>
public class LocalDateTimeConverterTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new LocalDateTimeConverter() } };

    [Fact]
    public void A_utc_instant_is_converted_to_local_time()
    {
        var value = JsonSerializer.Deserialize<DateTime>("\"2026-11-09T17:00:00Z\"", Options);

        Assert.Equal(DateTimeKind.Local, value.Kind);
        Assert.Equal(new DateTime(2026, 11, 9, 17, 0, 0, DateTimeKind.Utc).ToLocalTime(), value);
    }

    [Fact]
    public void A_database_timestamp_without_zone_is_treated_as_utc()
    {
        var value = JsonSerializer.Deserialize<DateTime>("\"2026-11-09T17:00:00\"", Options);

        Assert.Equal(new DateTime(2026, 11, 9, 17, 0, 0, DateTimeKind.Utc).ToLocalTime(), value);
    }

    [Fact]
    public void A_calendar_date_keeps_its_day()
    {
        var value = JsonSerializer.Deserialize<DateTime>("\"2026-11-20T00:00:00\"", Options);

        Assert.Equal(new DateTime(2026, 11, 20), value);
    }
}
