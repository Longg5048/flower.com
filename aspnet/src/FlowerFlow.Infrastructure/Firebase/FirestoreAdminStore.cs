using System.Text.Json.Nodes;
using FlowerFlow.Application.Admin;
using FlowerFlow.Application.Catalog;
using FlowerFlow.Application.Identity;
using Google.Cloud.Firestore;

namespace FlowerFlow.Infrastructure.Firebase;

public sealed class FirestoreAdminStore(Func<FirestoreDb> database) : IAdminStore
{
    public void EnsureConfigured() => _ = database();
    private static bool IsAdmin(AuthUser user) => user.Claims.TryGetValue("admin", out var admin) && admin is true;
    private static Dictionary<string, object> RoleFields(AuthUser user) => new()
    {
        ["email"] = user.Email, ["role"] = user.Claims.TryGetValue("role", out var role) && role is "owner" ? "owner" : "admin",
        ["updatedAt"] = FieldValue.ServerTimestamp
    };
    public async Task SyncRoleAsync(AuthUser user, bool admin, CancellationToken cancellationToken)
    {
        var reference = database().Collection("adminRoles").Document(user.Uid);
        if (admin) await reference.SetAsync(RoleFields(user), SetOptions.MergeAll, cancellationToken);
        else await reference.DeleteAsync(cancellationToken: cancellationToken);
    }
    public async Task SyncRolesAsync(IReadOnlyList<AuthUser> users, CancellationToken cancellationToken)
    {
        if (users.Count == 0) return;
        var db = database();
        var existing = await db.GetAllSnapshotsAsync(users.Select(user => db.Collection("adminRoles").Document(user.Uid)), cancellationToken);
        var batch = db.StartBatch(); var writes = 0;
        for (var index = 0; index < users.Count; index++)
        {
            if (IsAdmin(users[index]) && !existing[index].Exists) { batch.Set(existing[index].Reference, RoleFields(users[index])); writes++; }
            else if (!IsAdmin(users[index]) && existing[index].Exists) { batch.Delete(existing[index].Reference); writes++; }
        }
        if (writes > 0) await batch.CommitAsync(cancellationToken);
    }
    public async Task RecordAuditAsync(RequestIdentity actor, string action, string resourceType, string resourceId, JsonObject details, CancellationToken cancellationToken)
    {
        await database().Collection("adminAudit").Document().CreateAsync(new Dictionary<string, object?>
        {
            ["actorUid"] = FlowerPriceRules.Cut(actor.Uid, 128), ["actorEmail"] = FlowerPriceRules.Email(actor),
            ["action"] = FlowerPriceRules.Cut(action, 80), ["resourceType"] = FlowerPriceRules.Cut(resourceType, 80),
            ["resourceId"] = FlowerPriceRules.Cut(resourceId, 128), ["details"] = FirestoreSettingsStore.ToFirestore(details), ["createdAt"] = FieldValue.ServerTimestamp
        }, cancellationToken);
    }
    public async Task<IReadOnlyList<AdminAuditDocument>> ReadAuditAsync(CancellationToken cancellationToken)
    {
        var snapshot = await database().Collection("adminAudit").OrderByDescending("createdAt").Limit(100).GetSnapshotAsync(cancellationToken);
        return snapshot.Documents.Select(document =>
        {
            var fields = document.ToDictionary();
            return new AdminAuditDocument(document.Id, new JsonObject(fields.Select(pair =>
                KeyValuePair.Create(pair.Key, FirestoreProductReader.ConvertValue(pair.Value, true)))), fields.GetValueOrDefault("createdAt") is Timestamp);
        }).ToArray();
    }
}
