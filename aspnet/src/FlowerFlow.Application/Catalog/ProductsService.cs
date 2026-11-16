using System.Text.Json.Nodes;
using FlowerFlow.Application.Identity;

namespace FlowerFlow.Application.Catalog;

public sealed record ProductFailure(int Status, string Code, string Message);
public sealed record ProductsResult(IReadOnlyList<JsonObject>? Products, ProductFailure? Failure);
public sealed class ProductsService(IProductReader reader, ProductSerializer serializer,
    RequestIdentityService identities, IAdminRoleReader roles)
{
    public async Task<ProductsResult> ListAsync(bool manage, string? authorization, CancellationToken cancellationToken)
    {
        try { reader.EnsureConfigured(); }
        catch (Exception) { return Error("admin-api-not-configured", "API quản trị Firebase chưa được cấu hình."); }
        try
        {
            if (manage)
            {
                var result = await identities.VerifyAsync(authorization, RequiredAccess.Customer, cancellationToken);
                if (result.Failure is { } failure)
                {
                    if (failure.Status == 503) return Unavailable();
                    return new(null, new(failure.Status, failure.Code, failure.Message));
                }
                var identity = result.Identity!;
                // Products reads the role even when the admin claim is missing.
                var role = await roles.ReadAsync(identity.Uid, cancellationToken);
                if (!identity.Claims.TryGetValue("admin", out var admin) || admin is not true || role is null)
                    return new(null, new(403, "permission-denied", "Chỉ Admin hoặc Owner đang được cấp quyền mới xem được toàn bộ sản phẩm."));
            }
            var documents = await reader.ReadAllAsync(cancellationToken);
            return new(documents.Select(serializer.Serialize).Where(product => manage || product["status"] is not JsonValue status ||
                !status.TryGetValue<string>(out var text) || text != "hidden").ToArray(), null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return Unavailable(); }
    }
    private static ProductsResult Unavailable() => Error("product-service-unavailable", "Máy chủ chưa đọc được kho sản phẩm. Vui lòng thử lại sau.");
    private static ProductsResult Error(string code, string message) => new(null, new(503, code, message));
}
