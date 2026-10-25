namespace TechStore.Client.Services.Files;

public interface IImageFilePicker
{
    /// <summary>Opens the multi-select JPG/JPEG/PNG/WebP picker; returns empty on cancel. Files over the limit have null Content.</summary>
    Task<IReadOnlyList<PickedImageFile>> PickImagesAsync(long maxBytesToRead, CancellationToken cancellationToken = default);
}

public sealed record PickedImageFile(string FileName, long SizeBytes, byte[]? Content);
