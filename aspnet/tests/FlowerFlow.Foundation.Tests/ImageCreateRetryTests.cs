using FlowerFlow.Infrastructure.Firebase;
using Google.Api.Gax.Grpc;
using Google.Cloud.Firestore;
using Google.Cloud.Firestore.V1;
using Grpc.Core;
using Xunit;

namespace FlowerFlow.Foundation.Tests;

public sealed class ImageCreateRetryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Aborted_create_retries_without_overwriting_an_existing_photo(bool alreadyExists)
    {
        var client = new CommitClient(alreadyExists ? [StatusCode.Aborted, StatusCode.AlreadyExists] : [StatusCode.Aborted]);
        var store = new FirestoreImageStore(() => FirestoreDb.Create("demo-image-retry", client));
        await store.CreateProductPhotoAsync(new string('a', 40), ImageContractTests.Sample, "user-a", CancellationToken.None);
        Assert.Equal(2, client.Requests.Count);
        Assert.All(client.Requests, request =>
        {
            var write = Assert.Single(request.Writes);
            Assert.Equal(Google.Cloud.Firestore.V1.Precondition.ConditionTypeOneofCase.Exists, write.CurrentDocument.ConditionTypeCase);
            Assert.False(write.CurrentDocument.Exists);
            Assert.Equal(client.Requests[0], request);
        });
    }

    [Theory]
    [InlineData(StatusCode.PermissionDenied, 1)]
    [InlineData(StatusCode.Aborted, 5)]
    public async Task Non_duplicate_failures_are_not_silently_accepted_and_retries_are_bounded(StatusCode code, int attempts)
    {
        var client = new CommitClient(Enumerable.Repeat(code, attempts + 1).ToArray());
        var store = new FirestoreImageStore(() => FirestoreDb.Create("demo-image-retry", client));
        var error = await Assert.ThrowsAsync<RpcException>(() => store.CreateProductPhotoAsync(new string('a', 40), ImageContractTests.Sample, "user-a", CancellationToken.None));
        Assert.Equal(code, error.StatusCode);
        Assert.Equal(attempts, client.Requests.Count);
    }

    [Fact]
    public async Task Cancellation_during_retry_stops_additional_creates()
    {
        using var cancelled = new CancellationTokenSource();
        var client = new CommitClient([StatusCode.Aborted]) { OnCommit = cancelled.Cancel };
        var store = new FirestoreImageStore(() => FirestoreDb.Create("demo-image-retry", client));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.CreateProductPhotoAsync(new string('a', 40), ImageContractTests.Sample, "user-a", cancelled.Token));
        Assert.Single(client.Requests);
    }

    private sealed class CommitClient(StatusCode[] errors) : FirestoreClient
    {
        public List<CommitRequest> Requests { get; } = [];
        public Action? OnCommit { get; init; }
        public override Task<CommitResponse> CommitAsync(CommitRequest request, CallSettings? callSettings = null)
        {
            Requests.Add(request.Clone());
            OnCommit?.Invoke();
            if (Requests.Count <= errors.Length)
                return Task.FromException<CommitResponse>(new RpcException(new Status(errors[Requests.Count - 1], "provider-private")));
            var timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(new DateTime(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc));
            return Task.FromResult(new CommitResponse { CommitTime = timestamp, WriteResults = { new Google.Cloud.Firestore.V1.WriteResult { UpdateTime = timestamp } } });
        }
    }
}
