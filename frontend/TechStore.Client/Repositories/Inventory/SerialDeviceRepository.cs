using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.Repositories.Inventory;

public class SerialDeviceRepository(IApiClient api) : ISerialDeviceRepository
{
    public async Task<SerialDeviceMutationResult> ReportDefectiveAsync(
        Guid deviceId,
        ReportDefectiveDeviceRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var data = await api.PostAsync<ReportDefectiveDeviceRequest, SerialImeiDto>(
                $"api/inventory/serials/{deviceId:D}/defective",
                request,
                cancellationToken);
            return SerialDeviceMutationResult.Success(data);
        }
        catch (ApiException ex)
        {
            return SerialDeviceMutationResult.Failed(MapFailureKind(ex), ex.Detail, ex.FieldErrors);
        }
        catch (Exception)
        {
            return SerialDeviceMutationResult.Failed(SerialDeviceFailureKind.Network);
        }
    }

    public async Task<SerialDeviceMutationResult> RestoreAsync(
        Guid deviceId,
        RestoreDeviceRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var data = await api.PostAsync<RestoreDeviceRequest, SerialImeiDto>(
                $"api/inventory/serials/{deviceId:D}/restore",
                request,
                cancellationToken);
            return SerialDeviceMutationResult.Success(data);
        }
        catch (ApiException ex)
        {
            return SerialDeviceMutationResult.Failed(MapFailureKind(ex), ex.Detail, ex.FieldErrors);
        }
        catch (Exception)
        {
            return SerialDeviceMutationResult.Failed(SerialDeviceFailureKind.Network);
        }
    }

    private static SerialDeviceFailureKind MapFailureKind(ApiException ex) =>
        ex.StatusCode switch
        {
            400 => SerialDeviceFailureKind.BadRequest,
            401 => SerialDeviceFailureKind.Unauthorized,
            403 => SerialDeviceFailureKind.Forbidden,
            404 => SerialDeviceFailureKind.NotFound,
            409 => SerialDeviceFailureKind.Conflict,
            _ => SerialDeviceFailureKind.Network
        };
}
