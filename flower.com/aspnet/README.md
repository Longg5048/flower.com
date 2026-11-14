# ASP.NET Core backend

The ASP.NET Core implementation is being added alongside the production Node.js
backend. The current Node.js routes remain authoritative until the C# APIs pass
behavior-parity and staging checks.

The project targets .NET 10 and uses Firebase Admin and Google Cloud Firestore
packages. Credentials must be supplied through server-side environment
variables; never put a service-account key in this directory or in the frontend.

From `flower.com/aspnet`, run:

```powershell
dotnet test FlowerFlow.Tests\FlowerFlow.Tests.csproj
dotnet run --project FlowerFlow.Api\FlowerFlow.Api.csproj
```

The API host currently serves the existing frontend files and exposes `/healthz`.
Business APIs are not yet routed to this host.
