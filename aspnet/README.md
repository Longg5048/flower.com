# FlowerFlow ASP.NET Core API

This is the ASP.NET Core 10 sidecar API for FlowerFlow. The existing Vercel/Node backend, Firebase, and frontend remain in place. The approved SQL Server design in `docs/database/schema.sql` is mapped by EF Core; this project does not create or change a database automatically.

## Project map

- `src/FlowerFlow.Domain`: persistence-independent C# entity types, grouped by identity, catalog, commerce, and operations.
- `src/FlowerFlow.Application`: application/use-case layer, kept independent of ASP.NET and SQL details.
- `src/FlowerFlow.Infrastructure`: EF Core SQL Server provider, `FlowerFlowDbContext` mappings, and migrations. The DbContext is the unit of work for a request: it tracks entity changes and translates LINQ into SQL.
- `src/FlowerFlow.Api`: ASP.NET startup, controllers, HTTP configuration, Firebase bearer authentication, and OpenAPI/Swagger UI.
- `initial-schema-migration.sql`: generated, idempotent SQL for review. It is not applied automatically.

## Request flow

An HTTP request enters an API controller. ASP.NET resolves its dependencies through dependency injection; `AddDbContext` registers `FlowerFlowDbContext` with a scoped lifetime, normally one instance per request. A controller queries a `DbSet` with LINQ. EF Core translates that expression to parameterized SQL, the SQL Server provider sends it to SQL Server, and EF maps returned rows to C# entities. The controller should return response DTOs rather than database entities directly.

## Local setup and secret handling

Install the .NET 10 SDK and SQL Server. The API reads `ConnectionStrings:FlowerFlow` from .NET User Secrets in Development or from the `ConnectionStrings__FlowerFlow` environment variable in other environments. Never place a real connection string in `appsettings*.json` or source control.

From this directory, for local Development:

```powershell
dotnet user-secrets set "ConnectionStrings:FlowerFlow" "Server=localhost;Database=FlowerFlowDev;Trusted_Connection=True;TrustServerCertificate=True" --project .\src\FlowerFlow.Api
dotnet run --project .\src\FlowerFlow.Api
```

For another environment, provide `ConnectionStrings__FlowerFlow` through the hosting environment's secret store. The design-time EF factory uses an intentionally unusable loopback endpoint if the environment variable is absent, so EF can construct the model but cannot silently target LocalDB or a developer database. Supply the environment variable explicitly when running EF commands that need a real database.

In Development, the OpenAPI document is at `/openapi/v1.json` and Swagger UI is at `/swagger`. `/health` checks that the API process is serving; `/health/database` attempts a connection and returns a sanitized 503 if no connection is configured or SQL Server cannot be reached. These endpoints never expose credentials.

## EF Core migration workflow

EF Core and its SQL Server provider are pinned to 10.0.14, compatible with .NET 10. The local tool manifest pins `dotnet-ef` 10.0.14. The initial migration is in `src/FlowerFlow.Infrastructure/Migrations`; its generated script is `initial-schema-migration.sql`.

```powershell
dotnet tool restore
dotnet ef migrations list --project .\src\FlowerFlow.Infrastructure --startup-project .\src\FlowerFlow.Api
dotnet ef migrations script --idempotent --project .\src\FlowerFlow.Infrastructure --startup-project .\src\FlowerFlow.Api --output .\initial-schema-migration.sql
```

Review the generated SQL before applying it to a deliberately selected development database. This foundation does not run `database update`, seed data, or modify Firebase. Existing product read/profile endpoints and Firebase token verification remain available as integration scaffolding; broader business workflows are deferred to later stages.

## Learning notes

- C# classes model the domain; properties map to columns through conventions plus explicit fluent configuration in `FlowerFlowDbContext.OnModelCreating`.
- A primary key identifies one row. A foreign key enforces a relationship in SQL Server. Composite keys are used for join/line tables where the design identifies a row by multiple values.
- `DbContext` is EF Core's session/unit-of-work abstraction, not a global singleton. Scoped lifetime prevents request state from leaking across API calls.
- `Program.cs` is the composition root: it registers services and middleware, then maps routes. Middleware handles each request in order; authentication runs before authorization and controller execution.
- Migrations are versioned schema changes generated from the model. Generating a migration is separate from applying it to a database.
