using System.Collections;
using System.Globalization;
using System.Text.Json.Nodes;
using FlowerFlow.Application.Catalog;
using Google.Cloud.Firestore;

namespace FlowerFlow.Infrastructure.Firebase;

public sealed class FirestoreProductReader(Func<FirestoreDb> database) : IProductReader
{
    public void EnsureConfigured() => _ = database();
    public async Task<IReadOnlyList<ProductDocument>> ReadAllAsync(CancellationToken cancellationToken)
    {
        var snapshot = await database().Collection("products").GetSnapshotAsync(cancellationToken);
        return snapshot.Documents.Select(document => new ProductDocument(document.Id,
            new JsonObject(document.ToDictionary().Select(pair => KeyValuePair.Create(pair.Key, ConvertValue(pair.Value, true)))))).ToArray();
    }
    public static JsonNode? ConvertValue(object? value, bool topLevel = false) => value switch
    {
        null => null,
        Timestamp timestamp when topLevel => JsonValue.Create(timestamp.ToDateTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture)),
        Timestamp timestamp => new JsonObject { ["_seconds"] = timestamp.ToProto().Seconds, ["_nanoseconds"] = timestamp.ToProto().Nanos },
        GeoPoint point => new JsonObject { ["_latitude"] = point.Latitude, ["_longitude"] = point.Longitude },
        Blob blob => ConvertValue(blob.ByteString.ToByteArray()),
        byte[] bytes => new JsonObject { ["type"] = "Buffer", ["data"] = new JsonArray(bytes.Select(b => (JsonNode?)JsonValue.Create((int)b)).ToArray()) },
        IDictionary<string, object> fields => new JsonObject(fields.Select(pair => KeyValuePair.Create(pair.Key, ConvertValue(pair.Value)))),
        string text => JsonValue.Create(text),
        double number when !double.IsFinite(number) => null,
        long number => JsonValue.Create((double)number),
        DocumentReference => throw new NotSupportedException("Product document references are not supported."),
        IEnumerable items => new JsonArray(items.Cast<object?>().Select(item => ConvertValue(item)).ToArray()),
        _ => System.Text.Json.JsonSerializer.SerializeToNode(value)
    };
}
