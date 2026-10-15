namespace TechStore.Client.Services.Session;

public sealed record SessionSnapshot(long Generation, SessionStatus Status, string? AccessToken, string? RefreshToken = null, string? Username = null, string? FullName = null, string? Role = null);

public sealed record SessionChangedEventArgs(long Generation, SessionStatus Status);
