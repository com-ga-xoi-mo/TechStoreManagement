using System.Text;
using Path = System.IO.Path;

namespace TechStore.Client.Services.Files;

public static class CsvFileWriter
{
    public static readonly Encoding Utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    public static string DefaultFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    public static async Task<string> WriteToFolderAsync(
        string directory,
        string fileName,
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();

        Directory.CreateDirectory(directory);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var path = Path.Combine(directory, fileName);
        for (var i = 2; File.Exists(path); i++)
            path = Path.Combine(directory, $"{stem}-{i}{ext}");

        await File.WriteAllTextAsync(path, content, Utf8WithBom, cancellationToken);
        return path;
    }
}
