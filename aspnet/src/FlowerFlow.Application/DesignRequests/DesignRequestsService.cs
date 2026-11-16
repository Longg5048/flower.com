using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FlowerFlow.Application.Catalog;
using FlowerFlow.Application.Commerce;
using FlowerFlow.Application.Identity;
using FlowerFlow.Application.Orders;
using FlowerFlow.Application.Payments;
using FlowerFlow.Domain.Models.Compatibility;
using static FlowerFlow.Application.DesignRequests.DesignRequestValidationService;

namespace FlowerFlow.Application.DesignRequests;

public sealed class DesignRequestsService(ICommerceStore store, IFirebaseTokenVerifier verifier, RequestIdentityService admins, ICommerceRandom random, TimeProvider time)
{
    private static string Text(JsonElement body, string key) => Js.StringOrEmpty(Js.Get(body, key));
    private static bool ValidId(string id) => Regex.IsMatch(id, @"\A[A-Za-z0-9_-]{1,128}\z");
    private static CommerceReply Reply(Dictionary<string, object> request, int status = 200) => new(status, new() { ["request"] = request });
    private static CommerceReply Unavailable(string message) => CommerceReply.Error(503, "design-request-unavailable", message);
    private static BackendException Missing() => new("design-request-not-found", "Không tìm thấy yêu cầu thiết kế.");
    private async Task<CommerceReply?> AdminAsync(string? authorization, CancellationToken ct)
    {
        try { if (!verifier.IsConfigured) throw new FirebaseConfigurationException(); var result = await admins.VerifyAsync(authorization, RequiredAccess.Admin, ct); return result.Failure is { } error ? CommerceReply.Error(error.Status, error.Code, error.Message) : null; }
        catch (FirebaseConfigurationException) { return CommerceReply.Error(503, "admin-api-not-configured", "API quản trị yêu cầu thiết kế chưa được thiết lập."); }
    }
    public async Task<CommerceReply> GetAsync(string id, string token, bool manage, string? authorization, CancellationToken ct)
    {
        if (manage)
        {
            if (await AdminAsync(authorization, ct) is { } failure) return failure;
            try { return new(200, new() { ["requests"] = (await store.QueryAsync(new("designRequests", OrderBy: "createdAt", Descending: true, Limit: 100), ct)).Select(d => PublicRequest(d.Id, d.Data)).ToList() }); }
            catch (Exception) { return Unavailable("Chưa thể tải danh sách yêu cầu thiết kế."); }
        }
        if (!ValidId(id) || token.Length == 0) return CommerceReply.Error(400, "invalid-design-request", "Mã yêu cầu hoặc mã truy cập không hợp lệ.");
        try
        {
            var doc = await store.ReadAsync("designRequests/" + id, ct);
            return doc is null || !ValidAccessToken(Js.Element(token), Js.Element(doc.Data.GetValueOrDefault("accessTokenHash")))
                ? CommerceReply.Error(404, "design-request-not-found", "Không tìm thấy yêu cầu thiết kế.") : Reply(PublicRequest(id, doc.Data));
        }
        catch (Exception) { return Unavailable("Chưa thể tra cứu yêu cầu thiết kế."); }
    }
    public async Task<CommerceReply> PostAsync(JsonElement body, string? authorization, CancellationToken ct)
    {
        return Text(body, "action") switch
        {
            "create" => await CreateAsync(body, authorization, ct),
            "respond" => await RespondAsync(body, ct),
            "admin-update" => await UpdateAsync(body, authorization, ct),
            _ => CommerceReply.Error(400, "invalid-design-action", "Thao tác yêu cầu thiết kế không hợp lệ.")
        };
    }
    private async Task<CommerceReply> CreateAsync(JsonElement body, string? authorization, CancellationToken ct)
    {
        RequestIdentity? user = null;
        var match = Regex.Match(authorization ?? "", @"\ABearer\s+(.+)\z", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            try { if (!verifier.IsConfigured) throw new FirebaseConfigurationException(); user = await verifier.VerifyAsync(match.Groups[1].Value, ct); }
            catch (FirebaseConfigurationException) { return CommerceReply.Error(503, "admin-api-not-configured", "API xác thực chưa được thiết lập."); }
            catch (InvalidFirebaseTokenException) { return CommerceReply.Error(401, "unauthenticated", "Phiên đăng nhập không hợp lệ hoặc đã hết hạn."); }
        }
        var name = CleanText(Js.Get(body, "customerName"), 100); var phone = CleanText(Js.Get(body, "phone"), 30); var address = CleanText(Js.Get(body, "address"), 500);
        var date = Text(body, "deliveryDate"); var slot = Text(body, "deliveryTime"); var zoneId = Text(body, "deliveryZoneId");
        var design = ParseDesign(Js.Get(body, "design")); var stems = NormalizeStems(Js.Get(body, "stems"));
        var note = CleanText(Js.Get(body, "deliveryNote"), TextLimits["deliveryNote"]); var image = ValidImage(Js.Get(body, "referenceImage"));
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(address) || stems is null || design is null
            || (stems.Count == 0 && !(design["surprise"] is true && Convert.ToDouble(design["budget"]) > 0)) || note is null || image is null
            || !Regex.IsMatch(zoneId, @"\A[A-Za-z0-9_-]{1,80}\z") || OrderValidationService.DeliveryScheduleError(date, slot, time.GetUtcNow()).Length > 0)
            return CommerceReply.Error(400, "invalid-design-request", "Thông tin người nhận, lịch giao hoặc thiết kế không hợp lệ.");
        if (OrderValidationService.NormalizePhone(phone).Length is < 8 or > 15) return CommerceReply.Error(400, "invalid-design-request", "Số điện thoại không hợp lệ.");
        var token = Convert.ToBase64String(Convert.FromHexString(random.Hex(32))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token))); var id = store.NewId("designRequests");
        try
        {
            var result = await store.RunAsync(async tx =>
            {
                var products = await tx.ReadManyAsync(stems.Select(s => "products/" + s["productId"]).ToArray(), ct);
                var zones = await tx.ReadAsync("metadata/deliveryZones", ct); var wrapping = await tx.ReadAsync("metadata/bouquetWrapping", ct);
                var (zone, _) = PricingService.ResolveDeliveryZone(Js.Element(zones?.Data ?? new()), zoneId);
                var lines = stems.Select((stem, index) =>
                {
                    var data = products[index]?.Data ?? new();
                    if (products[index] is null || CommerceValues.Text(data, "status") == "hidden" || PricingService.GetProductSaleType(Js.Element(data)) != "flower")
                        throw new BackendException("product-unavailable", "Một loại hoa không còn khả dụng.");
                    var colors = CommerceValues.Array(data, "colors").Select(c => CanonicalColor(Js.Element(c))).ToList();
                    if (colors.Count > 0 && !colors.Contains(CanonicalColor(Js.Element(stem["color"])))) throw new BackendException("product-unavailable", $"Màu {stem["color"]} của hoa {CommerceValues.Text(data, "title", "đã chọn")} hiện không có.");
                    var price = PricingService.GetProductPrice(Js.Element(data)); var stock = CommerceValues.Number(data, "quantity");
                    if (!Js.SafeInteger(stock) || stock < Convert.ToDouble(stem["quantity"])) throw new BackendException("insufficient-stock", $"Hoa {CommerceValues.Text(data, "title", "đã chọn")} không còn đủ số lượng.");
                    return new Dictionary<string, object>(stem) { ["title"] = Js.Cut(CommerceValues.Text(data, "title", "Hoa"), 120), ["image"] = Js.Cut(CommerceValues.Text(data, "image"), 1000), ["price"] = price, ["priceUpdatedAt"] = CommerceValues.Text(data, "priceUpdatedAt"), ["stock"] = stock };
                }).ToList();
                var tiers = Js.Element(wrapping?.Data.GetValueOrDefault("tiers"));
                if (BouquetWrappingRules.NormalizeTiers(tiers) is null) throw new BackendException("bouquet-wrapping-unavailable", "Cửa hàng chưa cấu hình bậc phí bó hoa.");
                var count = lines.Sum(l => Convert.ToDouble(l["quantity"])); var fee = count > 0 ? BouquetWrappingRules.FeeForDesign(tiers, count, Js.Element(design))!.Value : 0;
                var subtotal = lines.Sum(l => Convert.ToDouble(l["quantity"]) * Convert.ToDouble(l["price"]));
                var main = (string)design["mainProductId"]; if (main.Length == 0 || !lines.Any(l => (string)l["productId"] == main)) main = lines.FirstOrDefault()?.GetValueOrDefault("productId") as string ?? "";
                var request = new Dictionary<string, object>
                {
                    ["uid"] = user?.Uid ?? "", ["customerName"] = name, ["phone"] = phone, ["address"] = address, ["deliveryDate"] = date, ["deliveryTime"] = slot,
                    ["deliveryZoneId"] = zone!["id"], ["deliveryProvince"] = zone["province"], ["deliveryDistrict"] = zone["district"], ["deliveryNote"] = note,
                    ["stems"] = lines, ["design"] = new Dictionary<string, object>(design) { ["mainProductId"] = main }, ["referenceImage"] = image,
                    ["specialRequest"] = design.GetValueOrDefault("specialRequest") ?? "", ["subtotal"] = subtotal, ["wrappingFee"] = fee, ["estimatedTotal"] = subtotal + fee,
                    ["quotedTotal"] = 0, ["quoteNote"] = "", ["status"] = "Chờ tư vấn", ["customerResponse"] = "", ["accessTokenHash"] = hash,
                    ["history"] = new List<object> { AddHistory("Chờ tư vấn", "Khách đã gửi yêu cầu thiết kế.", time) }, ["createdAt"] = StoreValue.ServerTimestamp, ["updatedAt"] = StoreValue.ServerTimestamp
                };
                tx.Create("designRequests/" + id, request);
                return new Dictionary<string, object> { ["id"] = id, ["status"] = request["status"], ["subtotal"] = subtotal, ["wrappingFee"] = fee, ["estimatedTotal"] = subtotal + fee, ["createdAt"] = time.GetUtcNow().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'") };
            }, ct);
            return new(201, new() { ["request"] = result, ["accessToken"] = token });
        }
        catch (BackendException ex) when (new[] { "product-unavailable", "insufficient-stock", "delivery-zone-unavailable", "bouquet-wrapping-unavailable" }.Contains(ex.Code)) { return CommerceReply.Error(409, ex.Code, ex.Message); }
        catch (Exception) { return Unavailable("Chưa thể lưu yêu cầu thiết kế. Vui lòng thử lại."); }
    }
    private async Task<CommerceReply> RespondAsync(JsonElement body, CancellationToken ct)
    {
        var id = Text(body, "id"); var token = Text(body, "accessToken"); var response = Text(body, "response");
        if (!ValidId(id) || token.Length == 0 || response is not ("accept" or "reject")) return CommerceReply.Error(400, "invalid-design-response", "Phản hồi báo giá không hợp lệ.");
        try
        {
            return Reply(await store.RunAsync(async tx =>
            {
                var doc = await tx.ReadAsync("designRequests/" + id, ct);
                if (doc is null || !ValidAccessToken(Js.Element(token), Js.Element(doc.Data.GetValueOrDefault("accessTokenHash")))) throw Missing();
                var data = doc.Data;
                if (CommerceValues.Text(data, "status") != "Chờ khách xác nhận" || !Js.SafeInteger(CommerceValues.Number(data, "quotedTotal"))) throw new BackendException("design-quote-unavailable", "Yêu cầu chưa có báo giá cần xác nhận.");
                var status = response == "accept" ? "Đang thực hiện" : "Đã hủy";
                tx.Update(doc.Path, new() { ["status"] = status, ["customerResponse"] = response, ["history"] = CommerceValues.Array(data, "history").Append(AddHistory(status, response == "accept" ? "Khách đã chấp nhận báo giá." : "Khách đã từ chối báo giá.", time)).ToList(), ["updatedAt"] = StoreValue.ServerTimestamp });
                return PublicRequest(id, new(data) { ["status"] = status, ["customerResponse"] = response });
            }, ct));
        }
        catch (BackendException ex) { return CommerceReply.Error(ex.Code == "design-request-not-found" ? 404 : 409, ex.Code, ex.Message); }
        catch (Exception) { return Unavailable("Chưa thể cập nhật phản hồi. Vui lòng thử lại."); }
    }
    private async Task<CommerceReply> UpdateAsync(JsonElement body, string? authorization, CancellationToken ct)
    {
        if (await AdminAsync(authorization, ct) is { } failure) return failure;
        var id = Text(body, "id"); var status = Text(body, "status"); var raw = Js.Get(body, "quotedTotal");
        double? total = raw.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined || raw.ValueKind == JsonValueKind.String && raw.GetString() == "" ? null : Js.Number(raw);
        var note = CleanText(Js.Get(body, "quoteNote"), TextLimits["quoteNote"]);
        if (!ValidId(id) || !Statuses.Contains(status) || note is null || total is { } number && (!Js.SafeInteger(number) || number is < 0 or > 1_000_000_000) || status == "Chờ khách xác nhận" && total is null)
            return CommerceReply.Error(400, "invalid-design-update", "Trạng thái, báo giá hoặc ghi chú không hợp lệ.");
        try
        {
            return Reply(await store.RunAsync(async tx =>
            {
                var doc = await tx.ReadAsync("designRequests/" + id, ct) ?? throw Missing(); var data = doc.Data;
                var detail = status == "Chờ khách xác nhận" ? $"Cửa hàng gửi báo giá {Js.String(Js.Element(total))} ₫{(note.Length > 0 ? " · " + note : "")}." : note.Length > 0 ? note : $"Cửa hàng cập nhật trạng thái: {status}.";
                var quote = (object?)total ?? data.GetValueOrDefault("quotedTotal") ?? 0;
                tx.Update(doc.Path, new() { ["status"] = status, ["quotedTotal"] = quote, ["quoteNote"] = note, ["history"] = CommerceValues.Array(data, "history").Append(AddHistory(status, detail, time)).ToList(), ["updatedAt"] = StoreValue.ServerTimestamp });
                return PublicRequest(id, new(data) { ["status"] = status, ["quotedTotal"] = quote, ["quoteNote"] = note, ["updatedAt"] = time.GetUtcNow().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'") });
            }, ct));
        }
        catch (BackendException ex) { return CommerceReply.Error(404, ex.Code, ex.Message); }
        catch (Exception) { return Unavailable("Chưa thể cập nhật yêu cầu thiết kế."); }
    }
}
