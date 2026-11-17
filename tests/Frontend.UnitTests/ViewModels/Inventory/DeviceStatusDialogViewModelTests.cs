using FluentAssertions;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Repositories.Inventory;
using TechStore.Client.ViewModels.Inventory;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.Client.UnitTests.ViewModels.Inventory;

public sealed class DeviceStatusDialogViewModelTests
{
    private sealed class FakeSerialDeviceRepository : ISerialDeviceRepository
    {
        public int ReportDefectiveCalls { get; private set; }
        public int RestoreCalls { get; private set; }
        public Guid? LastDeviceId { get; private set; }
        public ReportDefectiveDeviceRequest? LastReportRequest { get; private set; }
        public RestoreDeviceRequest? LastRestoreRequest { get; private set; }
        public SerialDeviceMutationResult? NextResult { get; set; }

        public Task<SerialDeviceMutationResult> ReportDefectiveAsync(Guid deviceId, ReportDefectiveDeviceRequest request, CancellationToken cancellationToken = default)
        {
            ReportDefectiveCalls++;
            LastDeviceId = deviceId;
            LastReportRequest = request;
            return Task.FromResult(NextResult ?? SerialDeviceMutationResult.Success(new SerialImeiDto
            {
                Id = deviceId,
                Status = SerialImeiStatus.Defective
            }));
        }

        public Task<SerialDeviceMutationResult> RestoreAsync(Guid deviceId, RestoreDeviceRequest request, CancellationToken cancellationToken = default)
        {
            RestoreCalls++;
            LastDeviceId = deviceId;
            LastRestoreRequest = request;
            return Task.FromResult(NextResult ?? SerialDeviceMutationResult.Success(new SerialImeiDto
            {
                Id = deviceId,
                Status = SerialImeiStatus.InStock
            }));
        }
    }

    private sealed class FakeInventoryRepository(PagedResultDto<SerialImeiDto> page) : IInventoryRepository
    {
        public int LoadCalls { get; private set; }
        public Task<PagedResultDto<InventoryStockDto>> GetStocksAsync(InventoryStockFilterRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResultDto<SerialImeiDto>> GetSerialsAsync(SerialImeiFilterRequest request, CancellationToken cancellationToken = default)
        {
            LoadCalls++;
            return Task.FromResult(page);
        }
    }

    [Fact]
    public void CanSubmit_ReportDefective_DisabledWhenReasonEmpty_AndEnabledWhenReasonFilled()
    {
        var repo = new FakeSerialDeviceRepository();
        var device = new SerialImeiDto { Id = Guid.NewGuid(), Status = SerialImeiStatus.InStock, Imei = "35111" };
        var vm = new DeviceStatusDialogViewModel(repo, device, DeviceStatusMode.ReportDefective);

        vm.CanSubmit.Should().BeFalse();
        vm.ConfirmCommand.CanExecute(null).Should().BeFalse();

        vm.Reason = "   ";
        vm.CanSubmit.Should().BeFalse();
        vm.ConfirmCommand.CanExecute(null).Should().BeFalse();

        vm.Reason = "Hư hỏng";
        vm.CanSubmit.Should().BeTrue();
        vm.ConfirmCommand.CanExecute(null).Should().BeTrue();

        vm.Reason = "";
        vm.CanSubmit.Should().BeFalse();
        vm.ConfirmCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void CanSubmit_Restore_EnabledEvenWhenReasonEmpty()
    {
        var repo = new FakeSerialDeviceRepository();
        var device = new SerialImeiDto { Id = Guid.NewGuid(), Status = SerialImeiStatus.Defective, Imei = "35111" };
        var vm = new DeviceStatusDialogViewModel(repo, device, DeviceStatusMode.Restore);

        vm.CanSubmit.Should().BeTrue();
        vm.ConfirmCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmAsync_ReportDefective_WhenSuccess_FiresDeviceStatusChanged_AndRequestsClose()
    {
        var repo = new FakeSerialDeviceRepository();
        var deviceId = Guid.NewGuid();
        var device = new SerialImeiDto { Id = deviceId, Status = SerialImeiStatus.InStock, Imei = "35111", ProductName = "iPhone" };
        var vm = new DeviceStatusDialogViewModel(repo, device, DeviceStatusMode.ReportDefective)
        {
            Reason = "Mất",
            Note = "Khách làm rơi"
        };

        SerialDeviceItem? changedDevice = null;
        var closed = false;
        vm.DeviceStatusChanged += d => changedDevice = d;
        vm.RequestClose += (_, _) => closed = true;

        await vm.ConfirmAsync();

        repo.ReportDefectiveCalls.Should().Be(1);
        repo.LastDeviceId.Should().Be(deviceId);
        repo.LastReportRequest.Should().NotBeNull();
        repo.LastReportRequest!.Reason.Should().Be("Mất");
        repo.LastReportRequest.Note.Should().Be("Khách làm rơi");

        changedDevice.Should().NotBeNull();
        changedDevice!.Status.Should().Be(SerialImeiStatus.Defective);
        closed.Should().BeTrue();
        vm.HasError.Should().BeFalse();
    }

    [Fact]
    public async Task ConfirmAsync_Restore_WhenSuccess_FiresDeviceStatusChanged_AndRequestsClose()
    {
        var repo = new FakeSerialDeviceRepository();
        var deviceId = Guid.NewGuid();
        var device = new SerialImeiDto { Id = deviceId, Status = SerialImeiStatus.Defective, Imei = "35111", ProductName = "iPhone" };
        var vm = new DeviceStatusDialogViewModel(repo, device, DeviceStatusMode.Restore)
        {
            Note = "Đã thay linh kiện"
        };

        SerialDeviceItem? changedDevice = null;
        var closed = false;
        vm.DeviceStatusChanged += d => changedDevice = d;
        vm.RequestClose += (_, _) => closed = true;

        await vm.ConfirmAsync();

        repo.RestoreCalls.Should().Be(1);
        repo.LastDeviceId.Should().Be(deviceId);
        repo.LastRestoreRequest.Should().NotBeNull();
        repo.LastRestoreRequest!.Note.Should().Be("Đã thay linh kiện");

        changedDevice.Should().NotBeNull();
        changedDevice!.Status.Should().Be(SerialImeiStatus.InStock);
        closed.Should().BeTrue();
        vm.HasError.Should().BeFalse();
    }

    [Theory]
    [InlineData(SerialDeviceFailureKind.BadRequest, null, "Dữ liệu không hợp lệ.")]
    [InlineData(SerialDeviceFailureKind.Unauthorized, null, "Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại.")]
    [InlineData(SerialDeviceFailureKind.Forbidden, null, "Bạn không có quyền thực hiện thao tác này.")]
    [InlineData(SerialDeviceFailureKind.NotFound, "Máy không tồn tại", "Máy không tồn tại")]
    [InlineData(SerialDeviceFailureKind.Conflict, "Thiết bị không ở trạng thái Trong kho.", "Thiết bị không ở trạng thái Trong kho.")]
    [InlineData(SerialDeviceFailureKind.Network, null, "Không thể kết nối máy chủ. Vui lòng thử lại.")]
    public async Task ConfirmAsync_WhenError_RetainsFormData_DoesNotClose_AndMapsErrorMessage(
        SerialDeviceFailureKind failureKind,
        string? failureMsg,
        string expectedError)
    {
        var repo = new FakeSerialDeviceRepository
        {
            NextResult = SerialDeviceMutationResult.Failed(failureKind, failureMsg)
        };
        var device = new SerialImeiDto { Id = Guid.NewGuid(), Status = SerialImeiStatus.InStock };
        var vm = new DeviceStatusDialogViewModel(repo, device, DeviceStatusMode.ReportDefective)
        {
            Reason = "Hư hỏng",
            Note = "Bể màn hình"
        };

        var closed = false;
        vm.RequestClose += (_, _) => closed = true;

        await vm.ConfirmAsync();

        closed.Should().BeFalse();
        vm.HasError.Should().BeTrue();
        vm.ErrorMessage.Should().Be(expectedError);
        // Form data retained
        vm.Reason.Should().Be("Hư hỏng");
        vm.Note.Should().Be("Bể màn hình");
    }

    [Fact]
    public async Task DeviceStatusChanged_ReloadsSerialImeiViewModelCurrentPageExactlyOnce()
    {
        var page = new PagedResultDto<SerialImeiDto>
        {
            Items = [new SerialImeiDto { Id = Guid.NewGuid(), Status = SerialImeiStatus.InStock }],
            Page = 2,
            PageSize = 20,
            TotalCount = 25,
            TotalPages = 2
        };
        var inventoryRepo = new FakeInventoryRepository(page);
        var serialVm = new SerialImeiViewModel(inventoryRepo) { Page = 2 };

        var deviceRepo = new FakeSerialDeviceRepository();
        var device = new SerialImeiDto { Id = Guid.NewGuid(), Status = SerialImeiStatus.InStock };
        var dialogVm = new DeviceStatusDialogViewModel(deviceRepo, device, DeviceStatusMode.ReportDefective)
        {
            Reason = "Hư hỏng"
        };

        // Wire subscription as done in the Page:
        dialogVm.DeviceStatusChanged += async _ =>
        {
            await serialVm.LoadCommand.ExecuteAsync(null);
        };

        await dialogVm.ConfirmAsync();

        inventoryRepo.LoadCalls.Should().Be(1);
        serialVm.Page.Should().Be(2);
    }

    [Fact]
    public void CancelCommand_ClosesDialogWithoutCallingRepository()
    {
        var repo = new FakeSerialDeviceRepository();
        var device = new SerialImeiDto { Id = Guid.NewGuid(), Status = SerialImeiStatus.InStock };
        var vm = new DeviceStatusDialogViewModel(repo, device, DeviceStatusMode.ReportDefective);

        var closed = false;
        vm.RequestClose += (_, _) => closed = true;

        vm.CancelCommand.Execute(null);

        closed.Should().BeTrue();
        repo.ReportDefectiveCalls.Should().Be(0);
        repo.RestoreCalls.Should().Be(0);
    }

    [Fact]
    public void UsePresetReasonCommand_FillsReasonAndEnablesConfirm()
    {
        // Arrange
        var vm = new DeviceStatusDialogViewModel(new PresetOnlyRepository(),
            new SerialImeiDto { Id = Guid.NewGuid(), Imei = "356789012345678", Status = SerialImeiStatus.InStock },
            DeviceStatusMode.ReportDefective);

        // Act
        vm.UsePresetReasonCommand.Execute("Mất");

        // Assert
        vm.Reason.Should().Be("Mất");
        vm.CanSubmit.Should().BeTrue();
        vm.ConfirmCommand.CanExecute(null).Should().BeTrue();
    }

    private sealed class PresetOnlyRepository : ISerialDeviceRepository
    {
        public Task<SerialDeviceMutationResult> ReportDefectiveAsync(Guid deviceId, ReportDefectiveDeviceRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SerialDeviceMutationResult> RestoreAsync(Guid deviceId, RestoreDeviceRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
