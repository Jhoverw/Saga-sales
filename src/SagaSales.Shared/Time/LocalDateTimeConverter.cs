namespace SagaSales.Shared.Time;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// The services store and send UTC. Every date the panel shows must be the store's local time (Ecuador,
/// GMT−5), so each DateTime is converted once, when the JSON is read, instead of in every page.
/// <list type="bullet">
/// <item>With "Z" or an offset → converted to the browser's time.</item>
/// <item>Without zone and with a time of day → a UTC timestamp from the database → converted.</item>
/// <item>Exactly midnight without zone → a calendar date (a lot's expiry, a tax start): left as is, otherwise
/// it would show the day before.</item>
/// </list>
/// Writing sends local values back as UTC; dates without zone travel unchanged.
/// </summary>
public sealed class LocalDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetDateTime();

        return value.Kind switch
        {
            DateTimeKind.Utc => value.ToLocalTime(),
            DateTimeKind.Local => value,
            _ when value.TimeOfDay == TimeSpan.Zero => value,
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime(),
        };
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value);
}
