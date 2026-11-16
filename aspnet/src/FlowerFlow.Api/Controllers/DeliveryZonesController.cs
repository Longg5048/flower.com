using System.Text.Json;
using System.Text.Json.Nodes;
using FlowerFlow.Application.ShopSettings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace FlowerFlow.Api.Controllers;

[ApiController, Route("api"), FirestoreEndpoint]
public sealed class DeliveryZonesController(SettingsService settings) : ControllerBase
{
    [Route("delivery-zones")]
    public Task<IActionResult> Zones(CancellationToken cancellationToken) => HandleAsync(SettingsKind.DeliveryZones, cancellationToken);
    [Route("bouquet-wrapping")]
    public Task<IActionResult> Wrapping(CancellationToken cancellationToken) => HandleAsync(SettingsKind.BouquetWrapping, cancellationToken);

    private async Task<IActionResult> HandleAsync(SettingsKind kind, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (kind == SettingsKind.DeliveryZones)
        {
            var routes = Values("route");
            if (routes.Count > 0)
            {
                if (routes.Count != 1 || routes[0] != "bouquet-wrapping")
                    return ImageHttp.Failure(new(404, "api-not-found", "Không tìm thấy API."));
                kind = SettingsKind.BouquetWrapping;
            }
        }
        if (settings.Configuration(kind) is { } configuration) return ImageHttp.Failure(configuration);
        if (Request.Method is not ("GET" or "POST"))
        {
            Response.Headers.Allow = "GET, POST";
            return ImageHttp.Failure(new(405, "method-not-allowed", kind == SettingsKind.DeliveryZones
                ? "API khu vực giao hàng chỉ chấp nhận GET hoặc POST." : "API phí bó hoa chỉ chấp nhận GET hoặc POST."));
        }
        var manage = Values("manage");
        var management = manage.Count == 1 && manage[0] == "1";
        var access = Request.Method == "POST" || management ? await settings.AuthorizeAsync(kind, Request.Headers.Authorization, cancellationToken) : null;
        if (access?.Failure is { } denied) return ImageHttp.Failure(denied);
        SettingsResult result;
        if (Request.Method == "GET") result = await settings.ReadAsync(kind, management, cancellationToken);
        else
        {
            JsonNode? body = null;
            try { if (Request.ContentLength is not 0) body = await JsonNode.ParseAsync(Request.Body, cancellationToken: cancellationToken); }
            catch (JsonException) { }
            result = await settings.UpdateAsync(kind, access!.Identity!, body, cancellationToken);
        }
        return result.Failure is { } failure ? ImageHttp.Failure(failure) : Ok(result.Body);
    }
    private List<string> Values(string name)
    {
        var values = new List<string>();
        foreach (var pair in new QueryStringEnumerable(Request.QueryString.Value))
            if (pair.DecodeName().ToString() == name) values.Add(pair.DecodeValue().ToString());
        return values;
    }
}
