using System.Text.Json;
using FirebaseAdmin;
using FirebaseAdmin.Auth;
using FlowerFlow.Application.Identity;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;

namespace FlowerFlow.Infrastructure.Firebase;

public sealed record FirebaseConnectionOptions(string? ProjectId, string? ServiceAccountJson,
    bool AllowApplicationDefaultCredentials = false)
{
    public override string ToString() => "FirebaseConnectionOptions { credentials = redacted }";
}

/// <summary>Lazy, server-owned SDK connection. Construction/startup never reads or writes documents.</summary>
public sealed class FirebaseConnection(FirebaseConnectionOptions options) : IDisposable
{
    private readonly object gate = new();
    private FirebaseApp? app;
    private GoogleCredential? credential;
    private GoogleCredential? identityCredential;
    private FirestoreDb? database;
    private bool disposed;

    public FirebaseAuth GetAuth()
    {
        lock (gate)
        {
            // Auth SDK otherwise accepts unsigned emulator tokens when this variable is inherited.
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FIREBASE_AUTH_EMULATOR_HOST")))
                throw new FirebaseConfigurationException();
            return FirebaseAuth.GetAuth(GetApp());
        }
    }

    public FirestoreDb GetDatabase()
    {
        lock (gate)
        {
            var firebase = GetApp();
            // Production only: an inherited emulator environment variable must not redirect live data.
            return database ??= new FirestoreDbBuilder
            {
                ProjectId = firebase.Options.ProjectId,
                GoogleCredential = credential,
                EmulatorDetection = Google.Api.Gax.EmulatorDetection.ProductionOnly
            }.Build();
        }
    }

    public string GetProjectId()
    {
        lock (gate) return GetApp().Options.ProjectId;
    }
    public Task<string> GetIdentityAccessTokenAsync(CancellationToken cancellationToken)
    {
        GoogleCredential scoped;
        lock (gate)
        {
            GetApp();
            scoped = identityCredential ??= credential!.CreateScoped("https://www.googleapis.com/auth/cloud-platform", "https://www.googleapis.com/auth/identitytoolkit");
        }
        return scoped.UnderlyingCredential.GetAccessTokenForRequestAsync(null, cancellationToken);
    }

    private FirebaseApp GetApp()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (app is not null) return app;
        try
        {
            var project = options.ProjectId;
            if (string.IsNullOrWhiteSpace(options.ServiceAccountJson))
            {
                // Preserve ADC support of the original ASP.NET verifier when explicitly enabled by the host.
                if (!options.AllowApplicationDefaultCredentials || string.IsNullOrWhiteSpace(project))
                    throw new FirebaseConfigurationException();
                credential = GoogleCredential.GetApplicationDefault();
            }
            else
            {
                using var json = JsonDocument.Parse(options.ServiceAccountJson);
                project = string.IsNullOrWhiteSpace(project) ? json.RootElement.GetProperty("project_id").GetString() : project;
                if (string.IsNullOrWhiteSpace(project) || json.RootElement.GetProperty("type").GetString() != "service_account")
                    throw new FirebaseConfigurationException();
                credential = CredentialFactory.FromJson<ServiceAccountCredential>(options.ServiceAccountJson).ToGoogleCredential();
            }
            // Multiple test hosts/connections may coexist in the same process.
            return app = FirebaseApp.Create(new AppOptions { ProjectId = project, Credential = credential },
                "FlowerFlow-" + Guid.NewGuid().ToString("N"));
        }
        catch (Exception)
        {
            // Do not preserve an inner exception: JSON/SDK errors may contain secret material.
            throw new FirebaseConfigurationException();
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            app?.Delete();
        }
    }
}
