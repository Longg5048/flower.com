using System.Text.Json.Nodes;
using FlowerFlow.Application.Catalog;

namespace FlowerFlow.Infrastructure.Firebase;

public sealed class EmbeddedProductCatalog : IProductCatalogDefaults
{
    private readonly Dictionary<string, JsonObject> products = new(StringComparer.Ordinal);
    public EmbeddedProductCatalog()
    {
        using var source = typeof(EmbeddedProductCatalog).Assembly.GetManifestResourceStream("FlowerFlow.Catalog.json")
            ?? throw new InvalidOperationException("Embedded catalog is missing.");
        foreach (var product in JsonNode.Parse(source)!["flower"]!.AsArray().OfType<JsonObject>())
            products[product["id"]!.GetValue<string>()] = product;
    }
    public JsonObject? Find(string id) => products.TryGetValue(id, out var product) ? (JsonObject)product.DeepClone() : null;
}
