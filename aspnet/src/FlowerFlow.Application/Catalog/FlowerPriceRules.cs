using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FlowerFlow.Application.Identity;

namespace FlowerFlow.Application.Catalog;

public static class FlowerPriceRules
{
    private static readonly char[] JsWhitespace = "\u0009\u000a\u000b\u000c\u000d\u0020\u00a0\u1680\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200a\u2028\u2029\u202f\u205f\u3000\ufeff".ToCharArray();
    public const long MinPrice = 1000;
    public const long MaxPrice = 1_000_000_000;
    public static string Trim(string value) => value.Trim(JsWhitespace);
    public static bool ValidId(string? value) => value is { Length: > 0 and <= 128 }
        && value == value.Trim(JsWhitespace)
        && !Regex.IsMatch(value, @"[/\u0000-\u001f\u007f]");

    public static bool ValidPrice(JsonNode? value, out long price)
    {
        price = 0;
        if (value is not JsonValue scalar || scalar.GetValueKind() != System.Text.Json.JsonValueKind.Number)
            return false;
        var number = Number(value);
        if (!double.IsFinite(number) || number != Math.Truncate(number) || number < MinPrice || number > MaxPrice)
            return false;
        price = (long)number;
        return true;
    }

    public static bool Truthy(JsonNode? value) => value is not null && value.ToJsonString() is not ("false" or "\"\"")
        && (value.GetValueKind() != System.Text.Json.JsonValueKind.Number || Number(value) != 0);
    public static string Text(JsonNode? value) => value switch
    {
        null => "null",
        JsonValue scalar when scalar.TryGetValue<string>(out var text) => text,
        JsonArray array => string.Join(",", array.Select(item => item is null ? "" : Text(item))),
        JsonObject => "[object Object]",
        _ => value.ToJsonString()
    };
    public static string OrText(JsonNode? value, string fallback = "") => Truthy(value) ? Text(value) : fallback;
    public static double Number(JsonNode? value)
    {
        if (value is null) return 0;
        if (value is JsonValue scalar)
        {
            if (scalar.TryGetValue<double>(out var number)) return number;
            if (scalar.TryGetValue<bool>(out var boolean)) return boolean ? 1 : 0;
        }
        if (value is JsonObject) return double.NaN;
        var text = Text(value).Trim().Trim('\uFEFF');
        if (text.Length == 0) return 0;
        try
        {
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return Convert.ToInt64(text[2..], 16);
            if (text.StartsWith("0b", StringComparison.OrdinalIgnoreCase)) return Convert.ToInt64(text[2..], 2);
            if (text.StartsWith("0o", StringComparison.OrdinalIgnoreCase)) return Convert.ToInt64(text[2..], 8);
        }
        catch (Exception) { return double.NaN; }
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : double.NaN;
    }
    public static double ToVndPrice(JsonNode? value)
    {
        var price = Number(value);
        if (!double.IsFinite(price) || price < 0) return 0;
        return price is > 0 and < 1000 ? Math.Floor(price * 25949.123093 / 1000 + 0.5) * 1000 : Math.Floor(price + 0.5);
    }
    public static string TimestampText(JsonNode? value) => value is JsonValue scalar && scalar.TryGetValue<string>(out var text) ? text : "";
    public static string Email(RequestIdentity actor) => Cut(actor.Claims.TryGetValue("email", out var email)
        ? OrText(System.Text.Json.JsonSerializer.SerializeToNode(email)) : "", 320);
    public static string Cut(string value, int length) => value[..Math.Min(value.Length, length)];
    public static JsonObject Serialize(string id, JsonObject fields) => new()
    {
        ["flowerId"] = id, ["name"] = OrText(fields["title"]), ["image"] = OrText(fields["image"]),
        ["unit"] = OrText(fields["unit"], "cành"), ["price"] = ToVndPrice(fields["price"]),
        ["priceUpdatedAt"] = TimestampText(fields["priceChangedAt"]) is { Length: > 0 } changed ? changed : OrText(fields["priceUpdatedAt"]),
        ["updatedBy"] = OrText(fields["priceUpdatedByEmail"], OrText(fields["priceUpdatedBy"]))
    };
}
