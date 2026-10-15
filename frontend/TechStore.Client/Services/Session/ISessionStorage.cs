namespace TechStore.Client.Services.Session;

public interface ISessionStorage
{
    PersistedSessionData? Load();
    void Save(PersistedSessionData data);
    void Clear();
}

public sealed record PersistedSessionData(
    string AccessToken,
    string RefreshToken,
    string Username,
    string FullName,
    string Role
);
