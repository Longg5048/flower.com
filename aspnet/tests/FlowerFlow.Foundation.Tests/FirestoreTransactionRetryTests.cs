using FlowerFlow.Infrastructure.Firebase;
using Grpc.Core;
using Xunit;
namespace FlowerFlow.Foundation.Tests;
public sealed class FirestoreTransactionRetryTests
{
    [Fact]
    public async Task Aborted_transactions_restart_from_callback_and_stop_after_three_rounds()
    {
        var calls = 0;
        var result = await FirestoreTransactions.RunAsync(() => ++calls < 3 ? Task.FromException<int>(new RpcException(new Status(StatusCode.Aborted, "test"))) : Task.FromResult(42), CancellationToken.None);
        Assert.Equal(42, result); Assert.Equal(3, calls);
        calls = 0;
        await Assert.ThrowsAsync<RpcException>(() => FirestoreTransactions.RunAsync<int>(() => { calls++; throw new RpcException(new Status(StatusCode.Aborted, "test")); }, CancellationToken.None));
        Assert.Equal(3, calls);
    }
    [Theory]
    [InlineData(StatusCode.Unavailable)]
    [InlineData(StatusCode.AlreadyExists)]
    [InlineData(StatusCode.PermissionDenied)]
    public async Task Ambiguous_or_permanent_errors_are_not_retried(StatusCode status)
    {
        var calls = 0;
        var error = await Assert.ThrowsAsync<RpcException>(() => FirestoreTransactions.RunAsync<int>(() => { calls++; throw new RpcException(new Status(status, "test")); }, CancellationToken.None));
        Assert.Equal(status, error.StatusCode); Assert.Equal(1, calls);
    }
    [Fact]
    public async Task Cancellation_stops_before_another_transaction_can_start()
    {
        using var cancellation = new CancellationTokenSource(); var calls = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FirestoreTransactions.RunAsync<int>(() =>
        { calls++; cancellation.Cancel(); throw new RpcException(new Status(StatusCode.Aborted, "test")); }, cancellation.Token));
        Assert.Equal(1, calls);
    }
}
