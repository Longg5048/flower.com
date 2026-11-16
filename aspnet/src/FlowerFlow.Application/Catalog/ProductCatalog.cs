using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace FlowerFlow.Application.Catalog;

public sealed record ProductDocument(string Id, JsonObject Fields);
public interface IProductReader
{
    void EnsureConfigured();
    Task<IReadOnlyList<ProductDocument>> ReadAllAsync(CancellationToken cancellationToken);
}
public interface IProductCatalogDefaults { JsonObject? Find(string id); }

public sealed class ProductSerializer(IProductCatalogDefaults defaults)
{
    public JsonObject Serialize(ProductDocument document)
    {
        var product = (JsonObject)document.Fields.DeepClone();
        product["id"] ??= JsonValue.Create(document.Id);
        var original = defaults.Find(Text(product["id"]));
        if (original is null) return product;
        if (product["title"] is JsonValue title && title.TryGetValue<string>(out var text) && text.Contains('?'))
            product["title"] = original["title"]?.DeepClone();
        if (IsLegacyImage(product["image"]))
        {
            product["image"] = original["image"]?.DeepClone();
            if (product["images"] is JsonArray images)
                product["images"] = new JsonArray(images.Where(image => !IsLegacyImage(image)).Select(image => image?.DeepClone()).ToArray());
        }
        return product;
    }
    public static bool IsLegacyImage(JsonNode? value)
    {
        var image = value is null || value.ToJsonString() is "false" or "0" ? "" : Text(value).Trim();
        if (image.Length == 0 || !Uri.TryCreate(new Uri("https://flowerflow.invalid"), image, out var uri)) return false;
        return Regex.IsMatch(uri.AbsolutePath, @"/img/p/GEN/lgwt/[0-9]+\.jpg$", RegexOptions.IgnoreCase)
            || Regex.IsMatch(uri.AbsolutePath, @"\A/images/flower-0[1-6]\.webp\z", RegexOptions.IgnoreCase);
    }
    private static string Text(JsonNode? value) => value switch
    {
        null => "null", JsonValue scalar when scalar.TryGetValue<string>(out var text) => text,
        JsonObject => "[object Object]", JsonArray array => string.Join(",", array.Select(item => item is null ? "" : Text(item))),
        _ => value.ToJsonString()
    };
}
