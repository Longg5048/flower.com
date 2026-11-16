using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FlowerFlow.Application.Catalog;
using FlowerFlow.Application.Commerce;
using FlowerFlow.Application.Identity;
using FlowerFlow.Domain.Models.Compatibility;

namespace FlowerFlow.Application.Orders;

public sealed class CheckoutService(ICommerceStore store, ReservationsService reservations, CustomerIdentityService identities, OrderSecurityOptions security, TimeProvider time)
{
    private static string ProductId(Dictionary<string, object> item) => (string)(item.GetValueOrDefault("productId") ?? item["id"]);
    private static double Number(Dictionary<string, object> item, string key) => Convert.ToDouble(item[key], CultureInfo.InvariantCulture);
    public static string RequestHash(Dictionary<string, object> checkout, string note, string uid) => Convert.ToHexStringLower(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsJson.Stringify(new Dictionary<string, object>(checkout) { ["orderNote"] = note, ["uid"] = uid }))));
    private static CommerceReply? QuantityError(JsonElement body)
    {
        var items = Js.Get(body, "items");
        return items.ValueKind == JsonValueKind.Array && items.EnumerateArray().Any(item => Js.Number(Js.Get(item, "quantity")) > OrderConfig.MaxQuantityPerProduct)
            ? CommerceReply.Error(400, "quantity-limit", $"Mỗi lựa chọn bó hoa đặt tối đa {OrderConfig.MaxQuantityPerProduct} bó. Hãy giảm số lượng, hoặc liên hệ cửa hàng nếu cần số lượng lớn.") : null;
    }
    private static Dictionary<string, object> Line(Dictionary<string, object> item, CommerceDocument? snapshot)
    {
        var data = snapshot?.Data ?? new();
        return new(item) { ["data"] = data, ["price"] = PricingService.GetProductPrice(Js.Element(data)),
            ["stock"] = CommerceValues.Number(data, "quantity"), ["path"] = snapshot?.Path ?? "products/" + ProductId(item) };
    }
    private static Dictionary<string, object> PublicLine(Dictionary<string, object> item)
    {
        var data = (Dictionary<string, object>)item["data"];
        var flower = PricingService.GetProductSaleType(Js.Element(data)) == "flower";
        return new()
        {
            ["id"] = item["id"], ["productId"] = ProductId(item), ["title"] = Js.Cut(CommerceValues.Text(data, "title", "Sản phẩm"), 120),
            ["image"] = Js.Cut(CommerceValues.Text(data, "image"), 1000), ["saleType"] = flower ? "flower" : "ready_made", ["unit"] = flower ? "cành" : "bó",
            ["priceUpdatedAt"] = flower ? data.GetValueOrDefault("priceUpdatedAt", default(JsonElement)) : "",
            ["quantity"] = item["quantity"], ["price"] = item["price"], ["stock"] = item["stock"],
            ["customBouquetId"] = item.GetValueOrDefault("customBouquetId") ?? "", ["size"] = item.GetValueOrDefault("size") ?? "",
            ["color"] = item.GetValueOrDefault("color") ?? "", ["cardMessage"] = item.GetValueOrDefault("cardMessage") ?? "", ["personalNote"] = item.GetValueOrDefault("personalNote") ?? ""
        };
    }
    public async Task<CommerceReply> QuoteAsync(JsonElement body, CancellationToken cancellationToken)
    {
        if (QuantityError(body) is { } quantity) return quantity;
        var quote = OrderValidationService.ValidateQuote(body);
        if (quote is null) return CommerceReply.Error(400, "invalid-checkout", "Danh sách sản phẩm không hợp lệ.");
        try
        {
            await reservations.ReleaseQuietlyAsync(cancellationToken);
            var items = (List<Dictionary<string, object>>)quote["items"];
            var products = await store.ReadManyAsync(items.Select(item => "products/" + ProductId(item)).ToArray(), cancellationToken);
            var promotions = (await store.ReadAsync("metadata/activePromotions", cancellationToken))?.Data ?? new();
            var delivery = (await store.ReadAsync("metadata/deliveryZones", cancellationToken))?.Data ?? new();
            var wrapping = (await store.ReadAsync("metadata/bouquetWrapping", cancellationToken))?.Data ?? new();
            var (zone, shippingFee) = PricingService.ResolveDeliveryZone(Js.Element(delivery), (string)quote["deliveryZoneId"]);
            var shortages = new List<Dictionary<string, object>>();
            var cart = items.Select((item, index) =>
            {
                var line = Line(item, products[index]); var data = (Dictionary<string, object>)line["data"];
                var price = Number(line, "price"); var stock = Number(line, "stock");
                if (CommerceValues.Text(data, "status") == "hidden") shortages.Add(new()
                { ["id"] = item["id"], ["title"] = Js.Cut(CommerceValues.Text(data, "title"), 120), ["available"] = 0, ["hidden"] = true });
                else if (products[index] is null || !double.IsFinite(price) || price <= 0 || !Js.Integer(stock) || stock < BouquetOptions.Demand(items, ProductId(item)))
                    shortages.Add(new() { ["id"] = item["id"], ["title"] = Js.Cut(CommerceValues.Text(data, "title"), 120),
                        ["available"] = products[index] is not null && Js.Integer(stock) && double.IsFinite(price) && price > 0 ? Math.Max(0, stock) : 0 });
                return line;
            }).ToList();
            var hidden = shortages.Where(item => item.GetValueOrDefault("hidden") is true).ToList();
            if (hidden.Count > 0) return CommerceReply.Error(409, "product-unavailable", string.Join(", ", hidden.Select(item => $"“{CommerceValues.Text(item, "title", "Sản phẩm")}”"))
                + " hiện đang tạm ẩn và không thể đặt hàng. Hãy chọn sản phẩm khác.");
            if (shortages.Count > 0)
            {
                var message = string.Join("; ", shortages.Select(item => Number(item, "available") > 0
                    ? $"“{CommerceValues.Text(item, "title", "Một sản phẩm")}” chỉ còn {Js.String(Js.Element(item["available"]))} sản phẩm"
                    : $"“{CommerceValues.Text(item, "title", "Một sản phẩm")}” đã hết hàng"));
                return new(409, new() { ["code"] = "insufficient-stock", ["error"] = message + ". Hãy giảm số lượng hoặc xóa sản phẩm đó khỏi giỏ.", ["items"] = shortages });
            }
            var promotion = PricingService.FindPromotion(Js.Element(promotions.GetValueOrDefault("items")), (string)quote["promotionId"], time.GetUtcNow());
            var priced = PricingService.PriceCart(cart, promotion);
            var bouquets = PricingService.CalculateCustomBouquets(cart, Js.Element(wrapping.GetValueOrDefault("tiers")));
            var wrappingFee = bouquets.Sum(item => Number(item, "wrappingFee"));
            var subtotal = Number(priced, "subtotal"); var savings = Number(priced, "savings");
            return new(200, new()
            {
                ["totals"] = new Dictionary<string, object> { ["subtotal"] = subtotal, ["savings"] = savings, ["tax"] = 0,
                    ["wrappingFee"] = wrappingFee, ["shippingFee"] = shippingFee, ["total"] = subtotal - savings + wrappingFee + shippingFee },
                ["customBouquets"] = bouquets, ["deliveryZone"] = zone is null ? null! : new Dictionary<string, object>
                    { ["id"] = zone["id"], ["province"] = zone["province"], ["district"] = zone["district"], ["fee"] = zone["fee"] },
                ["deliveryZonesConfigured"] = PricingService.GetEnabledDeliveryZones(Js.Element(delivery)).Count > 0, ["items"] = cart.Select(PublicLine).ToList()
            });
        }
        catch (BackendException error) { return CommerceReply.Error(409, error.Code, error.Message); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return CommerceReply.Error(503, "order-service-unavailable", "Chưa thể kiểm tra giá đơn hàng. Vui lòng thử lại sau."); }
    }
    private IReadOnlyList<(string Path, int Maximum)> Limits(OrderRequestContext request, RequestIdentity? user, string phone)
    {
        if (string.IsNullOrEmpty(request.ClientIp) || request.ClientIp.Length > 64 || string.IsNullOrEmpty(security.Secret)) throw new InvalidOperationException("Trusted order source unavailable.");
        var values = new List<(string Type, string Value, int Maximum)> { ("ip", request.ClientIp, 5), ("phone", OrderValidationService.NormalizePhone(phone), 3) };
        if (user is not null) values.Add(("account", user.Uid, 5));
        return values.Select(value => ("orderCreationLimits/" + TrackingService.Hmac(security.Secret, value.Type + ":" + value.Value), value.Maximum)).ToArray();
    }
    public async Task<CommerceReply> CreateAsync(JsonElement body, OrderRequestContext request, CancellationToken cancellationToken)
    {
        var note = Js.StringOrEmpty(Js.Get(body, "orderNote")).Trim();
        if (note.Length > OrderConfig.MaxOrderNoteLength) return CommerceReply.Error(400, "note-too-long", $"Ghi chú tối đa {OrderConfig.MaxOrderNoteLength} ký tự. Hãy rút gọn ghi chú rồi đặt lại.");
        if (QuantityError(body) is { } quantity) return quantity;
        var checkout = OrderValidationService.ValidateCheckout(body);
        if (checkout is null) return CommerceReply.Error(400, "invalid-checkout", "Thông tin đặt hàng hoặc danh sách sản phẩm không hợp lệ.");
        try
        {
            var user = await identities.ReadAsync(request.Authorization, cancellationToken);
            await reservations.ReleaseQuietlyAsync(cancellationToken);
            var limits = Limits(request, user, (string)checkout["phone"]);
            var items = (List<Dictionary<string, object>>)checkout["items"];
            var key = (string)checkout["idempotencyKey"]; var requestPath = "orderRequests/" + key;
            var now = DocumentTimestamp.FromMilliseconds(time.GetUtcNow().ToUnixTimeMilliseconds());
            var fingerprint = RequestHash(checkout, note, user?.Uid ?? "");
            var order = await store.RunAsync(async transaction =>
            {
                var previous = await transaction.ReadAsync(requestPath, cancellationToken);
                if (previous is not null)
                {
                    if (CommerceValues.Text(previous.Data, "uid") != (user?.Uid ?? "") || CommerceValues.Text(previous.Data, "requestHash") is { Length: > 0 } hash && hash != fingerprint)
                        throw new BackendException("idempotency-conflict", "Mã yêu cầu đã được dùng cho nội dung đặt hàng khác. Hãy kiểm tra đơn đã gửi.");
                    var existing = await transaction.ReadAsync("orders/" + CommerceValues.Text(previous.Data, "orderId"), cancellationToken);
                    if (existing is null) throw new BackendException("idempotency-conflict", "Yêu cầu đặt hàng này đã được xử lý nhưng đơn không còn tồn tại.");
                    return new Dictionary<string, object>(existing.Data) { ["id"] = existing.Id,
                        ["shippingFee"] = CommerceValues.NumberOrZero(existing.Data, "shippingFee"), ["wrappingFee"] = CommerceValues.NumberOrZero(existing.Data, "wrappingFee") };
                }
                var limitRecords = await transaction.ReadManyAsync(limits.Select(limit => limit.Path).ToArray(), cancellationToken);
                var limitChanges = limits.Select((limit, index) =>
                {
                    var data = limitRecords[index]?.Data ?? new(); var until = CommerceValues.Milliseconds(data.GetValueOrDefault("expiresAt"));
                    var count = until > now.Milliseconds ? CommerceValues.NumberOrZero(data, "count") : 0;
                    if (count >= limit.Maximum) throw new BackendException("order-rate-limited", "Bạn đã gửi nhiều đơn trong thời gian ngắn. Vui lòng thử lại sau.", (int)Math.Max(1, Math.Ceiling((until - now.Milliseconds) / 1000)));
                    return (limit.Path, Data: new Dictionary<string, object> { ["count"] = count + 1,
                        ["expiresAt"] = DocumentTimestamp.FromMilliseconds(until > now.Milliseconds ? until : now.Milliseconds + 900_000) });
                }).ToList();
                if (user is null && items.Any(item => BouquetOptions.Demand(items, ProductId(item)) > 5))
                    throw new BackendException("guest-quantity-limit", "Khách chưa đăng nhập đặt tối đa 5 sản phẩm mỗi loại trong một đơn. Hãy đăng nhập để đặt số lượng lớn hơn.");
                var schedule = OrderValidationService.DeliveryScheduleError((string)checkout["deliveryDate"], (string)checkout["deliveryTime"], time.GetUtcNow());
                if (schedule.Length > 0) throw new BackendException("invalid-delivery-schedule", schedule);
                var snapshots = await transaction.ReadManyAsync(items.Select(item => "products/" + ProductId(item)).ToArray(), cancellationToken);
                var promotions = (await transaction.ReadAsync("metadata/activePromotions", cancellationToken))?.Data ?? new();
                var delivery = (await transaction.ReadAsync("metadata/deliveryZones", cancellationToken))?.Data ?? new();
                var wrapping = (await transaction.ReadAsync("metadata/bouquetWrapping", cancellationToken))?.Data ?? new();
                var (zone, shippingFee) = PricingService.ResolveDeliveryZone(Js.Element(delivery), (string)checkout["deliveryZoneId"]);
                var cart = items.Select((item, index) =>
                {
                    var snapshot = snapshots[index];
                    if (snapshot is null) throw new BackendException("product-unavailable", "Một sản phẩm không còn bán.");
                    var line = Line(item, snapshot); var data = snapshot.Data;
                    if (CommerceValues.Text(data, "status") == "hidden") throw new BackendException("product-unavailable", $"“{Js.Cut(CommerceValues.Text(data, "title", "Sản phẩm"), 120)}” hiện đang tạm ẩn và không thể đặt hàng.");
                    var price = Number(line, "price"); var stock = Number(line, "stock"); var title = CommerceValues.Text(data, "title", "Một sản phẩm");
                    if (!double.IsFinite(price) || price <= 0) throw new BackendException("product-unavailable", $"“{title}” hiện chưa có giá bán. Hãy bỏ sản phẩm này khỏi giỏ.");
                    if (!Js.Integer(stock) || stock < BouquetOptions.Demand(items, ProductId(item)))
                    {
                        var available = Js.Integer(stock) ? Math.Max(0, stock) : 0;
                        throw new BackendException("insufficient-stock", available > 0 ? $"“{title}” vừa có người mua, chỉ còn {Js.String(Js.Element(available))} sản phẩm. Hãy giảm số lượng rồi đặt lại."
                            : $"“{title}” vừa hết hàng. Hãy bỏ sản phẩm này khỏi giỏ.");
                    }
                    return line;
                }).ToList();
                var promotion = PricingService.FindPromotion(Js.Element(promotions.GetValueOrDefault("items")), (string)checkout["promotionId"], time.GetUtcNow());
                var priced = PricingService.PriceCart(cart, promotion); var subtotal = Number(priced, "subtotal"); var savings = Number(priced, "savings");
                var bouquets = PricingService.CalculateCustomBouquets(cart, Js.Element(wrapping.GetValueOrDefault("tiers")));
                var wrappingFee = bouquets.Sum(item => Number(item, "wrappingFee")); var total = subtotal - savings + wrappingFee + shippingFee;
                var expected = Js.Element(checkout["expectedTotals"]);
                if (Js.Number(Js.Get(expected, "subtotal")) != subtotal || Js.Number(Js.Get(expected, "savings")) != savings || Js.Number(Js.Get(expected, "tax")) != 0
                    || Js.Number(Js.Get(expected, "shippingFee")) != shippingFee || (Js.Get(expected, "wrappingFee").ValueKind == JsonValueKind.Undefined ? 0 : Js.Number(Js.Get(expected, "wrappingFee"))) != wrappingFee
                    || Js.Number(Js.Get(expected, "total")) != total) throw new BackendException("price-changed", "Giá hoặc ưu đãi vừa thay đổi. Hãy quay lại trang thanh toán để xem tổng tiền mới.");
                var id = store.NewId("orders"); var payment = (string)checkout["paymentMethod"];
                var orderData = new Dictionary<string, object>
                {
                    ["orderType"] = cart.Any(item => PricingService.GetProductSaleType(Js.Element(item["data"])) == "flower") ? "custom_bouquet" : "ready_made",
                    ["uid"] = user?.Uid ?? "", ["customerName"] = checkout["customerName"], ["phone"] = checkout["phone"], ["address"] = checkout["address"],
                    ["recipientName"] = checkout["recipientName"], ["recipientPhone"] = checkout["recipientPhone"], ["customerIsRecipient"] = checkout["customerIsRecipient"],
                    ["deliveryDate"] = checkout["deliveryDate"], ["deliveryTime"] = checkout["deliveryTime"], ["deliveryZoneId"] = zone!["id"],
                    ["deliveryProvince"] = zone["province"], ["deliveryDistrict"] = zone["district"], ["greetingCard"] = checkout["greetingCard"],
                    ["anonymousGreetingCard"] = checkout["anonymousGreetingCard"], ["giftWrap"] = checkout["giftWrap"], ["createdAt"] = StoreValue.ServerTimestamp,
                    ["status"] = "Chờ xác nhận", ["currentLocation"] = "Đơn hàng đã được gửi đến cửa hàng", ["estimatedDelivery"] = "",
                    ["trackingHistory"] = new List<object> { new Dictionary<string, object> { ["status"] = "Chờ xác nhận", ["location"] = "Đơn hàng đã được gửi đến cửa hàng", ["estimatedDelivery"] = "", ["updatedAt"] = now.Iso } },
                    ["subtotal"] = subtotal, ["savings"] = savings, ["tax"] = 0, ["wrappingFee"] = wrappingFee, ["customBouquets"] = bouquets,
                    ["shippingFee"] = shippingFee, ["total"] = total, ["paymentMethod"] = payment,
                    ["paymentStatus"] = payment == "qr" ? "Chờ xác minh thanh toán" : payment == "vnpay" ? "chua_thanh_toan" : "Thanh toán khi nhận hàng",
                    ["paymentReference"] = PricingService.PaymentReferenceFor(key), ["paidAt"] = "", ["paymentVerified"] = false,
                    ["promotionId"] = promotion is null ? "" : checkout["promotionId"], ["orderNote"] = note, ["items"] = cart.Select(PublicLine).ToList(),
                    ["inventoryReserved"] = true, ["inventoryState"] = "reserved", ["reservationExpiresAt"] = DocumentTimestamp.FromMilliseconds(now.Milliseconds +
                        (payment == "qr" ? OrderConfig.QrReservationMs : payment == "vnpay" ? OrderConfig.VnpayReservationMs : OrderConfig.CodReservationMs))
                };
                foreach (var limit in limitChanges) transaction.Set(limit.Path, limit.Data);
                foreach (var item in cart.GroupBy(ProductId).Select(group => group.First())) transaction.Update((string)item["path"], new()
                    { ["quantity"] = Number(item, "stock") - BouquetOptions.Demand(items, ProductId(item)) });
                transaction.Create("orders/" + id, orderData);
                transaction.Create(requestPath, new() { ["orderId"] = id, ["requestHash"] = fingerprint, ["uid"] = user?.Uid ?? "", ["createdAt"] = StoreValue.ServerTimestamp });
                return new Dictionary<string, object>(orderData) { ["id"] = id, ["createdAt"] = now.Iso };
            }, cancellationToken);
            return new(201, new() { ["order"] = new Dictionary<string, object> { ["id"] = order["id"], ["paymentReference"] = order["paymentReference"] },
                ["totals"] = new Dictionary<string, object> { ["subtotal"] = order["subtotal"], ["savings"] = order["savings"],
                    ["wrappingFee"] = order["wrappingFee"], ["shippingFee"] = order["shippingFee"], ["total"] = order["total"] } });
        }
        catch (InvalidFirebaseTokenException) { return CommerceReply.Error(401, "unauthenticated", "Phiên đăng nhập hết hạn. Vui lòng đăng nhập lại rồi đặt hàng."); }
        catch (BackendException error)
        {
            var status = error.Code == "order-rate-limited" ? 429 : new[] { "guest-quantity-limit", "invalid-checkout", "invalid-delivery-schedule" }.Contains(error.Code) ? 400 : 409;
            return CommerceReply.Error(status, error.Code, error.Message, error.RetryAfter);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return CommerceReply.Error(503, "order-service-unavailable", "Chưa thể xác nhận đơn hàng. Vui lòng thử lại sau."); }
    }
}
