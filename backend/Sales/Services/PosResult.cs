namespace TechStore.Api.Sales.Services;

public enum PosResultKind
{
    Success,
    NotFound,
    ValidationFailed,
    Conflict,
    Unavailable
}

// Outcome of a POS service call; the controller maps Kind to 200/201, 404, 400 and 409.
public record PosResult<T>(
    PosResultKind Kind,
    T? Data,
    Dictionary<string, string[]>? ValidationErrors,
    string? ConflictReason)
{
    public bool IsSuccess => Kind == PosResultKind.Success;

    public static PosResult<T> Success(T data) => new(PosResultKind.Success, data, null, null);

    public static PosResult<T> NotFound() => new(PosResultKind.NotFound, default, null, null);

    public static PosResult<T> ValidationFailed(Dictionary<string, string[]> errors) =>
        new(PosResultKind.ValidationFailed, default, errors, null);

    public static PosResult<T> ValidationFailed(string field, string message) =>
        ValidationFailed(new Dictionary<string, string[]> { [field] = [message] });

    public static PosResult<T> Conflict(string reason) => new(PosResultKind.Conflict, default, null, reason);

    /// <summary>A needed setting or integration is not configured (HTTP 503).</summary>
    public static PosResult<T> Unavailable(string reason) => new(PosResultKind.Unavailable, default, null, reason);
}
