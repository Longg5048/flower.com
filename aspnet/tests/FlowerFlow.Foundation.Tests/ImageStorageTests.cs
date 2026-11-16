using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowerFlow.Application.Catalog;
using FlowerFlow.Infrastructure.Firebase;
using Google.Cloud.Firestore;
using Xunit;

namespace FlowerFlow.Foundation.Tests;

public sealed class ImageStorageTests
{
    [FirestoreEmulatorFact, Trait("Category", "Emulator")]
    public async Task Product_upload_deduplicates_concurrent_creates_and_preserves_metadata_and_binary()
    {
        await using var fixture = await Fixture.CreateAsync();
        using var input = JsonDocument.Parse("{}");
        var backend = new ImageContractTests.Backend(input.RootElement);
        await using var host = new ImageContractTests.Host(backend, fixture.Store, new FirestoreAdminRoleReader(() => fixture.Database));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin");
        var bytes = await File.ReadAllBytesAsync(Path.Combine(ImageContractTests.Root(), "flower.com/images/start.jpeg"), fixture.Token);
        var photo = "data:image/jpeg;base64," + Convert.ToBase64String(bytes);
        var id = Convert.ToHexStringLower(SHA1.HashData(bytes));
        var response = await client.PostAsJsonAsync("/api/product-images", new { photo }, fixture.Token);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var url = JsonNode.Parse(await response.Content.ReadAsStringAsync(fixture.Token))!["url"]!.GetValue<string>();
        Assert.Equal("/api/product-images?id=" + id, url);
        var reference = fixture.Database.Collection("productImages").Document(id);
        var original = await reference.GetSnapshotAsync(fixture.Token);
        Assert.Equal(bytes, original.GetValue<Blob>("bytes").ByteString.ToByteArray());
        Assert.Equal(bytes.Length, original.GetValue<long>("size"));
        Assert.Equal("user-a", original.GetValue<string>("uploadedBy"));
        Assert.True(original.ContainsField("createdAt"));
        // Exercise SDK duplicate handling directly so provider errors are visible if it fails.
        await fixture.Store.CreateProductPhotoAsync(id, bytes, "another-uploader", fixture.Token);
        var duplicates = await Task.WhenAll(
            client.PostAsJsonAsync("/api/product-images", new { photo }, fixture.Token),
            client.PostAsJsonAsync("/api/product-images", new { photo }, fixture.Token));
        Assert.All(duplicates, duplicate => Assert.Equal(HttpStatusCode.Created, duplicate.StatusCode));
        Assert.Equal(original.UpdateTime, (await reference.GetSnapshotAsync(fixture.Token)).UpdateTime);
        Assert.Single((await fixture.Database.Collection("productImages").GetSnapshotAsync(fixture.Token)).Documents);
        var tokenReads = backend.TokenReads;
        using var get = new HttpRequestMessage(HttpMethod.Get, url);
        get.Headers.Range = new RangeHeaderValue(0, 1);
        var image = await client.SendAsync(get, fixture.Token);
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal(bytes, await image.Content.ReadAsByteArrayAsync(fixture.Token));
        Assert.Equal(bytes.Length, image.Content.Headers.ContentLength);
        Assert.Equal("image/jpeg", image.Content.Headers.ContentType!.MediaType);
        Assert.Equal("public, max-age=31536000, immutable", image.Headers.NonValidated["Cache-Control"].ToString());
        Assert.Equal("nosniff", image.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(tokenReads, backend.TokenReads);
        await fixture.Role.DeleteAsync(cancellationToken: fixture.Token);
        var denied = await client.PostAsJsonAsync("/api/product-images", new { photo }, fixture.Token);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(original.UpdateTime, (await reference.GetSnapshotAsync(fixture.Token)).UpdateTime);
    }

    [FirestoreEmulatorFact, Trait("Category", "Emulator")]
    public async Task Review_images_obey_author_admin_moderation_and_document_relationship()
    {
        await using var fixture = await Fixture.CreateAsync();
        using var input = JsonDocument.Parse("{}");
        var backend = new ImageContractTests.Backend(input.RootElement);
        await using var host = new ImageContractTests.Host(backend, fixture.Store, new FirestoreAdminRoleReader(() => fixture.Database));
        using var client = host.CreateClient();
        using var anonymous = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "author");
        var id = new string('b', 40);
        var photoId = id + "_0";
        var review = fixture.Database.Collection("reviews").Document(id);
        var photo = fixture.Database.Collection("reviewImages").Document(photoId);
        await review.SetAsync(new Dictionary<string, object>
        { ["authorUid"] = "user-a", ["moderationStatus"] = "pending", ["photoIds"] = new[] { photoId } }, cancellationToken: fixture.Token);
        await photo.SetAsync(new Dictionary<string, object>
        { ["reviewId"] = id, ["bytes"] = ImageContractTests.Sample }, cancellationToken: fixture.Token);
        await fixture.Role.DeleteAsync(cancellationToken: fixture.Token);
        var url = "/api/review-images?id=" + photoId;
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(url, fixture.Token)).StatusCode);
        var own = await client.GetAsync(url, fixture.Token);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(ImageContractTests.Sample, await own.Content.ReadAsByteArrayAsync(fixture.Token));
        Assert.Equal("private, no-store", own.Headers.NonValidated["Cache-Control"].ToString());
        await review.UpdateAsync(new Dictionary<string, object> { ["authorUid"] = "other-user" }, cancellationToken: fixture.Token);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(url, fixture.Token)).StatusCode);
        await fixture.Role.SetAsync(new Dictionary<string, object> { ["role"] = "admin" }, cancellationToken: fixture.Token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url, fixture.Token)).StatusCode);
        await photo.UpdateAsync(new Dictionary<string, object> { ["reviewId"] = "wrong-review" }, cancellationToken: fixture.Token);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(url, fixture.Token)).StatusCode);
        await photo.UpdateAsync(new Dictionary<string, object> { ["reviewId"] = id }, cancellationToken: fixture.Token);
        await review.UpdateAsync(new Dictionary<string, object> { ["moderationStatus"] = "approved" }, cancellationToken: fixture.Token);
        var tokenReads = backend.TokenReads;
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync(url, fixture.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url, fixture.Token)).StatusCode);
        Assert.Equal(tokenReads, backend.TokenReads);
        await review.UpdateAsync(new Dictionary<string, object> { ["photoIds"] = Array.Empty<string>() }, cancellationToken: fixture.Token);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(url, fixture.Token)).StatusCode);
        await review.DeleteAsync(cancellationToken: fixture.Token);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(url, fixture.Token)).StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Malformed_upload_JSON_still_requires_authentication_and_never_writes(bool authenticated)
    {
        using var input = JsonDocument.Parse("{}");
        var backend = new ImageContractTests.Backend(input.RootElement);
        await using var host = new ImageContractTests.Host(backend);
        using var client = host.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/product-images")
        { Content = new StringContent("{private-invalid-photo", Encoding.UTF8, "application/json") };
        if (authenticated) request.Headers.TryAddWithoutValidation("Authorization", "Bearer admin");
        var response = await client.SendAsync(request);
        Assert.Equal(authenticated ? 400 : 401, (int)response.StatusCode);
        Assert.DoesNotContain("private-invalid-photo", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, backend.CreateAttempts);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Image_services_propagate_request_cancellation(bool review)
    {
        using var input = JsonDocument.Parse("{}");
        var backend = new ImageContractTests.Backend(input.RootElement);
        var store = new CancelledStore();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        if (review)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ReviewImagesService(store, backend, backend)
                .ShowAsync(new string('b', 40) + "_0", null, cancelled.Token));
        else
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ProductImagesService(store, backend, new(backend, backend), backend)
                .ShowAsync(new string('a', 40), cancelled.Token));
    }

    private sealed class CancelledStore : IImageStore
    {
        public void EnsureConfigured() { }
        public Task<StoredPhoto?> ReadProductPhotoAsync(string id, CancellationToken cancellationToken) => Task.FromCanceled<StoredPhoto?>(cancellationToken);
        public Task<StoredPhoto?> ReadReviewPhotoAsync(string id, CancellationToken cancellationToken) => Task.FromCanceled<StoredPhoto?>(cancellationToken);
        public Task<ImageReview?> ReadReviewAsync(string id, CancellationToken cancellationToken) => Task.FromCanceled<ImageReview?>(cancellationToken);
        public Task CreateProductPhotoAsync(string id, byte[] bytes, string uid, CancellationToken cancellationToken) => Task.FromCanceled(cancellationToken);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public required FirestoreDb Database { get; init; }
        public required FirestoreImageStore Store { get; init; }
        public DocumentReference Role => Database.Collection("adminRoles").Document("user-a");
        private readonly CancellationTokenSource timeout = new(TimeSpan.FromSeconds(40));
        public CancellationToken Token => timeout.Token;
        public static async Task<Fixture> CreateAsync()
        {
            var endpoint = Environment.GetEnvironmentVariable("FLOWERFLOW_TEST_FIRESTORE_HOST")!;
            if (!Uri.TryCreate("http://" + endpoint, UriKind.Absolute, out var uri) || !uri.IsLoopback)
                throw new InvalidOperationException("Only loopback emulator endpoints are permitted.");
            var database = new FirestoreDbBuilder
            {
                ProjectId = "demo-images-" + Guid.NewGuid().ToString("N")[..12], Endpoint = endpoint,
                ChannelCredentials = Grpc.Core.ChannelCredentials.Insecure,
                Settings = new Google.Cloud.Firestore.V1.FirestoreSettings
                { CallSettings = Google.Api.Gax.Grpc.CallSettings.FromHeader("Authorization", "Bearer owner") }
            }.Build();
            var fixture = new Fixture { Database = database, Store = new(() => database) };
            await fixture.Role.SetAsync(new Dictionary<string, object> { ["role"] = "admin" }, cancellationToken: fixture.Token);
            return fixture;
        }
        public async ValueTask DisposeAsync()
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            foreach (var collection in new[] { "adminRoles", "productImages", "reviews", "reviewImages" })
                foreach (var document in (await Database.Collection(collection).GetSnapshotAsync(cleanup.Token)).Documents)
                    await document.Reference.DeleteAsync(cancellationToken: cleanup.Token);
            timeout.Dispose();
        }
    }
}
