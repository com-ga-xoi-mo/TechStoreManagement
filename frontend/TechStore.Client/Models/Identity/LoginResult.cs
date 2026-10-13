using System;
using System.Collections.Generic;

namespace TechStore.Client.Models.Identity;

public enum LoginResultStatus
{
    Success,
    WrongCredentials,
    LockedOut,
    Inactive,
    NetworkError,
    UnknownError
}

public sealed record LoginResult(
    LoginResultStatus Status,
    string? ErrorMessage,
    SessionSnapshot? Session);

public sealed record SessionSnapshot(
    string DisplayName,
    IReadOnlySet<string> Capabilities,
    string? Token = null);
