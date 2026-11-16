using System.Text.Json.Nodes;
using FlowerFlow.Application.Catalog;
using Google.Cloud.Firestore;

namespace FlowerFlow.Infrastructure.Firebase;

public sealed class FirestoreFlowerPriceStore(Func<FirestoreDb> database) : IFlowerPriceStore
{
    public void EnsureConfigured() => _ = database();
    public async Task<IReadOnlyList<ProductDocument>> ListFlowersAsync(CancellationToken cancellationToken) =>
        (await database().Collection("products").WhereEqualTo("saleType", "flower").GetSnapshotAsync(cancellationToken)).Documents.Select(Map).ToArray();
    public async Task<ProductDocument?> ReadProductAsync(string id, CancellationToken cancellationToken)
    {
        var snapshot = await database().Collection("products").Document(id).GetSnapshotAsync(cancellationToken);
        return snapshot.Exists ? Map(snapshot) : null;
    }
    public async Task<IReadOnlyList<ProductDocument>> ReadHistoryAsync(string id, CancellationToken cancellationToken) =>
        (await database().Collection("priceHistory").WhereEqualTo("flowerId", id).GetSnapshotAsync(cancellationToken)).Documents.Select(Map).ToArray();
    public Task<FlowerPriceResult> RunTransactionAsync(string id, Func<IFlowerPriceTransaction, Task<FlowerPriceResult>> operation, CancellationToken cancellationToken)
    {
        var db = database();
        var product = db.Collection("products").Document(id);
        var history = db.Collection("priceHistory").Document();
        return FirestoreTransactions.RunAsync(() => db.RunTransactionAsync(async transaction =>
        {
            var adapter = new PriceTransaction(db, transaction, product, history);
            return adapter.Complete(await operation(adapter));
        }, cancellationToken: cancellationToken), cancellationToken);
    }
    private static ProductDocument Map(DocumentSnapshot snapshot) => new(snapshot.Id, new JsonObject(snapshot.ToDictionary()
        .Select(pair => KeyValuePair.Create(pair.Key, FirestoreProductReader.ConvertValue(pair.Value, true)))));

    private sealed class PriceTransaction(FirestoreDb db, Transaction transaction, DocumentReference product, DocumentReference history) : FirestoreTransactions.State, IFlowerPriceTransaction
    {
        public async Task<ProductDocument?> ReadProductAsync(CancellationToken cancellationToken)
        {
            var snapshot = await transaction.GetSnapshotAsync(product, cancellationToken);
            return snapshot.Exists ? Map(snapshot) : null;
        }
        public void StageChange(FlowerPriceChange change)
        {
            HasWrites = true;
            if (change.FlowerId != product.Id) throw new InvalidOperationException("Transaction product mismatch.");
            var actor = change.Actor;
            var email = FlowerPriceRules.Email(actor);
            transaction.Update(product, new Dictionary<string, object>
            {
                ["price"] = change.NewPrice, ["priceUpdatedAt"] = change.LocalDate,
                ["priceChangedAt"] = FieldValue.ServerTimestamp, ["priceUpdatedBy"] = actor.Uid
            });
            transaction.Create(history, new Dictionary<string, object>
            {
                ["flowerId"] = change.FlowerId, ["oldPrice"] = change.OldPrice, ["newPrice"] = change.NewPrice,
                ["updatedAt"] = FieldValue.ServerTimestamp, ["updatedBy"] = actor.Uid, ["updatedByEmail"] = email
            });
            transaction.Create(db.Collection("adminAudit").Document(), new Dictionary<string, object>
            {
                ["actorUid"] = FlowerPriceRules.Cut(actor.Uid, 128), ["actorEmail"] = email,
                ["action"] = "flower-price.update", ["resourceType"] = "flower", ["resourceId"] = FlowerPriceRules.Cut(change.FlowerId, 128),
                ["details"] = new Dictionary<string, object> { ["oldPrice"] = change.OldPrice, ["newPrice"] = change.NewPrice },
                ["createdAt"] = FieldValue.ServerTimestamp
            });
        }
    }
}
