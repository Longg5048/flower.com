using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FlowerFlow.Infrastructure.Persistence;

/// <summary>Lets EF migration commands build the model without connecting to a live database.</summary>
public sealed class FlowerFlowDbContextFactory : IDesignTimeDbContextFactory<FlowerFlowDbContext>
{
    public FlowerFlowDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__FlowerFlow")
            // A deliberately unusable endpoint lets EF build migrations without making
            // a developer's LocalDB or another real database the implicit target.
            ?? "Server=127.0.0.1,1;Database=FlowerFlowDesignOnly;Integrated Security=True;Encrypt=False;Connect Timeout=1";
        var options = new DbContextOptionsBuilder<FlowerFlowDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new FlowerFlowDbContext(options);
    }
}
