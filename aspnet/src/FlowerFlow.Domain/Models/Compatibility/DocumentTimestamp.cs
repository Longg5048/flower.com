using System.Globalization;
using System.Text.Json.Serialization;

namespace FlowerFlow.Domain.Models.Compatibility;

public sealed record DocumentTimestamp([property: JsonPropertyName("_seconds")] long Seconds,
    [property: JsonPropertyName("_nanoseconds")] int Nanoseconds)
{
    [JsonIgnore] public double Milliseconds => Seconds * 1000d + Nanoseconds / 1_000_000d;
    [JsonIgnore] public string Iso => DateTimeOffset.FromUnixTimeMilliseconds((long)Milliseconds).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
    public static DocumentTimestamp FromMilliseconds(double milliseconds)
    {
        var seconds = (long)Math.Floor(milliseconds / 1000);
        return new(seconds, (int)Math.Floor((milliseconds - seconds * 1000d) * 1_000_000));
    }
}
