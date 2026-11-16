using System.Globalization;
using System.Text.Json.Nodes;
using FlowerFlow.Application.Identity;

namespace FlowerFlow.Application.Catalog;

public sealed record FlowerPriceResult(JsonObject? Body, ProductFailure? Failure);
public sealed record FlowerPriceAccess(RequestIdentity? Identity, ProductFailure? Failure);
public sealed record FlowerPriceChange(string FlowerId, double OldPrice, long NewPrice, string LocalDate, RequestIdentity Actor);
public interface IFlowerPriceTransaction
{
    Task<ProductDocument?> ReadProductAsync(CancellationToken cancellationToken);
    void StageChange(FlowerPriceChange change);
}
public interface IFlowerPriceStore
{
    void EnsureConfigured();
    Task<IReadOnlyList<ProductDocument>> ListFlowersAsync(CancellationToken cancellationToken);
    Task<ProductDocument?> ReadProductAsync(string id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductDocument>> ReadHistoryAsync(string id, CancellationToken cancellationToken);
    Task<FlowerPriceResult> RunTransactionAsync(string id, Func<IFlowerPriceTransaction, Task<FlowerPriceResult>> operation, CancellationToken cancellationToken);
}

public sealed class FlowerPricesService(IFirebaseTokenVerifier verifier, RequestIdentityService identities,
    IAdminRoleReader roles, IFlowerPriceStore store, TimeProvider time)
{
    public async Task<FlowerPriceAccess> AuthorizeAsync(string? authorization, CancellationToken cancellationToken)
    {
        try
        {
            if (!verifier.IsConfigured) return new(null, new(503, "admin-api-not-configured", "API quản trị Firebase chưa được cấu hình."));
        }
        catch (Exception) { return new(null, new(503, "admin-api-not-configured", "API quản trị Firebase chưa được cấu hình.")); }
        var result = await identities.VerifyAsync(authorization, RequiredAccess.Customer, cancellationToken);
        if (result.Failure is { } failure) return new(null, new(failure.Status, failure.Code, failure.Message));
        var actor = result.Identity!;
        if (!actor.Claims.TryGetValue("admin", out var admin) || admin is not true
            || !actor.Claims.TryGetValue("role", out var claim) || claim is not "owner")
            return new(null, new(403, "permission-denied", "Chỉ chủ cửa hàng mới được quản lý giá hoa."));
        try
        {
            store.EnsureConfigured();
            var role = await roles.ReadAsync(actor.Uid, cancellationToken);
            if (role?.Role != "owner") return new(null, new(403, "permission-denied", "Tài khoản không còn quyền Owner."));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return new(null, new(503, "price-service-unavailable", "Chưa thể xác minh quyền quản lý giá.")); }
        return new(actor, null);
    }

    public Task<FlowerPriceResult> ListAsync(CancellationToken cancellationToken) => Safely(async () =>
    {
        var flowers = (await store.ListFlowersAsync(cancellationToken)).Select(doc => FlowerPriceRules.Serialize(doc.Id, doc.Fields))
            .OrderBy(flower => flower["name"]!.GetValue<string>(), StringComparer.Create(CultureInfo.GetCultureInfo("vi-VN"), false));
        return new(new JsonObject { ["flowers"] = new JsonArray(flowers.Cast<JsonNode?>().ToArray()) }, null);
    }, cancellationToken);

    public Task<FlowerPriceResult> HistoryAsync(string? id, CancellationToken cancellationToken) => Safely(async () =>
    {
        if (!FlowerPriceRules.ValidId(id)) return Error(400, "invalid-argument", "Mã hoa không hợp lệ.");
        var product = await store.ReadProductAsync(id!, cancellationToken);
        if (!IsFlower(product)) return Error(404, "flower-not-found", "Không tìm thấy loại hoa.");
        var history = (await store.ReadHistoryAsync(id!, cancellationToken)).Select(document =>
        {
            var fields = document.Fields;
            JsonNode? Numeric(string name)
            {
                var number = fields.ContainsKey(name) ? FlowerPriceRules.Number(fields[name]) : double.NaN;
                return double.IsFinite(number) ? JsonValue.Create(number) : null;
            }
            return new JsonObject
            {
                ["id"] = document.Id, ["oldPrice"] = Numeric("oldPrice"), ["newPrice"] = Numeric("newPrice"),
                ["updatedAt"] = FlowerPriceRules.TimestampText(fields["updatedAt"]),
                ["updatedBy"] = FlowerPriceRules.OrText(fields["updatedByEmail"], FlowerPriceRules.OrText(fields["updatedBy"]))
            };
        }).OrderBy(item => item, Comparer<JsonObject>.Create((left, right) =>
        {
            if (!DateTimeOffset.TryParse(left["updatedAt"]!.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var first)
                || !DateTimeOffset.TryParse(right["updatedAt"]!.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var second)) return 0;
            return second.CompareTo(first);
        }));
        return new(new JsonObject { ["history"] = new JsonArray(history.Cast<JsonNode?>().ToArray()) }, null);
    }, cancellationToken);

    public Task<FlowerPriceResult> UpdateAsync(RequestIdentity actor, JsonNode? body, CancellationToken cancellationToken) => Safely(async () =>
    {
        var fields = body as JsonObject;
        var id = fields?["flowerId"] is JsonValue scalar && scalar.TryGetValue<string>(out var text) ? text : null;
        if (!FlowerPriceRules.ValidId(id) || !FlowerPriceRules.ValidPrice(fields?["price"], out var price))
            return Error(400, "invalid-argument", "Mã hoa hoặc giá bán không hợp lệ.");
        var now = DateTimeOffset.FromUnixTimeMilliseconds(time.GetUtcNow().ToUnixTimeMilliseconds());
        var localDate = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return await store.RunTransactionAsync(id!, async transaction =>
        {
            var product = await transaction.ReadProductAsync(cancellationToken);
            if (!IsFlower(product)) return Error(404, "flower-not-found", "Không tìm thấy loại hoa.");
            var oldPrice = FlowerPriceRules.ToVndPrice(product!.Fields["price"]);
            if (oldPrice < FlowerPriceRules.MinPrice || oldPrice > FlowerPriceRules.MaxPrice)
                return Error(400, "invalid-current-price", "Giá hiện tại của loại hoa không hợp lệ.");
            var flower = FlowerPriceRules.Serialize(id!, product.Fields);
            if (oldPrice == price) return new(new JsonObject { ["changed"] = false, ["flower"] = flower }, null);
            transaction.StageChange(new(id!, oldPrice, price, localDate, actor));
            flower["price"] = price;
            flower["priceUpdatedAt"] = now.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
            var email = FlowerPriceRules.Email(actor);
            flower["updatedBy"] = email.Length > 0 ? email : actor.Uid;
            return new(new JsonObject { ["changed"] = true, ["flower"] = flower }, null);
        }, cancellationToken);
    }, cancellationToken);

    private static bool IsFlower(ProductDocument? document) => document?.Fields["saleType"] is JsonValue value
        && value.TryGetValue<string>(out var text) && text == "flower";
    private static FlowerPriceResult Error(int status, string code, string message) => new(null, new(status, code, message));
    public static FlowerPriceResult Unavailable() => Error(503, "price-service-unavailable", "Chưa thể tải hoặc cập nhật giá hoa. Vui lòng thử lại.");
    private static async Task<FlowerPriceResult> Safely(Func<Task<FlowerPriceResult>> operation, CancellationToken cancellationToken)
    {
        try { return await operation(); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return Unavailable(); }
    }
}
