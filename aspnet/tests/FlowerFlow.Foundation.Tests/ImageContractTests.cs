using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowerFlow.Application.Catalog;
using FlowerFlow.Application.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace FlowerFlow.Foundation.Tests;

public sealed class ImageContractTests
{
    internal static readonly byte[] Sample = [0xff, 0xd8, 0xff, 0xc0, 0, 8, 8, 0, 100, 0, 100, 0, 0xff, 0xd9];
    internal static string Root()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "flower.com/backend/catalog/image-rules.js"))) root = root.Parent;
        return root?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
    private static async Task<JsonDocument> OracleAsync(string name)
    {
        var start = new ProcessStartInfo("node")
        {
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        start.ArgumentList.Add(Path.Combine(Root(), "aspnet/tests/FlowerFlow.Foundation.Tests", name));
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); } catch { process.Kill(true); throw; }
        Assert.True(process.ExitCode == 0, await errors);
        return JsonDocument.Parse(await output);
    }

    [Fact]
    public async Task JPEG_dimensions_and_photo_limits_match_actual_JavaScript()
    {
        using var oracle = await OracleAsync("image-rules-oracle.cjs");
        Assert.Equal(ImageRules.MaxPhotoBytes, oracle.RootElement.GetProperty("maxBytes").GetInt32());
        Assert.Equal(ImageRules.MaxReviewPhotos, oracle.RootElement.GetProperty("maxPhotos").GetInt32());
        var dimensions = oracle.RootElement.GetProperty("dimensions");
        var validations = oracle.RootElement.GetProperty("validations");
        Assert.Equal(17, dimensions.GetArrayLength());
        Assert.Equal(23, validations.GetArrayLength());
        foreach (var input in dimensions.EnumerateArray())
        {
            JpegSize? size = null;
            var error = Record.Exception(() => size = ImageRules.JpegDimensions(Convert.FromBase64String(input.GetProperty("bytes").GetString()!)));
            Assert.Equal(input.GetProperty("throws").GetBoolean(), error is not null);
            if (error is not null) continue;
            var actual = size is null ? null : JsonSerializer.SerializeToNode(new { width = size.Width, height = size.Height });
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(input.GetProperty("expected").GetRawText()), actual), input.GetProperty("name").GetString());
        }
        foreach (var input in validations.EnumerateArray())
        {
            IReadOnlyList<byte[]>? result = null;
            var error = Record.Exception(() => result = input.TryGetProperty("photos", out var photos)
                ? ImageRules.ValidateReviewPhotos(JsonNode.Parse(photos.GetRawText())) : ImageRules.ValidateReviewPhotos());
            Assert.Equal(input.GetProperty("throws").GetBoolean(), error is not null);
            if (error is not null) continue;
            var actual = result is null ? null : JsonSerializer.SerializeToNode(result.Select(bytes => new
                { size = bytes.Length, sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)) }));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(input.GetProperty("expected").GetRawText()), actual), input.GetProperty("name").GetString());
        }
    }

    [Fact]
    public async Task Product_and_review_HTTP_images_match_JavaScript_binary_headers_permissions_and_writes()
    {
        using var oracle = await OracleAsync("images-oracle.cjs");
        Assert.Equal(46, oracle.RootElement.GetArrayLength());
        foreach (var input in oracle.RootElement.EnumerateArray())
        {
            var backend = new Backend(input);
            await using var host = new Host(backend);
            using var client = host.CreateClient();
            var query = new List<string>();
            if (input.TryGetProperty("query", out var values))
                foreach (var pair in values.EnumerateObject())
                    foreach (var value in pair.Value.ValueKind == JsonValueKind.Array ? pair.Value.EnumerateArray().ToArray() : new[] { pair.Value })
                        query.Add(Uri.EscapeDataString(pair.Name) + "=" + Uri.EscapeDataString(value.GetString()!));
            var route = input.GetProperty("kind").GetString() == "product" ? "/api/product-images?" : "/api/review-images?";
            using var request = new HttpRequestMessage(new HttpMethod(input.TryGetProperty("method", out var method) ? method.GetString()! : "GET"), route + string.Join("&", query));
            var authorization = input.TryGetProperty("authorization", out var auth) ? auth.GetString()! : "Bearer token";
            if (authorization.Length > 0) request.Headers.TryAddWithoutValidation("Authorization", authorization);
            if (input.TryGetProperty("body", out var body)) request.Content = new StringContent(body.GetRawText(), Encoding.UTF8, "application/json");
            var response = await client.SendAsync(request);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            var binary = response.Content.Headers.ContentType?.MediaType == "image/jpeg";
            var actual = JsonSerializer.SerializeToNode(new
            {
                status = (int)response.StatusCode, body = binary ? null : JsonNode.Parse(bytes),
                binary = binary ? Convert.ToBase64String(bytes) : null,
                cache = response.Headers.NonValidated.TryGetValues("Cache-Control", out var cache) ? string.Join(", ", cache) : null,
                allow = response.Content.Headers.TryGetValues("Allow", out var allow) ? string.Join(", ", allow) : null,
                nosniff = response.Headers.TryGetValues("X-Content-Type-Options", out var nosniff) ? string.Join(", ", nosniff) : null,
                contentType = response.Content.Headers.ContentType?.MediaType,
                length = binary ? response.Content.Headers.ContentLength?.ToString(CultureInfo.InvariantCulture) : null,
                tokenReads = backend.TokenReads, roleReads = backend.RoleReads, reviewReads = backend.ReviewReads,
                photoReads = backend.PhotoReads, createAttempts = backend.CreateAttempts, writes = backend.Writes
            });
            var expected = JsonNode.Parse(input.GetProperty("expected").GetRawText());
            Assert.True(JsonNode.DeepEquals(expected, actual), $"{input.GetProperty("name")}: expected {expected}; actual {actual}");
        }
    }

    internal sealed class Host(Backend backend, IImageStore? store = null, IAdminRoleReader? roles = null) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:FlowerFlow"] = "Server=127.0.0.1,1;Database=DoNotUse;Connect Timeout=1;User Id=test;Password=test;Encrypt=False"
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IFirebaseTokenVerifier>(); services.AddSingleton<IFirebaseTokenVerifier>(backend);
                services.RemoveAll<IAdminRoleReader>(); services.AddSingleton<IAdminRoleReader>(roles ?? backend);
                services.RemoveAll<IImageStore>(); services.AddSingleton<IImageStore>(store ?? backend);
            });
        }
    }

    internal sealed class Backend(JsonElement input) : IFirebaseTokenVerifier, IAdminRoleReader, IImageStore
    {
        public int TokenReads { get; private set; }
        public int RoleReads { get; private set; }
        public int ReviewReads { get; private set; }
        public int PhotoReads { get; private set; }
        public int CreateAttempts { get; private set; }
        public JsonArray Writes { get; } = [];
        private bool Flag(string name) => input.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
        private string String(string name, string fallback) => input.TryGetProperty(name, out var value) ? value.GetString()! : fallback;
        public bool IsConfigured => !Flag("authError");
        public void EnsureConfigured() { if (Flag("storeError")) throw new FirebaseConfigurationException(); }
        public Task<RequestIdentity> VerifyAsync(string token, CancellationToken cancellationToken)
        {
            if (Flag("authError")) throw new FirebaseConfigurationException();
            TokenReads++;
            if (Flag("tokenError")) throw new InvalidFirebaseTokenException();
            object admin = input.TryGetProperty("admin", out var value) ? value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetBoolean() : true;
            return Task.FromResult(new RequestIdentity("user-a", new Dictionary<string, object> { ["admin"] = admin }));
        }
        public Task<AdminRole?> ReadAsync(string uid, CancellationToken cancellationToken)
        {
            Assert.Equal("user-a", uid); RoleReads++;
            if (Flag("roleError")) throw new Exception("provider-private");
            return Task.FromResult<AdminRole?>(Flag("roleMissing") ? null : new(null));
        }
        public Task<StoredPhoto?> ReadProductPhotoAsync(string id, CancellationToken cancellationToken)
        { EnsureConfigured(); PhotoReads++; return Task.FromResult(Photo()); }
        public Task<StoredPhoto?> ReadReviewPhotoAsync(string id, CancellationToken cancellationToken)
        { EnsureConfigured(); PhotoReads++; return Task.FromResult(Photo()); }
        private StoredPhoto? Photo() => Flag("photoMissing") ? null : new(Flag("wrongReview") ? "wrong-review" : new string('b', 40), Flag("malformedBytes") ? null : Sample);
        public Task<ImageReview?> ReadReviewAsync(string id, CancellationToken cancellationToken)
        {
            EnsureConfigured(); ReviewReads++;
            return Task.FromResult<ImageReview?>(Flag("reviewMissing") ? null : new(id, String("moderation", "approved"),
                String("author", "other-user"), Flag("badPhotoIds") ? null : Flag("unlisted") ? Array.Empty<string>() : new[] { id + "_0" }));
        }
        public Task CreateProductPhotoAsync(string id, byte[] bytes, string uid, CancellationToken cancellationToken)
        {
            CreateAttempts++;
            if (Flag("duplicate")) return Task.CompletedTask;
            if (Flag("createError")) throw new Exception("provider-private");
            Writes.Add(new JsonObject
            {
                ["id"] = id, ["bytes"] = Convert.ToBase64String(bytes), ["size"] = bytes.Length,
                ["uploadedBy"] = uid, ["createdAt"] = "$serverTimestamp"
            });
            return Task.CompletedTask;
        }
    }
}
