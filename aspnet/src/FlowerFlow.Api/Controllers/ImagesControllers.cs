using System.Text.Json;
using System.Text.Json.Nodes;
using FlowerFlow.Application.Catalog;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace FlowerFlow.Api.Controllers;

[ApiController, Route("api/product-images"), FirestoreEndpoint]
public sealed class ProductImagesController(ProductImagesService images) : ControllerBase
{
    [Route("")]
    public async Task<IActionResult> Handle(CancellationToken cancellationToken)
    {
        Response.Headers.XContentTypeOptions = "nosniff";
        if (Request.Method == "GET")
        {
            var result = await images.ShowAsync(ImageHttp.QueryId(Request), cancellationToken);
            if (result.Failure is { } failure) return ImageHttp.Failure(failure);
            Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            Response.ContentLength = result.Bytes!.Length;
            return File(result.Bytes, "image/jpeg");
        }
        if (Request.Method != "POST")
        {
            Response.Headers.Allow = "GET, POST";
            return ImageHttp.Failure(new(405, "method-not-allowed", "Chỉ hỗ trợ xem hoặc tải ảnh lên."));
        }
        var access = await images.AuthorizeUploadAsync(Request.Headers.Authorization, cancellationToken);
        if (access.Failure is { } denied) return ImageHttp.Failure(denied);
        JsonNode? body = null;
        try
        {
            if (Request.ContentLength is not 0) body = await JsonNode.ParseAsync(Request.Body, cancellationToken: cancellationToken);
        }
        catch (JsonException) { }
        var uploaded = await images.UploadAsync(access.Identity!, (body as JsonObject)?["photo"], cancellationToken);
        return uploaded.Failure is { } error ? ImageHttp.Failure(error) : StatusCode(201, new { url = uploaded.Url });
    }
}

[ApiController, Route("api/review-images"), FirestoreEndpoint]
public sealed class ReviewImagesController(ReviewImagesService images) : ControllerBase
{
    [Route("")]
    public async Task<IActionResult> Handle(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        if (Request.Method != "GET")
        {
            Response.Headers.Allow = "GET";
            return ImageHttp.Failure(new(405, "method-not-allowed", "Chỉ hỗ trợ xem ảnh."));
        }
        var result = await images.ShowAsync(ImageHttp.QueryId(Request), Request.Headers.Authorization, cancellationToken);
        if (result.Failure is { } failure) return ImageHttp.Failure(failure);
        Response.ContentLength = result.Bytes!.Length;
        return File(result.Bytes, "image/jpeg");
    }
}

internal static class ImageHttp
{
    public static string QueryId(HttpRequest request)
    {
        var values = new List<string>();
        foreach (var pair in new QueryStringEnumerable(request.QueryString.Value))
            if (pair.DecodeName().ToString() == "id") values.Add(pair.DecodeValue().ToString());
        return string.Join(",", values);
    }
    public static IActionResult Failure(ProductFailure failure) => new ObjectResult(new { code = failure.Code, error = failure.Message })
        { StatusCode = failure.Status };
}
