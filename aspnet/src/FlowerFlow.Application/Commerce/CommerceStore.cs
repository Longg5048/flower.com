using System.Text.Json;
using System.Text.Json.Nodes;
using FlowerFlow.Domain.Models.Compatibility;

namespace FlowerFlow.Application.Commerce;

public sealed record CommerceDocument(string Path, string Id, Dictionary<string, object> Data);
public sealed record CommerceFilter(string Field, string Operator, object? Value);
public sealed record CommerceQuery(string Collection, IReadOnlyList<CommerceFilter>? Filters = null, string? OrderBy = null, bool Descending = false, int? Limit = null);
public sealed record ServerTimeValue;
public sealed record DeleteFieldValue;
public static class StoreValue
{
    public static ServerTimeValue ServerTimestamp { get; } = new();
    public static DeleteFieldValue Delete { get; } = new();
}
public interface ICommerceTransaction
{
    Task<CommerceDocument?> ReadAsync(string path, CancellationToken cancellationToken);
    Task<IReadOnlyList<CommerceDocument?>> ReadManyAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken);
    void Set(string path, Dictionary<string, object> data, bool merge = false);
    void Update(string path, Dictionary<string, object> data);
    void Create(string path, Dictionary<string, object> data);
    void Delete(string path);
}
public interface ICommerceStore
{
    void EnsureConfigured();
    string NewId(string collection);
    Task<CommerceDocument?> ReadAsync(string path, CancellationToken cancellationToken);
    Task<IReadOnlyList<CommerceDocument?>> ReadManyAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken);
    Task<IReadOnlyList<CommerceDocument>> QueryAsync(CommerceQuery query, CancellationToken cancellationToken);
    Task CreateAsync(string path, Dictionary<string, object> data, CancellationToken cancellationToken);
    Task SetAsync(string path, Dictionary<string, object> data, bool merge, CancellationToken cancellationToken);
    Task<T> RunAsync<T>(Func<ICommerceTransaction, Task<T>> operation, CancellationToken cancellationToken);
}
public sealed record OrderRequestContext(string? Authorization, string? ClientIp);
public sealed record OrderSecurityOptions(string? Secret, string? CronSecret)
{
    public override string ToString() => "OrderSecurityOptions { credentials = redacted }";
}
public sealed record CommerceReply(int Status, Dictionary<string, object> Body, int? RetryAfter = null, string? Allow = null)
{
    public JsonNode? Json => JsonNode.Parse(JsJson.Stringify(Body));
    public static CommerceReply Error(int status, string code, string message, int? retryAfter = null, string? allow = null) =>
        new(status, new() { ["code"] = code, ["error"] = message }, retryAfter, allow);
}
public static class CommerceValues
{
    public static object Or(Dictionary<string, object> data, string key, object fallback) => data.TryGetValue(key, out var value) && Js.Truthy(Js.Element(value)) ? value : fallback;
    public static JsonElement Element(object? value) => Js.Element(value);
    public static string Text(Dictionary<string, object> data, string key, string fallback = "") => data.TryGetValue(key, out var value) && Js.Truthy(Js.Element(value)) ? Js.String(Js.Element(value)) : fallback;
    public static double Number(Dictionary<string, object> data, string key) => Js.Number(data.TryGetValue(key, out var value) ? Js.Element(value) : default);
    public static double NumberOrZero(Dictionary<string, object> data, string key) { var number = Number(data, key); return double.IsNaN(number) || number == 0 ? 0 : number; }
    public static string Timestamp(object? value) => value is DocumentTimestamp timestamp ? timestamp.Iso : value as string ?? "";
    public static double Milliseconds(object? value) => value is DocumentTimestamp timestamp ? timestamp.Milliseconds : 0;
    public static List<object> Array(Dictionary<string, object> data, string key) => data.GetValueOrDefault(key) is List<object> values ? values : [];
    public static Dictionary<string, object> Object(object? value) => value as Dictionary<string, object> ?? new();
}
