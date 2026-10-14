namespace TechStore.Client.Services.Session;

public sealed record SessionSnapshot(long Generation, SessionStatus Status, string? AccessToken);

public sealed record SessionChangedEventArgs(long Generation, SessionStatus Status);
