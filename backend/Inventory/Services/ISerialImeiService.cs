using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Inventory.Services;

public class DuplicateSerialImeiException : Exception
{
    public IReadOnlyList<string> ConflictingSerials { get; }
    public IReadOnlyList<string> ConflictingImeis { get; }

    public DuplicateSerialImeiException(
        IReadOnlyList<string> conflictingSerials,
        IReadOnlyList<string> conflictingImeis,
        string message)
        : base(message)
    {
        ConflictingSerials = conflictingSerials;
        ConflictingImeis = conflictingImeis;
    }
}

public record SerialImeiSearchResult(
    bool IsSuccess,
    PagedResultDto<SerialImeiDto>? Data,
    Dictionary<string, string[]>? ValidationErrors)
{
    public static SerialImeiSearchResult Success(PagedResultDto<SerialImeiDto> data) =>
        new(true, data, null);

    public static SerialImeiSearchResult ValidationFailed(Dictionary<string, string[]> errors) =>
        new(false, null, errors);
}

public record BatchImportSerialImeiResult(
    bool IsSuccess,
    int ImportedCount,
    IReadOnlyList<string>? ConflictingSerials,
    IReadOnlyList<string>? ConflictingImeis,
    Dictionary<string, string[]>? ValidationErrors,
    bool IsConflict,
    string? ConflictReason)
{
    public static BatchImportSerialImeiResult Success(int importedCount) =>
        new(true, importedCount, null, null, null, false, null);

    public static BatchImportSerialImeiResult ValidationFailed(Dictionary<string, string[]> errors) =>
        new(false, 0, null, null, errors, false, null);

    public static BatchImportSerialImeiResult Conflict(
        string reason,
        IReadOnlyList<string>? conflictingSerials = null,
        IReadOnlyList<string>? conflictingImeis = null) =>
        new(false, 0, conflictingSerials, conflictingImeis, null, true, reason);
}

public interface ISerialImeiService
{
    Task<SerialImeiSearchResult> GetSerialsAsync(
        SerialImeiFilterRequest request,
        CancellationToken cancellationToken = default);

    Task<BatchImportSerialImeiResult> BatchImportAsync(
        BatchImportSerialImeiRequest request,
        Guid performedByUserId,
        CancellationToken cancellationToken = default);
}
