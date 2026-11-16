using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FlowerFlow.Application.Catalog;
using FlowerFlow.Application.Identity;

namespace FlowerFlow.Application.ShopSettings;

public enum SettingsKind { DeliveryZones, BouquetWrapping }
public sealed record SettingsResult(JsonObject? Body, ProductFailure? Failure);
public sealed record SettingsAccess(RequestIdentity? Identity, ProductFailure? Failure);
public sealed record SettingsAudit(RequestIdentity Actor, string Action, string ResourceType, string ResourceId, JsonObject Details);
public interface ISettingsTransaction
{
    Task<JsonObject?> ReadAsync(CancellationToken cancellationToken);
    void Replace(JsonObject fields, SettingsAudit audit);
}
public interface ISettingsStore
{
    void EnsureConfigured();
    Task<JsonObject?> ReadAsync(string document, CancellationToken cancellationToken);
    Task<SettingsResult> RunTransactionAsync(string document, Func<ISettingsTransaction, Task<SettingsResult>> operation, CancellationToken cancellationToken);
}

public sealed class SettingsService(ISettingsStore store, IFirebaseTokenVerifier verifier, RequestIdentityService identities)
{
    public ProductFailure? Configuration(SettingsKind kind)
    {
        try { store.EnsureConfigured(); return null; }
        catch (Exception) { return ConfigFailure(kind, authentication: false); }
    }
    public async Task<SettingsAccess> AuthorizeAsync(SettingsKind kind, string? authorization, CancellationToken cancellationToken)
    {
        try { if (!verifier.IsConfigured) return new(null, ConfigFailure(kind, authentication: true)); }
        catch (Exception) { return new(null, ConfigFailure(kind, authentication: true)); }
        var result = await identities.VerifyAsync(authorization, RequiredAccess.Admin, cancellationToken);
        return result.Failure is { } failure ? new(null, new(failure.Status, failure.Code, failure.Message)) : new(result.Identity, null);
    }
    public async Task<SettingsResult> ReadAsync(SettingsKind kind, bool manage, CancellationToken cancellationToken)
    {
        try { return Response(kind, await store.ReadAsync(Document(kind), cancellationToken), manage); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return Unavailable(kind, writing: false); }
    }
    public async Task<SettingsResult> UpdateAsync(SettingsKind kind, RequestIdentity actor, JsonNode? body, CancellationToken cancellationToken)
    {
        var fields = body as JsonObject;
        JsonArray? tiers = null;
        JsonObject? zone = null;
        string action = "", id = "";
        if (kind == SettingsKind.BouquetWrapping)
        {
            tiers = SettingsRules.Tiers(fields?["tiers"]);
            if (tiers is null) return Error(400, "invalid-bouquet-wrapping", "Bậc phí phải tăng dần theo số cành, mức cuối phải áp dụng đến 50 cành; phí là số nguyên VND không âm.");
        }
        else
        {
            action = fields?["action"] is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";
            if (action is not ("upsert" or "delete")) return Error(400, "invalid-delivery-zone", "Thao tác khu vực giao hàng không hợp lệ.");
            zone = action == "upsert" ? SettingsRules.Zone(fields?["zone"]) : null;
            id = action == "delete" ? FlowerPriceRules.Trim(FlowerPriceRules.OrText(fields?["id"])) : zone?["id"]?.GetValue<string>() ?? "";
            if (!Regex.IsMatch(id, @"\A[A-Za-z0-9_-]{1,80}\z") || (action == "upsert" && zone is null))
                return Error(400, "invalid-delivery-zone", "Vui lòng nhập tên tỉnh/thành, quận/huyện và phí giao hợp lệ.");
        }
        try
        {
            return await store.RunTransactionAsync(Document(kind), async transaction =>
            {
                var previous = await transaction.ReadAsync(cancellationToken);
                if (kind == SettingsKind.BouquetWrapping)
                {
                    var before = SettingsRules.Tiers(previous?["tiers"]) ?? [];
                    var next = new JsonObject { ["tiers"] = tiers!.DeepClone() };
                    transaction.Replace(next, new(actor, "bouquet-wrapping.update", "bouquet-wrapping", "bouquetWrapping",
                        new JsonObject { ["before"] = before, ["after"] = tiers.DeepClone() }));
                    return Response(kind, next, true);
                }
                var current = SettingsRules.Zones(previous);
                var nextZones = new JsonArray(current.OfType<JsonObject>().Where(item => item["id"]!.GetValue<string>() != id)
                    .Select(item => item.DeepClone()).ToArray());
                if (action == "upsert") nextZones.Add(zone!.DeepClone());
                if (SettingsRules.DuplicateAreas(nextZones)) return Error(409, "duplicate-delivery-zone", "Khu vực tỉnh/thành và quận/huyện này đã được khai báo.");
                var beforeZone = current.OfType<JsonObject>().FirstOrDefault(item => item["id"]!.GetValue<string>() == id);
                var afterZone = nextZones.OfType<JsonObject>().FirstOrDefault(item => item["id"]!.GetValue<string>() == id);
                var updated = new JsonObject { ["zones"] = nextZones };
                transaction.Replace(updated, new(actor, "delivery-zone." + action, "delivery-zone", id,
                    new JsonObject { ["before"] = beforeZone?.DeepClone(), ["after"] = afterZone?.DeepClone() }));
                return Response(kind, updated, true);
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return Unavailable(kind, writing: true); }
    }
    public static string Document(SettingsKind kind) => kind == SettingsKind.DeliveryZones ? "deliveryZones" : "bouquetWrapping";
    private static SettingsResult Response(SettingsKind kind, JsonObject? fields, bool manage)
    {
        if (kind == SettingsKind.BouquetWrapping)
        {
            var tiers = SettingsRules.Tiers(fields?["tiers"]) ?? [];
            return new(new JsonObject { ["configured"] = tiers.Count > 0, ["tiers"] = tiers }, null);
        }
        var zones = SettingsRules.Zones(fields);
        var enabled = zones.OfType<JsonObject>().Any(zone => zone["enabled"]!.GetValue<bool>());
        return new(new JsonObject { ["configured"] = enabled, ["zones"] = manage ? zones : new JsonArray(zones.OfType<JsonObject>()
            .Where(zone => zone["enabled"]!.GetValue<bool>()).Select(zone => zone.DeepClone()).ToArray()) }, null);
    }
    private static ProductFailure ConfigFailure(SettingsKind kind, bool authentication) => new(503, "admin-api-not-configured",
        kind == SettingsKind.DeliveryZones ? "API quản trị dữ liệu giao hàng chưa được cấu hình."
        : authentication ? "API quản trị phí bó hoa chưa được thiết lập." : "API cấu hình phí bó hoa chưa được thiết lập.");
    private static SettingsResult Unavailable(SettingsKind kind, bool writing) => kind == SettingsKind.DeliveryZones
        ? Error(503, "delivery-zone-service-unavailable", writing ? "Chưa thể lưu khu vực giao hàng. Vui lòng thử lại." : "Chưa thể tải khu vực giao hàng.")
        : Error(503, "bouquet-wrapping-unavailable", writing ? "Chưa thể lưu cấu hình phí bó hoa. Vui lòng thử lại." : "Chưa thể tải cấu hình phí bó hoa.");
    private static SettingsResult Error(int status, string code, string message) => new(null, new(status, code, message));
}
