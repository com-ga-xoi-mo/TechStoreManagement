using TechStore.Client.Services.Files;
using Windows.Storage;
using Windows.Storage.Pickers;
using Path = System.IO.Path;

namespace TechStore.Client.Views.Shell;

public sealed class CsvFileSaver : ICsvFileSaver
{
    public Window? Host { get; set; }

    public async Task<CsvSaveResult?> SaveAsync(
        string suggestedFileName,
        string content,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.Downloads,
            SuggestedFileName = OperatingSystem.IsLinux()
                ? suggestedFileName
                : Path.GetFileNameWithoutExtension(suggestedFileName),
            DefaultFileExtension = ".csv"
        };
        picker.FileTypeChoices.Add("CSV (UTF-8)", new List<string> { ".csv" });
#if WINDOWS
        if (Host == null) throw new InvalidOperationException("Không mở được hộp lưu file.");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(Host));
#endif
        StorageFile? file;
        try
        {
            if (OperatingSystem.IsLinux() && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS")))
                throw new InvalidOperationException("Không mở được hộp lưu file.");
            file = await picker.PickSaveFileAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var path = await CsvFileWriter.WriteToFolderAsync(
                CsvFileWriter.DefaultFolder,
                suggestedFileName,
                content,
                cancellationToken);
            return new CsvSaveResult(path, UsedFallbackFolder: true);
        }

        if (file == null) return null;

        cancellationToken.ThrowIfCancellationRequested();
        var bytes = CsvFileWriter.Utf8WithBom.GetPreamble()
            .Concat(CsvFileWriter.Utf8WithBom.GetBytes(content))
            .ToArray();
        try
        {
            await FileIO.WriteBytesAsync(file, bytes);
        }
        catch (NotImplementedException)
        {
            await File.WriteAllBytesAsync(file.Path, bytes, cancellationToken);
        }

        return new CsvSaveResult(string.IsNullOrEmpty(file.Path) ? file.Name : file.Path, false);
    }
}
