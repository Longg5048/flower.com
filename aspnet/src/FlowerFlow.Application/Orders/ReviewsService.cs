using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FlowerFlow.Application.Catalog;
using FlowerFlow.Application.Commerce;
using FlowerFlow.Application.Identity;
using FlowerFlow.Domain.Models.Compatibility;

namespace FlowerFlow.Application.Orders;

public sealed class ReviewsService(ICommerceStore store, CustomerIdentityService identities, IFirebaseTokenVerifier verifier, TimeProvider time)
{
    public static string ReviewId(string orderId) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("review:" + orderId)))[..40];
    public async Task<CommerceReply> OwnAsync(JsonElement body, string? authorization, CancellationToken cancellationToken)
    {
        var id = Js.StringOrEmpty(Js.Get(body, "orderId")).Trim();
        if (id.Length is < 1 or > 128 || id.Contains('/')) return CommerceReply.Error(400, "invalid-argument", "Mã đơn không hợp lệ.");
        try
        {
            var user = await identities.ReadAsync(authorization, cancellationToken);
            if (user is null) return CommerceReply.Error(401, "unauthenticated", "Đăng nhập để xem đánh giá của bạn.");
            var order = await store.ReadAsync("orders/" + id, cancellationToken);
            if (order is null || CommerceValues.Text(order.Data, "uid") != user.Uid) return CommerceReply.Error(404, "order-not-found", "Không tìm thấy đơn trong tài khoản.");
            var review = await store.ReadAsync("reviews/" + ReviewId(id), cancellationToken);
            return new(200, new() { ["review"] = review is null ? null! : new Dictionary<string, object>(review.Data)
                { ["id"] = review.Id, ["createdAt"] = CommerceValues.Timestamp(review.Data.GetValueOrDefault("createdAt")), ["orderId"] = id } });
        }
        catch (InvalidFirebaseTokenException) { return CommerceReply.Error(401, "unauthenticated", "Phiên đăng nhập hết hạn."); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return CommerceReply.Error(503, "review-unavailable", "Chưa tải được đánh giá. Hãy thử lại."); }
    }
    public async Task<CommerceReply> SubmitAsync(JsonElement body, string? authorization, CancellationToken cancellationToken)
    {
        var bearer = Regex.Match(authorization ?? "", @"^Bearer\s+(.+)$", RegexOptions.IgnoreCase);
        if (!bearer.Success) return CommerceReply.Error(401, "unauthenticated", "Đăng nhập tài khoản đã đặt đơn để gửi đánh giá.");
        var id = Js.StringOrEmpty(Js.Get(body, "orderId")).Trim();
        var rating = Js.Number(Js.Get(body, "rating")); var comment = Js.StringOrEmpty(Js.Get(body, "comment")).Trim();
        var supplied = Js.Get(body, "photos");
        var photos = supplied.ValueKind == JsonValueKind.Undefined ? ImageRules.ValidateReviewPhotos() : ImageRules.ValidateReviewPhotos(JsonNode.Parse(supplied.GetRawText()));
        if (photos is null) return CommerceReply.Error(400, "invalid-photos", "Tối đa 3 ảnh JPEG đã tối ưu, mỗi ảnh không quá 120 KB và 1600px.");
        if (id.Length is < 1 or > 128 || id.Contains('/') || !Js.Integer(rating) || rating is < 1 or > 5 || comment.Length is < 1 or > 1000)
            return CommerceReply.Error(400, "invalid-argument", "Hãy chọn số sao và nhập nhận xét (tối đa 1000 ký tự).");
        RequestIdentity actor;
        try { actor = await verifier.VerifyAsync(bearer.Groups[1].Value, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return CommerceReply.Error(401, "unauthenticated", "Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại."); }
        try
        {
            var reviewId = ReviewId(id); var path = "reviews/" + reviewId;
            var data = await store.RunAsync(async transaction =>
            {
                var records = await transaction.ReadManyAsync(["orders/" + id, path], cancellationToken);
                var order = records[0]?.Data;
                if (order is null || CommerceValues.Text(order, "uid").Length == 0 || CommerceValues.Text(order, "uid") != actor.Uid)
                    throw new BackendException("order-not-found", "Không tìm thấy đơn hàng này trong tài khoản của bạn.");
                if (CommerceValues.Text(order, "status") != "Đã giao") throw new BackendException("order-not-delivered", "Chỉ có thể đánh giá đơn hàng đã giao.");
                if (records[1] is not null) throw new BackendException("already-reviewed", "Bạn đã đánh giá đơn hàng này rồi.");
                var items = CommerceValues.Array(order, "items");
                var products = PricingService.AggregateItems(Js.Element(items)).Select(item => (string)item["id"]).Where(value => value.Length > 0).Take(10).ToList();
                if (products.Count == 0) throw new BackendException("invalid-argument", "Đơn hàng không có thông tin sản phẩm để gắn đánh giá.");
                var name = Js.Cut(CommerceValues.Text(order, "customerName").Trim(), 80);
                var productName = Js.Cut(string.Join(", ", items.Select(item => CommerceValues.Text(CommerceValues.Object(item), "title")).Where(text => text.Length > 0)), 500);
                var review = new Dictionary<string, object>
                {
                    ["authorUid"] = actor.Uid, ["verifiedPurchase"] = true, ["moderationStatus"] = "pending",
                    ["photoIds"] = photos.Select((_, index) => reviewId + "_" + index).ToList(), ["productIds"] = products,
                    ["productName"] = productName.Length > 0 ? productName : "Sản phẩm hoa",
                    ["customerName"] = name.Length == 0 || Regex.IsMatch(name, @"flower\s*flow|cửa hàng|quản trị|admin", RegexOptions.IgnoreCase) ? "Khách hàng" : name,
                    ["rating"] = rating, ["comment"] = comment, ["createdAt"] = StoreValue.ServerTimestamp
                };
                transaction.Create(path, review);
                for (var index = 0; index < photos.Count; index++) transaction.Create("reviewImages/" + reviewId + "_" + index,
                    new() { ["reviewId"] = reviewId, ["bytes"] = photos[index], ["createdAt"] = StoreValue.ServerTimestamp });
                return review;
            }, cancellationToken);
            return new(201, new() { ["review"] = new Dictionary<string, object>(data) { ["id"] = reviewId,
                ["createdAt"] = DocumentTimestamp.FromMilliseconds(time.GetUtcNow().ToUnixTimeMilliseconds()).Iso } });
        }
        catch (BackendException error) { return CommerceReply.Error(error.Code == "order-not-found" ? 404 : error.Code == "invalid-argument" ? 400 : 409, error.Code, error.Message); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return CommerceReply.Error(503, "review-service-unavailable", "Chưa thể gửi đánh giá. Vui lòng thử lại sau."); }
    }
}
