using System.Buffers.Binary;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace FlowerFlow.Application.Catalog;

public sealed record JpegSize(int Width, int Height);

public static class ImageRules
{
    public const int MaxReviewPhotos = 3;
    public const int MaxPhotoBytes = 120 * 1024;

    // Preserve the legacy marker/dimension check; this is not a full JPEG decoder.
    public static JpegSize? JpegDimensions(byte[] bytes)
    {
        if (bytes.Length < 12 || bytes[0] != 0xff || bytes[1] != 0xd8
            || bytes[^2] != 0xff || bytes[^1] != 0xd9) return null;
        var offset = 2;
        while (offset + 4 < bytes.Length)
        {
            if (bytes[offset++] != 0xff) return null;
            while (offset < bytes.Length && bytes[offset] == 0xff) offset++;
            if (offset >= bytes.Length) throw new InvalidDataException("Truncated JPEG marker.");
            var marker = bytes[offset++];
            if (marker is 0xda or 0xd9) return null;
            if (marker == 0x01 || marker is >= 0xd0 and <= 0xd7) continue;
            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset));
            if (length < 2 || offset + length > bytes.Length) return null;
            if (marker is 0xc0 or 0xc1 or 0xc2)
            {
                if (length < 8) return null;
                return new(BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset + 5)),
                    BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset + 3)));
            }
            offset += length;
        }
        return null;
    }

    public static IReadOnlyList<byte[]>? ValidateReviewPhotos() => Array.Empty<byte[]>();
    public static IReadOnlyList<byte[]>? ValidateReviewPhotos(JsonNode? photos)
    {
        if (photos is not JsonArray values || values.Count > MaxReviewPhotos) return null;
        var result = new List<byte[]>();
        foreach (var value in values)
        {
            var bytes = ValidatePhoto(value);
            if (bytes is null) return null;
            result.Add(bytes);
        }
        return result;
    }

    public static byte[]? ValidatePhoto(JsonNode? photo)
    {
        if (photo is not JsonValue scalar || !scalar.TryGetValue<string>(out var value)
            || value.Length > (int)Math.Ceiling(MaxPhotoBytes / 3d) * 4 + 30
            || !Regex.IsMatch(value, @"\Adata:image/jpeg;base64,[A-Za-z0-9+/]+={0,2}\z")) return null;
        var encoded = value[(value.IndexOf(',') + 1)..];
        byte[] bytes;
        try { bytes = Convert.FromBase64String(encoded); }
        catch (FormatException) { return null; }
        if (bytes.Length > MaxPhotoBytes || Convert.ToBase64String(bytes) != encoded) return null;
        var size = JpegDimensions(bytes);
        return size is { Width: >= 1 and <= 1600, Height: >= 1 and <= 1600 } ? bytes : null;
    }
}
