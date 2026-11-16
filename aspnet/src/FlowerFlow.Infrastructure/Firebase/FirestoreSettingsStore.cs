using System.Text.Json;
using System.Text.Json.Nodes;
using FlowerFlow.Application.Catalog;
using FlowerFlow.Application.ShopSettings;
using Google.Cloud.Firestore;

namespace FlowerFlow.Infrastructure.Firebase;

public sealed class FirestoreSettingsStore(Func<FirestoreDb> database) : ISettingsStore
{
    public void EnsureConfigured() => _ = database();
    public async Task<JsonObject?> ReadAsync(string document, CancellationToken cancellationToken)
    {
        var snapshot = await database().Collection("metadata").Document(document).GetSnapshotAsync(cancellationToken);
        return Map(snapshot);
    }
    public Task<SettingsResult> RunTransactionAsync(string document, Func<ISettingsTransaction, Task<SettingsResult>> operation, CancellationToken cancellationToken)
    {
        var db = database();
        var reference = db.Collection("metadata").Document(document);
        return FirestoreTransactions.RunAsync(() => db.RunTransactionAsync(async transaction =>
        {
            var adapter = new MetadataTransaction(db, reference, transaction);
            return adapter.Complete(await operation(adapter));
        }, cancellationToken: cancellationToken), cancellationToken);
    }
    private static JsonObject? Map(DocumentSnapshot snapshot) => snapshot.Exists ? new JsonObject(snapshot.ToDictionary()
        .Select(pair => KeyValuePair.Create(pair.Key, FirestoreProductReader.ConvertValue(pair.Value, true)))) : null;
    public static object? ToFirestore(JsonNode? value) => value switch
    {
        null => null, JsonObject fields => fields.ToDictionary(pair => pair.Key, pair => ToFirestore(pair.Value)),
        JsonArray items => items.Select(ToFirestore).ToArray(),
        _ => value.GetValueKind() switch
        {
            JsonValueKind.String => value.GetValue<string>(), JsonValueKind.True => true, JsonValueKind.False => false,
            JsonValueKind.Number => FlowerPriceRules.Number(value) is var number && number == Math.Truncate(number) && Math.Abs(number) < 9_007_199_254_740_992d ? (object)(long)number : FlowerPriceRules.Number(value),
            _ => null
        }
    };
    private sealed class MetadataTransaction(FirestoreDb db, DocumentReference reference, Transaction transaction) : FirestoreTransactions.State, ISettingsTransaction
    {
        public async Task<JsonObject?> ReadAsync(CancellationToken cancellationToken) => Map(await transaction.GetSnapshotAsync(reference, cancellationToken));
        public void Replace(JsonObject fields, SettingsAudit audit)
        {
            HasWrites = true;
            var data = (Dictionary<string, object?>)ToFirestore(fields)!;
            data["updatedAt"] = FieldValue.ServerTimestamp;
            transaction.Set(reference, data);
            transaction.Create(db.Collection("adminAudit").Document(), new Dictionary<string, object?>
            {
                ["actorUid"] = FlowerPriceRules.Cut(audit.Actor.Uid, 128), ["actorEmail"] = FlowerPriceRules.Email(audit.Actor),
                ["action"] = FlowerPriceRules.Cut(audit.Action, 80), ["resourceType"] = FlowerPriceRules.Cut(audit.ResourceType, 80),
                ["resourceId"] = FlowerPriceRules.Cut(audit.ResourceId, 128), ["details"] = ToFirestore(audit.Details), ["createdAt"] = FieldValue.ServerTimestamp
            });
        }
    }
}
