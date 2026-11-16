using System.Text.Json;
using FlowerFlow.Application.Commerce;
using FlowerFlow.Domain.Models.Compatibility;
using Google.Cloud.Firestore;

namespace FlowerFlow.Infrastructure.Firebase;

public sealed class FirestoreCommerceStore(Func<FirestoreDb> database) : ICommerceStore
{
    public void EnsureConfigured() => _ = database();
    public string NewId(string collection) => database().Collection(collection).Document().Id;
    public async Task<CommerceDocument?> ReadAsync(string path, CancellationToken cancellationToken) => Map(await database().Document(path).GetSnapshotAsync(cancellationToken));
    public async Task<IReadOnlyList<CommerceDocument?>> ReadManyAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken) =>
        (await database().GetAllSnapshotsAsync(paths.Select(path => database().Document(path)), cancellationToken)).Select(Map).ToArray();
    public async Task<IReadOnlyList<CommerceDocument>> QueryAsync(CommerceQuery definition, CancellationToken cancellationToken)
    {
        Query query = database().Collection(definition.Collection);
        foreach (var filter in definition.Filters ?? [])
        {
            var value = WriteValue(filter.Value);
            query = filter.Operator switch
            {
                "==" => query.WhereEqualTo(filter.Field, value), "<=" => query.WhereLessThanOrEqualTo(filter.Field, value),
                "<" => query.WhereLessThan(filter.Field, value), ">=" => query.WhereGreaterThanOrEqualTo(filter.Field, value),
                ">" => query.WhereGreaterThan(filter.Field, value), "in" => query.WhereIn(filter.Field, (IEnumerable<object>)value!),
                _ => throw new InvalidOperationException("Unsupported query operator.")
            };
        }
        if (definition.OrderBy is not null) query = definition.Descending ? query.OrderByDescending(definition.OrderBy) : query.OrderBy(definition.OrderBy);
        if (definition.Limit is { } limit) query = query.Limit(limit);
        return (await query.GetSnapshotAsync(cancellationToken)).Documents.Select(document => Map(document)!).ToArray();
    }
    public async Task CreateAsync(string path, Dictionary<string, object> data, CancellationToken cancellationToken) =>
        await database().Document(path).CreateAsync(WriteData(data), cancellationToken);
    public async Task SetAsync(string path, Dictionary<string, object> data, bool merge, CancellationToken cancellationToken) =>
        await database().Document(path).SetAsync(WriteData(data), merge ? SetOptions.MergeAll : SetOptions.Overwrite, cancellationToken);
    public Task<T> RunAsync<T>(Func<ICommerceTransaction, Task<T>> operation, CancellationToken cancellationToken)
    {
        var db = database();
        return FirestoreTransactions.RunAsync(() => db.RunTransactionAsync(async transaction =>
        {
            var adapter = new CommerceTransaction(db, transaction);
            return adapter.Complete(await operation(adapter));
        }, cancellationToken: cancellationToken), cancellationToken);
    }
    private static string RelativePath(DocumentReference reference)
    {
        const string separator = "/documents/";
        var index = reference.Path.IndexOf(separator, StringComparison.Ordinal);
        return index < 0 ? reference.Path : reference.Path[(index + separator.Length)..];
    }
    private static CommerceDocument? Map(DocumentSnapshot snapshot) => !snapshot.Exists ? null : new(RelativePath(snapshot.Reference), snapshot.Id,
        snapshot.ToDictionary().ToDictionary(pair => pair.Key, pair => ReadValue(pair.Value)!));
    private static object? ReadValue(object? value) => value switch
    {
        Timestamp timestamp => new DocumentTimestamp(timestamp.ToProto().Seconds, timestamp.ToProto().Nanos),
        Blob blob => blob.ByteString.ToByteArray(), IDictionary<string, object> data => data.ToDictionary(pair => pair.Key, pair => ReadValue(pair.Value)!),
        List<object> array => array.Select(item => ReadValue(item)!).ToList(), _ => value
    };
    private static object? WriteValue(object? value) => value switch
    {
        ServerTimeValue => FieldValue.ServerTimestamp, DeleteFieldValue => FieldValue.Delete,
        DocumentTimestamp timestamp => Timestamp.FromProto(new Google.Protobuf.WellKnownTypes.Timestamp { Seconds = timestamp.Seconds, Nanos = timestamp.Nanoseconds }),
        Dictionary<string, object> data => WriteData(data), byte[] bytes => bytes,
        IEnumerable<object> array => array.Select(WriteValue).ToArray(),
        JsonElement { ValueKind: JsonValueKind.Undefined } => throw new InvalidOperationException("Undefined Firestore field."),
        JsonElement element => WriteValue(Js.Object(element)), _ => value
    };
    private static Dictionary<string, object?> WriteData(Dictionary<string, object> data) => data.ToDictionary(pair => pair.Key, pair => WriteValue(pair.Value));
    private sealed class CommerceTransaction(FirestoreDb db, Transaction transaction) : FirestoreTransactions.State, ICommerceTransaction
    {
        public async Task<CommerceDocument?> ReadAsync(string path, CancellationToken cancellationToken) => Map(await transaction.GetSnapshotAsync(db.Document(path), cancellationToken));
        public async Task<IReadOnlyList<CommerceDocument?>> ReadManyAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken) =>
            (await transaction.GetAllSnapshotsAsync(paths.Select(db.Document), cancellationToken)).Select(Map).ToArray();
        public void Set(string path, Dictionary<string, object> data, bool merge) { HasWrites = true; transaction.Set(db.Document(path), WriteData(data), merge ? SetOptions.MergeAll : SetOptions.Overwrite); }
        public void Update(string path, Dictionary<string, object> data) { HasWrites = true; transaction.Update(db.Document(path), WriteData(data)); }
        public void Create(string path, Dictionary<string, object> data) { HasWrites = true; transaction.Create(db.Document(path), WriteData(data)); }
        public void Delete(string path) { HasWrites = true; transaction.Delete(db.Document(path)); }
    }
}
