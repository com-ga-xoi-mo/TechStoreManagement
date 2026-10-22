using TechStore.Client.Services.Files;

namespace TechStore.Client.ViewModels.Catalog;

public sealed record PickFilterResult(IReadOnlyList<PickedImageFile> Accepted, IReadOnlyList<string> Messages);

public static class ProductImageRules
{
    public const int MaxImages = 20;
    public const int MaxBytes = 5_242_880;
    public static bool IsAllowedExtension(string fileName) => System.IO.Path.GetExtension(fileName).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".webp";
    public static string ContentTypeFor(string fileName) => System.IO.Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".webp" => "image/webp",
        _ => throw new ArgumentException("Unsupported image extension.", nameof(fileName))
    };
    public static int CoverIndex<T>(IReadOnlyList<T> items, Func<T, bool> isShared)
    {
        for (var i = 0; i < items.Count; i++) if (isShared(items[i])) return i;
        return items.Count == 0 ? -1 : 0;
    }
    public static PickFilterResult FilterPicked(IReadOnlyList<PickedImageFile> picked, int existingCount)
    {
        List<PickedImageFile> accepted = []; List<string> messages = [];
        var remaining = Math.Max(0, MaxImages - existingCount); var dropped = false;
        foreach (var file in picked)
        {
            if (!IsAllowedExtension(file.FileName)) messages.Add($"{file.FileName}: chỉ nhận JPG, PNG, WebP");
            else if (file.SizeBytes > MaxBytes || file.Content == null || file.Content.Length > MaxBytes) messages.Add($"{file.FileName} lớn hơn 5 MB");
            else if (accepted.Count >= remaining) dropped = true;
            else accepted.Add(file);
        }
        if (dropped) messages.Add(remaining == 0 ? "Đã đủ 20 ảnh" : $"Chỉ thêm được {remaining} ảnh nữa (tối đa 20)");
        return new(accepted, messages);
    }
}
