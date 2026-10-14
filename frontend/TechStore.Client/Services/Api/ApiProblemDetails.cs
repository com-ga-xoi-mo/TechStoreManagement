using System.Text.Json;

namespace TechStore.Client.Services.Api;

public sealed record ApiProblemDetails(
    string? Type,
    string? Title,
    string? Detail,
    int? Status,
    string? Code,
    string? TraceId,
    IReadOnlyDictionary<string, IReadOnlyList<string>> FieldErrors)
{
    public static ApiProblemDetails? TryParse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var type = GetString(root, "type");
        var title = GetString(root, "title");
        var detail = GetString(root, "detail");
        var status = GetInt32(root, "status");
        var code = GetString(root, "code") ?? GetString(root, "errorCode");
        var traceId = GetString(root, "traceId") ?? GetString(root, "traceIdentifier");
        var errors = ParseFieldErrors(root);
        if (type is null && title is null && detail is null && status is null
            && code is null && traceId is null && errors.Count == 0)
        {
            return null;
        }

        return new ApiProblemDetails(type, title, detail, status, code, traceId, errors);
    }

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? GetInt32(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.TryGetInt32(out var result)
            ? result
            : null;

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ParseFieldErrors(JsonElement root)
    {
        var errors = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty("errors", out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return errors;
        }

        foreach (var property in value.EnumerateObject())
        {
            var messages = property.Value.ValueKind switch
            {
                JsonValueKind.Array => property.Value.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString()!)
                    .ToArray(),
                JsonValueKind.String => [property.Value.GetString()!],
                _ => []
            };
            if (messages.Length > 0)
            {
                errors[property.Name] = Array.AsReadOnly(messages);
            }
        }

        return errors;
    }
}
