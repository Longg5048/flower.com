using System.Text.Json;
using System.Text.Json.Nodes;
using FlowerFlow.Application.Accounts;
using Microsoft.AspNetCore.Mvc;

namespace FlowerFlow.Api.Controllers;

[ApiController, Route("api/login"), FirestoreEndpoint]
public sealed class AccountsController(LoginService login) : ControllerBase
{
    [Route("")]
    public async Task<IActionResult> Handle(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (Request.Method != "POST")
        {
            Response.Headers.Allow = "POST";
            return StatusCode(405, new { code = "method-not-allowed", error = "Phương thức không được hỗ trợ." });
        }
        JsonNode? body = null;
        try { if (Request.ContentLength is not 0) body = await JsonNode.ParseAsync(Request.Body, cancellationToken: cancellationToken); }
        catch (JsonException) { }
        var result = await login.LoginAsync(body, cancellationToken);
        if (result.RetryAfter is > 0) Response.Headers.RetryAfter = result.RetryAfter.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return StatusCode(result.Status, result.Body);
    }
}
