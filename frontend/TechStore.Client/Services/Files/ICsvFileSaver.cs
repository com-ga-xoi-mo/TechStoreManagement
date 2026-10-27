namespace TechStore.Client.Services.Files;

public interface ICsvFileSaver
{
    /// <summary>Opens the save dialog with a suggested name; returns null when the user cancels.
    /// When the dialog cannot be shown, writes to the Downloads folder and reports UsedFallbackFolder.</summary>
    Task<CsvSaveResult?> SaveAsync(string suggestedFileName, string content, CancellationToken cancellationToken = default);
}

public sealed record CsvSaveResult(string Path, bool UsedFallbackFolder);
