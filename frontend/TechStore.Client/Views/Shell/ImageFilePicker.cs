using TechStore.Client.Services.Files;
using Windows.Storage.Pickers;

namespace TechStore.Client.Views.Shell;

public sealed class ImageFilePicker : IImageFilePicker
{
    public Window? Host { get; set; }

    public async Task<IReadOnlyList<PickedImageFile>> PickImagesAsync(long maxBytesToRead, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.PicturesLibrary };
        foreach (var extension in new[] { ".jpg", ".jpeg", ".png", ".webp" }) picker.FileTypeFilter.Add(extension);
#if WINDOWS
        if (Host == null) throw new InvalidOperationException("Không mở được hộp chọn file.");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(Host));
#endif
        try
        {
            var files = await picker.PickMultipleFilesAsync();
            List<PickedImageFile> result = [];
            if (files == null) return result;
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                long size;
                byte[]? content = null;
                try
                {
                    size = checked((long)(await file.GetBasicPropertiesAsync()).Size);
                    if (size <= maxBytesToRead)
                    {
                        await using var stream = await file.OpenStreamForReadAsync();
                        using var buffer = new MemoryStream();
                        await stream.CopyToAsync(buffer, cancellationToken);
                        content = buffer.ToArray();
                    }
                }
                catch (NotImplementedException)
                {
                    size = new FileInfo(file.Path).Length;
                    if (size <= maxBytesToRead) content = await File.ReadAllBytesAsync(file.Path, cancellationToken);
                }
                result.Add(new(file.Name, size, content));
            }
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) { throw new InvalidOperationException("Không mở được hộp chọn file.", ex); }
    }
}
