using FlowerFlow.Application.Orders;
using FlowerFlow.Domain.Models.Compatibility;
using Xunit;
namespace FlowerFlow.Foundation.Tests;
public sealed class ArchitectureTests
{
    [Fact]
    public void Application_and_Domain_do_not_depend_on_HTTP_database_or_provider_assemblies()
    {
        foreach (var assembly in new[] { typeof(CheckoutService).Assembly, typeof(DocumentTimestamp).Assembly })
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), reference => new[] { "FlowerFlow.Infrastructure", "FlowerFlow.Api", "Microsoft.AspNetCore", "Google.", "FirebaseAdmin", "Microsoft.EntityFrameworkCore" }.Any(prefix => reference.Name!.StartsWith(prefix, StringComparison.Ordinal)));
        Assert.DoesNotContain(typeof(DocumentTimestamp).Assembly.GetReferencedAssemblies(), reference => reference.Name == "FlowerFlow.Application");
    }
}
