namespace TechStore.Api.Catalog.Services;

public sealed record ProductImageFileInfo(string ContentType, string Extension);

public static class ProductImageFileInspector
{
    public const int MaxBytes = 5_242_880;

    public static ProductImageFileInfo? Inspect(ReadOnlySpan<byte> content, out string? error)
    {
        error = content.Length switch
        {
            0 => "The image file is empty.",
            > MaxBytes => "The image file cannot exceed 5 MB.",
            _ => null
        };
        if (error != null) return null;
        if (content.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF })) return new("image/jpeg", ".jpg");
        if (content.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return new("image/png", ".png");
        if (content.Length >= 12 && content[..4].SequenceEqual("RIFF"u8) && content.Slice(8, 4).SequenceEqual("WEBP"u8))
            return new("image/webp", ".webp");
        error = "Only JPEG, PNG and WebP images are allowed.";
        return null;
    }
}
