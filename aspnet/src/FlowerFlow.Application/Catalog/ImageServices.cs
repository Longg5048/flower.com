using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FlowerFlow.Application.Identity;

namespace FlowerFlow.Application.Catalog;

public sealed record StoredPhoto(string? ReviewId, byte[]? Bytes);
public sealed record ImageReview(string Id, string? ModerationStatus, string? AuthorUid, IReadOnlyList<string>? PhotoIds);
public sealed record ImageResult(byte[]? Bytes, string? Url, ProductFailure? Failure);
public sealed record ImageUploadAccess(RequestIdentity? Identity, ProductFailure? Failure);

public interface IImageStore
{
    void EnsureConfigured();
    Task<StoredPhoto?> ReadProductPhotoAsync(string id, CancellationToken cancellationToken);
    Task CreateProductPhotoAsync(string id, byte[] bytes, string uid, CancellationToken cancellationToken);
    Task<ImageReview?> ReadReviewAsync(string id, CancellationToken cancellationToken);
    Task<StoredPhoto?> ReadReviewPhotoAsync(string id, CancellationToken cancellationToken);
}

public sealed class ProductImagesService(IImageStore store, IFirebaseTokenVerifier verifier,
    RequestIdentityService identities, IAdminRoleReader roles)
{
    public async Task<ImageUploadAccess> AuthorizeUploadAsync(string? authorization, CancellationToken cancellationToken)
    {
        try
        {
            if (!verifier.IsConfigured) return new(null, Unavailable().Failure);
            var identity = await identities.VerifyAsync(authorization, RequiredAccess.Customer, cancellationToken);
            if (identity.Failure is { } failure)
                return new(null, failure.Status == 503 ? Unavailable().Failure : new(failure.Status, failure.Code, failure.Message));
            store.EnsureConfigured();
            var actor = identity.Identity!;
            var role = await roles.ReadAsync(actor.Uid, cancellationToken);
            if (!actor.Claims.TryGetValue("admin", out var admin) || admin is not true || role is null)
                return new(null, new(403, "permission-denied", "Chỉ Admin hoặc Owner đang được cấp quyền mới tải được ảnh sản phẩm."));
            return new(actor, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return new(null, Unavailable().Failure); }
    }

    public async Task<ImageResult> UploadAsync(RequestIdentity actor, JsonNode? photo, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = ImageRules.ValidatePhoto(photo);
            if (bytes is null) return new(null, null, new(400, "invalid-photo", "Ảnh không hợp lệ. Hãy chọn ảnh JPG, PNG hoặc WebP khác."));
            var id = Convert.ToHexStringLower(SHA1.HashData(bytes));
            await store.CreateProductPhotoAsync(id, bytes, actor.Uid, cancellationToken);
            return new(null, $"/api/product-images?id={id}", null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return Unavailable(); }
    }

    public async Task<ImageResult> ShowAsync(string id, CancellationToken cancellationToken)
    {
        if (!Regex.IsMatch(id, @"\A[a-f0-9]{40}\z")) return NotFound();
        try
        {
            var photo = await store.ReadProductPhotoAsync(id, cancellationToken);
            return photo is null ? NotFound() : photo.Bytes is null ? Unavailable() : new(photo.Bytes, null, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return Unavailable(); }
    }
    public static ImageResult NotFound() => new(null, null, new(404, "photo-not-found", "Không tìm thấy ảnh."));
    private static ImageResult Unavailable() => new(null, null, new(503, "photo-unavailable", "Chưa xử lý được ảnh. Hãy thử lại."));
}

public sealed class ReviewImagesService(IImageStore store, IFirebaseTokenVerifier verifier, IAdminRoleReader roles)
{
    public async Task<ImageResult> ShowAsync(string id, string? authorization, CancellationToken cancellationToken)
    {
        if (!Regex.IsMatch(id, @"\A[a-f0-9]{40}_[0-2]\z")) return ProductImagesService.NotFound();
        try
        {
            var review = await store.ReadReviewAsync(id[..40], cancellationToken);
            if (review is null) return ProductImagesService.NotFound();
            var allowed = review.ModerationStatus == "approved";
            if (!allowed)
            {
                var bearer = Regex.Match(authorization ?? "", @"^Bearer\s+(.+)$", RegexOptions.IgnoreCase);
                if (bearer.Success)
                {
                    RequestIdentity actor;
                    try { actor = await verifier.VerifyAsync(bearer.Groups[1].Value, cancellationToken); }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                    catch (Exception) { return new(null, null, new(401, "unauthenticated", "Phiên đăng nhập hết hạn.")); }
                    allowed = actor.Uid == review.AuthorUid;
                    if (!allowed && actor.Claims.TryGetValue("admin", out var admin) && admin is true)
                        allowed = await roles.ReadAsync(actor.Uid, cancellationToken) is not null;
                }
            }
            if (!allowed || review.PhotoIds is null || !review.PhotoIds.Contains(id)) return ProductImagesService.NotFound();
            var photo = await store.ReadReviewPhotoAsync(id, cancellationToken);
            if (photo is null || photo.ReviewId != review.Id) return ProductImagesService.NotFound();
            return photo.Bytes is null ? Unavailable() : new(photo.Bytes, null, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return Unavailable(); }
    }
    private static ImageResult Unavailable() => new(null, null, new(503, "photo-unavailable", "Chưa tải được ảnh. Hãy thử lại."));
}
