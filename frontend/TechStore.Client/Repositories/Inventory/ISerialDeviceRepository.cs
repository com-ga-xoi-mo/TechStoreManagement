using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.Repositories.Inventory;

public enum SerialDeviceFailureKind
{
    None,
    BadRequest,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    Network
}

public sealed record SerialDeviceMutationResult(
    bool Succeeded,
    SerialImeiDto? Data = null,
    SerialDeviceFailureKind Failure = SerialDeviceFailureKind.None,
    string? Message = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? FieldErrors = null)
{
    public static SerialDeviceMutationResult Success(SerialImeiDto data) =>
        new(true, data);

    public static SerialDeviceMutationResult Failed(
        SerialDeviceFailureKind failure,
        string? message = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? fieldErrors = null) =>
        new(false, null, failure, message, fieldErrors);
}

public interface ISerialDeviceRepository
{
    Task<SerialDeviceMutationResult> ReportDefectiveAsync(
        Guid deviceId,
        ReportDefectiveDeviceRequest request,
        CancellationToken cancellationToken = default);

    Task<SerialDeviceMutationResult> RestoreAsync(
        Guid deviceId,
        RestoreDeviceRequest request,
        CancellationToken cancellationToken = default);
}
