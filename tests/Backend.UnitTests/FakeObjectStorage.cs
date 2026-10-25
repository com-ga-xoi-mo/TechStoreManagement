using TechStore.Api.Common.Storage;

namespace TechStore.UnitTests;

public sealed class FakeObjectStorage : IObjectStorage
{
    public Dictionary<string, byte[]> Objects { get; } = [];
    public List<string> DeletedKeys { get; } = [];
    public bool ThrowOnPut { get; set; }
    public int PutCount { get; private set; }

    public Task<string> PutAsync(string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken = default)
    {
        PutCount++;
        if (ThrowOnPut) throw new ObjectStorageUnavailableException("Storage unavailable in test.");
        Objects[key] = content.ToArray();
        return Task.FromResult("https://img.test/" + key);
    }

    public Task<bool> TryDeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        DeletedKeys.Add(key);
        Objects.Remove(key);
        return Task.FromResult(true);
    }
}
