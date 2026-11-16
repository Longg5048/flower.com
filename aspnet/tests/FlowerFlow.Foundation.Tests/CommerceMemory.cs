using System.Text.Json;
using FlowerFlow.Application.Commerce;
using FlowerFlow.Domain.Models.Compatibility;

namespace FlowerFlow.Foundation.Tests;

internal sealed class CommerceMemory(LoginContractTests.Clock clock) : ICommerceStore
{
    public void EnsureConfigured() { }
    public Dictionary<string, Dictionary<string, object>> Records { get; } = new(StringComparer.Ordinal);
    private int ids;
    private readonly SemaphoreSlim gate = new(1, 1);
    public string NewId(string collection) => "auto-" + Interlocked.Increment(ref ids);
    public static object? Decode(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object when value.TryGetProperty("$timestamp", out var timestamp) => DocumentTimestamp.FromMilliseconds(timestamp.ValueKind == JsonValueKind.Number
            ? timestamp.GetDouble() : DateTimeOffset.Parse(timestamp.GetString()!, System.Globalization.CultureInfo.InvariantCulture).ToUnixTimeMilliseconds()),
        JsonValueKind.Object when value.TryGetProperty("$bytes", out var bytes) => Convert.FromBase64String(bytes.GetString()!),
        JsonValueKind.Object => value.EnumerateObject().ToDictionary(pair => pair.Name, pair => Decode(pair.Value)!),
        JsonValueKind.Array => value.EnumerateArray().Select(item => Decode(item)!).ToList(),
        JsonValueKind.Number => value.GetDouble(), JsonValueKind.String => value.GetString(), JsonValueKind.True => true, JsonValueKind.False => false, _ => null
    };
    private object? Copy(object? value, bool persist = false) => value switch
    {
        ServerTimeValue when persist => DocumentTimestamp.FromMilliseconds(clock.Now),
        Dictionary<string, object> fields => fields.ToDictionary(pair => pair.Key, pair => Copy(pair.Value, persist)!),
        byte[] bytes => bytes.ToArray(), IEnumerable<object> items => items.Select(item => Copy(item, persist)!).ToList(),
        JsonElement { ValueKind: JsonValueKind.Undefined } when persist => throw new InvalidOperationException("Undefined Firestore field."),
        JsonElement element => Decode(element), _ => value
    };
    private CommerceDocument? Snapshot(string path) => Records.TryGetValue(path, out var fields)
        ? new(path, path[(path.LastIndexOf('/') + 1)..], (Dictionary<string, object>)Copy(fields)!) : null;
    public Task<CommerceDocument?> ReadAsync(string path, CancellationToken cancellationToken) => Task.FromResult(Snapshot(path));
    public Task<IReadOnlyList<CommerceDocument?>> ReadManyAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CommerceDocument?>>(paths.Select(Snapshot).ToArray());
    private static int Compare(object? first, object? second)
    {
        if (first is DocumentTimestamp a && second is DocumentTimestamp b) return a.Milliseconds.CompareTo(b.Milliseconds);
        if (first is string x && second is string y) return string.CompareOrdinal(x, y);
        if (first is bool left && second is bool right) return left.CompareTo(right);
        if (first is null || second is null) return first is null ? second is null ? 0 : -1 : 1;
        return Convert.ToDouble(first, System.Globalization.CultureInfo.InvariantCulture).CompareTo(Convert.ToDouble(second, System.Globalization.CultureInfo.InvariantCulture));
    }
    public Task<IReadOnlyList<CommerceDocument>> QueryAsync(CommerceQuery query, CancellationToken cancellationToken)
    {
        var records = Records.Keys.Where(path => path.StartsWith(query.Collection + "/", StringComparison.Ordinal)
            && !path[(query.Collection.Length + 1)..].Contains('/')).Select(path => Snapshot(path)!).Where(document => (query.Filters ?? []).All(filter =>
        {
            if (!document.Data.TryGetValue(filter.Field, out var value)) return false;
            var comparison = Compare(value, filter.Value);
            return filter.Operator switch { "==" => comparison == 0, "<=" => comparison <= 0, "<" => comparison < 0,
                ">=" => comparison >= 0, ">" => comparison > 0, _ => throw new InvalidOperationException("Unsupported memory query.") };
        }));
        if (query.OrderBy is { } order)
            records = records.Where(document => document.Data.ContainsKey(order)).OrderBy(document => document.Data[order],
                Comparer<object>.Create((a, b) => Compare(a, b) * (query.Descending ? -1 : 1)));
        return Task.FromResult<IReadOnlyList<CommerceDocument>>(records.Take(query.Limit ?? int.MaxValue).ToArray());
    }
    public Task CreateAsync(string path, Dictionary<string, object> data, CancellationToken cancellationToken)
    {
        if (Records.ContainsKey(path)) throw new InvalidOperationException("Already exists.");
        Records[path] = (Dictionary<string, object>)Copy(data, true)!; return Task.CompletedTask;
    }
    public Task SetAsync(string path, Dictionary<string, object> data, bool merge, CancellationToken cancellationToken)
    {
        var fields = (Dictionary<string, object>)Copy(data, true)!;
        if (merge && Records.TryGetValue(path, out var old)) { var next = new Dictionary<string, object>(old); foreach (var pair in fields) next[pair.Key] = pair.Value; fields = next; }
        Records[path] = fields; return Task.CompletedTask;
    }
    public async Task<T> RunAsync<T>(Func<ICommerceTransaction, Task<T>> operation, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        var before = Records.ToDictionary(pair => pair.Key, pair => (Dictionary<string, object>)Copy(pair.Value)!);
        try
        {
            var transaction = new MemoryTransaction(this);
            var result = await operation(transaction);
            foreach (var action in transaction.Pending) action();
            return result;
        }
        catch { Records.Clear(); foreach (var pair in before) Records[pair.Key] = pair.Value; throw; }
        finally { gate.Release(); }
    }
    private sealed class MemoryTransaction(CommerceMemory store) : ICommerceTransaction
    {
        public List<Action> Pending { get; } = [];
        public Task<CommerceDocument?> ReadAsync(string path, CancellationToken cancellationToken) => store.ReadAsync(path, cancellationToken);
        public Task<IReadOnlyList<CommerceDocument?>> ReadManyAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken) => store.ReadManyAsync(paths, cancellationToken);
        public void Set(string path, Dictionary<string, object> data, bool merge) => Pending.Add(() => store.SetAsync(path, data, merge, CancellationToken.None).GetAwaiter().GetResult());
        public void Create(string path, Dictionary<string, object> data) => Pending.Add(() => store.CreateAsync(path, data, CancellationToken.None).GetAwaiter().GetResult());
        public void Delete(string path) => Pending.Add(() => store.Records.Remove(path));
        public void Update(string path, Dictionary<string, object> data) => Pending.Add(() =>
        {
            if (!store.Records.TryGetValue(path, out var current)) throw new InvalidOperationException("Missing update target.");
            var fields = new Dictionary<string, object>(current);
            foreach (var pair in data)
            {
                if (pair.Value is DeleteFieldValue) fields.Remove(pair.Key);
                else fields[pair.Key] = store.Copy(pair.Value, true)!;
            }
            store.Records[path] = fields;
        });
    }
}
