using System.Text.Json;
using System.Text.Json.Nodes;
using FlowerFlow.Application.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace FlowerFlow.Api.Controllers;

[ApiController, Route("api"), FirestoreEndpoint]
public sealed class AdminController(AdminService admin) : ControllerBase
{
    [Route("admin/users")]
    public async Task<IActionResult> Users(CancellationToken cancellationToken)
    {
        var routes = new List<string>();
        foreach (var pair in new QueryStringEnumerable(Request.QueryString.Value))
            if (pair.DecodeName().ToString() == "route") routes.Add(pair.DecodeValue().ToString());
        if (routes.Count > 0)
        {
            if (routes.Count == 1 && routes[0] == "audit") return await Audit(cancellationToken);
            Response.Headers.CacheControl = "no-store";
            return ImageHttp.Failure(new(404, "api-not-found", "Không tìm thấy API."));
        }
        return await HandleAsync(false, false, cancellationToken);
    }
    [Route("owner/users")]
    public Task<IActionResult> Owner(CancellationToken cancellationToken) => HandleAsync(true, false, cancellationToken);
    [Route("admin/audit")]
    public Task<IActionResult> Audit(CancellationToken cancellationToken) => HandleAsync(false, true, cancellationToken);
    private async Task<IActionResult> HandleAsync(bool owner, bool audit, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = audit ? "private, no-store" : "no-store";
        if (Request.Method != (audit ? "GET" : "POST"))
        {
            Response.Headers.Allow = audit ? "GET" : "POST";
            return ImageHttp.Failure(new(405, "method-not-allowed", audit ? "API nhật ký quản trị chỉ chấp nhận GET." : "API này chỉ chấp nhận phương thức POST."));
        }
        var access = await admin.AuthorizeAsync(Request.Headers.Authorization, owner, audit, cancellationToken);
        if (access.Failure is { } denied) return ImageHttp.Failure(new(denied.Status, denied.Code, denied.Message));
        AdminReply result;
        if (audit) result = await admin.AuditAsync(cancellationToken);
        else
        {
            JsonNode? body = null;
            try { if (Request.ContentLength is not 0) body = await JsonNode.ParseAsync(Request.Body, cancellationToken: cancellationToken); } catch (JsonException) { }
            result = owner ? await admin.OwnerAsync(access.Identity!, body, cancellationToken) : await admin.ListAsync(body, false, cancellationToken);
        }
        return result.Failure is { } failure ? ImageHttp.Failure(failure) : Ok(result.Body);
    }
}
