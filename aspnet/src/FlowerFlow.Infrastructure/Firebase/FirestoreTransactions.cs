using Grpc.Core;

namespace FlowerFlow.Infrastructure.Firebase;

/// <summary>Restarts an aborted transaction after SDK retries are exhausted. Never retries an ambiguous commit.</summary>
public static class FirestoreTransactions
{
    // SDK runs RollbackAsync in finally when the callback throws. Complete a callback
    // with no writes this way, releasing its read locks without an empty commit.
    private sealed class ReadOnlyResult<T>(T result) : Exception { public T Result { get; } = result; }
    public abstract class State
    {
        protected bool HasWrites { get; set; }
        public T Complete<T>(T result) => HasWrites ? result : throw new ReadOnlyResult<T>(result);
    }
    public static async Task<T> RunAsync<T>(Func<Task<T>> transaction, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { return await transaction(); }
            catch (ReadOnlyResult<T> completed) { return completed.Result; }
            // ABORTED guarantees no commit. Re-run the entire read/validate/write callback,
            // never just commit stale writes. Other statuses (including UNAVAILABLE) propagate.
            catch (RpcException error) when (error.StatusCode == StatusCode.Aborted && attempt < 2)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100 * (1 << attempt) + Random.Shared.Next(100)), cancellationToken);
            }
        }
    }
}
