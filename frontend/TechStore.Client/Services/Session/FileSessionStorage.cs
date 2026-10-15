using System.Text.Json;

namespace TechStore.Client.Services.Session;

public sealed class FileSessionStorage : ISessionStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _filePath;

    public FileSessionStorage(string? customPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            _filePath = customPath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var folder = System.IO.Path.Combine(appData, "TechStore");
            Directory.CreateDirectory(folder);
            _filePath = System.IO.Path.Combine(folder, "session.json");
        }
    }

    public PersistedSessionData? Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return null;
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<PersistedSessionData>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public void Save(PersistedSessionData data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data, JsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // Ignore write failures in restricted environments
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }
        catch
        {
            // Ignore delete failures
        }
    }
}
