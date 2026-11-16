using System.Text.Json.Nodes;
using FlowerFlow.Application.Accounts;
using Google.Cloud.Firestore;

namespace FlowerFlow.Infrastructure.Firebase;

public sealed class FirestoreLoginPolicyStore(Func<FirestoreDb> database) : ILoginPolicyStore
{
    // Bound memory while avoiding a local burst of lock upgrades for the same account.
    // Firestore remains the authority across adapters, processes and hosts.
    private readonly SemaphoreSlim[] gates = Enumerable.Range(0, 128).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    public async Task<LoginPolicyReply> RunAsync(string key, Func<ILoginPolicyTransaction, Task<LoginPolicyReply>> operation, CancellationToken cancellationToken)
    {
        var gate = gates[(uint)StringComparer.Ordinal.GetHashCode(key) % (uint)gates.Length];
        await gate.WaitAsync(cancellationToken);
        try
        {
            var db = database();
            var reference = db.Collection("loginProtection").Document(key);
            return await FirestoreTransactions.RunAsync(() => db.RunTransactionAsync(async transaction =>
            {
                var adapter = new PolicyTransaction(reference, transaction);
                return adapter.Complete(await operation(adapter));
            }, cancellationToken: cancellationToken), cancellationToken);
        }
        finally { gate.Release(); }
    }
    private sealed class PolicyTransaction(DocumentReference reference, Transaction transaction) : FirestoreTransactions.State, ILoginPolicyTransaction
    {
        public async Task<JsonObject?> ReadAsync(CancellationToken cancellationToken)
        {
            var snapshot = await transaction.GetSnapshotAsync(reference, cancellationToken);
            return snapshot.Exists ? new JsonObject(snapshot.ToDictionary().Select(pair =>
                KeyValuePair.Create(pair.Key, FirestoreProductReader.ConvertValue(pair.Value, true)))) : null;
        }
        public void Set(JsonObject state) { HasWrites = true; transaction.Set(reference, FirestoreSettingsStore.ToFirestore(state)!); }
        public void Delete() { HasWrites = true; transaction.Delete(reference); }
    }
}
