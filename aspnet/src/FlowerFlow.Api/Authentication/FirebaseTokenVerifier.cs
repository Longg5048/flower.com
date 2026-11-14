using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;

namespace FlowerFlow.Api.Authentication;

/// <summary>Verifies Firebase ID tokens using server credentials, never the browser API key.</summary>
public sealed class FirebaseTokenVerifier : IDisposable
{
    private readonly FirebaseApp? _app;
    private readonly FirebaseAuth? _auth;

    public FirebaseTokenVerifier(IConfiguration configuration, ILogger<FirebaseTokenVerifier> logger)
    {
        var projectId = configuration["Firebase:ProjectId"] ?? Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID");
        if (string.IsNullOrWhiteSpace(projectId))
        {
            ConfigurationError = "Firebase project ID is not configured.";
            return;
        }

        try
        {
            var serviceAccountJson = Environment.GetEnvironmentVariable("FIREBASE_SERVICE_ACCOUNT_JSON");
            GoogleCredential credential = !string.IsNullOrWhiteSpace(serviceAccountJson)
                ? CredentialFactory.FromJson<ServiceAccountCredential>(serviceAccountJson).ToGoogleCredential()
                : GoogleCredential.GetApplicationDefault();

            _app = FirebaseApp.Create(new AppOptions { Credential = credential, ProjectId = projectId }, "FlowerFlowApi");
            _auth = FirebaseAuth.GetAuth(_app);
        }
        catch (Exception exception)
        {
            ConfigurationError = "Firebase Admin credentials could not be initialized.";
            logger.LogError("Firebase Admin initialization failed ({ExceptionType}).", exception.GetType().Name);
        }
    }

    public bool IsConfigured => _auth is not null;
    public string? ConfigurationError { get; }

    public Task<FirebaseToken> VerifyAsync(string idToken, CancellationToken cancellationToken) =>
        (_auth ?? throw new InvalidOperationException(ConfigurationError ?? "Firebase is not configured."))
        .VerifyIdTokenAsync(idToken, checkRevoked: true, cancellationToken: cancellationToken);

    public void Dispose() => _app?.Delete();
}
