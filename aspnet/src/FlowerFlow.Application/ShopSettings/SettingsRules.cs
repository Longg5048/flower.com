using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FlowerFlow.Application.Catalog;

namespace FlowerFlow.Application.ShopSettings;

public static class SettingsRules
{
    public static JsonObject? Zone(JsonNode? value)
    {
        if (value is not JsonObject fields) return null;
        var id = FlowerPriceRules.Trim(FlowerPriceRules.OrText(fields["id"]));
        var province = FlowerPriceRules.Trim(FlowerPriceRules.OrText(fields["province"]));
        var district = FlowerPriceRules.Trim(FlowerPriceRules.OrText(fields["district"]));
        if (!Regex.IsMatch(id, @"\A[A-Za-z0-9_-]{1,80}\z") || province.Length is < 1 or > 120 || district.Length is < 1 or > 120
            || !Integer(fields["fee"], 0, 1_000_000_000, out var fee)
            || fields["enabled"] is not JsonValue enabled || !enabled.TryGetValue<bool>(out var active)) return null;
        return new() { ["id"] = id, ["province"] = province, ["district"] = district, ["fee"] = fee, ["enabled"] = active };
    }
    public static JsonArray Zones(JsonObject? document) => document?["zones"] is JsonArray zones
        ? new JsonArray(zones.Select(Zone).Where(zone => zone is not null).Cast<JsonNode?>().ToArray()) : [];
    public static bool DuplicateAreas(JsonArray zones)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var culture = CultureInfo.GetCultureInfo("vi-VN");
        return zones.OfType<JsonObject>().Any(zone => !seen.Add(zone["province"]!.GetValue<string>().ToLower(culture)
            + "\0" + zone["district"]!.GetValue<string>().ToLower(culture)));
    }
    public static JsonArray? Tiers(JsonNode? value)
    {
        if (value is not JsonArray values || values.Count is < 1 or > 10) return null;
        var previous = 0L;
        var result = new JsonArray();
        foreach (var item in values)
        {
            if (item is not JsonObject tier || !Integer(tier["maxStems"], previous + 1, 50, out var max)
                || !Integer(tier["fee"], 0, 1_000_000_000, out var fee)) return null;
            result.Add(new JsonObject { ["maxStems"] = max, ["fee"] = fee });
            previous = max;
        }
        return previous == 50 ? result : null;
    }
    public static bool Integer(JsonNode? node, long min, long max, out long value)
    {
        value = 0;
        if (node is null || node.GetValueKind() != System.Text.Json.JsonValueKind.Number) return false;
        var number = FlowerPriceRules.Number(node);
        if (!double.IsFinite(number) || number != Math.Truncate(number) || number < min || number > max) return false;
        value = (long)number;
        return true;
    }
}
