using System.Collections.ObjectModel;

namespace TechStore.Client.Services.Api;

public enum ApiErrorKind
{
    Http,
    Network,
    Timeout,
    InvalidResponse
}

public sealed class ApiException : Exception
{
    public ApiException(
        ApiErrorKind kind,
        string message,
        int? statusCode = null,
        string? title = null,
        string? detail = null,
        string? code = null,
        string? traceId = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? fieldErrors = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
        StatusCode = statusCode;
        Title = title;
        Detail = detail;
        Code = code;
        TraceId = traceId;
        FieldErrors = new ReadOnlyDictionary<string, IReadOnlyList<string>>(
            fieldErrors?.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)Array.AsReadOnly(pair.Value.ToArray()),
                StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase));
    }

    public ApiErrorKind Kind { get; }

    public int? StatusCode { get; }

    public string? Title { get; }

    public string? Detail { get; }

    public string? Code { get; }

    public string? TraceId { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> FieldErrors { get; }

    internal static ApiException Http(int statusCode, ApiProblemDetails? problem) => new(
        ApiErrorKind.Http,
        $"The API request failed with HTTP status {statusCode}.",
        statusCode,
        problem?.Title,
        problem?.Detail,
        problem?.Code,
        problem?.TraceId,
        problem?.FieldErrors);
}
