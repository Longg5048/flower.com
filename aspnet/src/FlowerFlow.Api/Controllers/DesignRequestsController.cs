using System.Text.Json;
using FlowerFlow.Application.DesignRequests;
using FlowerFlow.Application.Commerce;
using FlowerFlow.Application.Identity;
using FlowerFlow.Domain.Models.Compatibility;
using Microsoft.AspNetCore.Mvc;

namespace FlowerFlow.Api.Controllers;

[ApiController, Route("api/design-requests"), FirestoreEndpoint]
public sealed class DesignRequestsController(DesignRequestsService requests, ICommerceStore store) : ControllerBase
{
    [Route("")]
    public async Task<IActionResult> Handle(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        try { store.EnsureConfigured(); }
        catch (FirebaseConfigurationException) { return StatusCode(503, new { code = "admin-api-not-configured", error = "API yêu cầu thiết kế chưa được thiết lập." }); }
        CommerceReply reply;
        if (Request.Method == "GET")
        {
            var query = PaymentQuery.Read(Request);
            reply = await requests.GetAsync(Js.StringOrEmpty(JsonSerializer.SerializeToElement(query["id"])), Js.StringOrEmpty(JsonSerializer.SerializeToElement(query["token"])),
                query["manage"] is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<string>(out var manage) && manage == "1", Request.Headers.Authorization, cancellationToken);
        }
        else if (Request.Method == "POST")
        {
            JsonElement body = default;
            try { using var document = await JsonDocument.ParseAsync(Request.Body, cancellationToken: cancellationToken); body = document.RootElement.Clone(); } catch (JsonException) { }
            reply = await requests.PostAsync(body, Request.Headers.Authorization, cancellationToken);
        }
        else reply = CommerceReply.Error(405, "method-not-allowed", "API yêu cầu thiết kế chỉ chấp nhận GET hoặc POST.", allow: "GET, POST");
        if (reply.Allow is { } allow) Response.Headers.Allow = allow;
        return StatusCode(reply.Status, reply.Json);
    }
}
