using FlowerFlow.Application.Catalog;
using Google.Cloud.Firestore;
using Grpc.Core;

namespace FlowerFlow.Infrastructure.Firebase;

public sealed class FirestoreImageStore(Func<FirestoreDb> database) : IImageStore
{
    public void EnsureConfigured() => _ = database();
    public Task<StoredPhoto?> ReadProductPhotoAsync(string id, CancellationToken cancellationToken) => ReadPhotoAsync("productImages", id, cancellationToken);
    public Task<StoredPhoto?> ReadReviewPhotoAsync(string id, CancellationToken cancellationToken) => ReadPhotoAsync("reviewImages", id, cancellationToken);

    private async Task<StoredPhoto?> ReadPhotoAsync(string collection, string id, CancellationToken cancellationToken)
    {
        var snapshot = await database().Collection(collection).Document(id).GetSnapshotAsync(cancellationToken);
        if (!snapshot.Exists) return null;
        var fields = snapshot.ToDictionary();
        var bytes = fields.GetValueOrDefault("bytes") switch
        {
            Blob blob => blob.ByteString.ToByteArray(), byte[] array => array,
            Google.Protobuf.ByteString buffer => buffer.ToByteArray(), _ => null
        };
        return new(fields.GetValueOrDefault("reviewId") as string, bytes);
    }
    public async Task CreateProductPhotoAsync(string id, byte[] bytes, string uid, CancellationToken cancellationToken)
    {
        var reference = database().Collection("productImages").Document(id);
        var data = new Dictionary<string, object>
        {
            ["bytes"] = bytes, ["size"] = bytes.Length, ["uploadedBy"] = uid, ["createdAt"] = FieldValue.ServerTimestamp
        };
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await reference.CreateAsync(data, cancellationToken);
                return;
            }
            catch (RpcException error) when (error.StatusCode == StatusCode.AlreadyExists) { return; }
            // Node Firestore WriteBatch.commit retries ABORTED too. Create with a content ID is safe to retry.
            catch (RpcException error) when (error.StatusCode == StatusCode.Aborted && attempt < 4)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50 * (1 << attempt)), cancellationToken);
            }
        }
    }
    public async Task<ImageReview?> ReadReviewAsync(string id, CancellationToken cancellationToken)
    {
        var snapshot = await database().Collection("reviews").Document(id).GetSnapshotAsync(cancellationToken);
        if (!snapshot.Exists) return null;
        var fields = snapshot.ToDictionary();
        var photoIds = fields.GetValueOrDefault("photoIds") is IEnumerable<object> array
            ? array.OfType<string>().ToArray() : null;
        return new(snapshot.Id, fields.GetValueOrDefault("moderationStatus") as string,
            fields.GetValueOrDefault("authorUid") as string, photoIds);
    }
}
