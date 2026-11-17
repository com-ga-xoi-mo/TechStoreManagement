using FluentAssertions;
using TechStore.Client.Models.Inventory;
using TechStore.Client.Repositories.Inventory;
using TechStore.Client.ViewModels.Inventory;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.Client.UnitTests.ViewModels.Inventory;

public sealed class SupplierViewModelTests
{
    private sealed class FakeClientSupplierRepository : ISupplierRepository
    {
        public List<SupplierItem> Items { get; } = [];
        public int GetSuppliersCallCount { get; private set; }
        public SupplierFilterRequest? LastFilterRequest { get; private set; }
        public SupplierSearchResult? CustomSearchResult { get; set; }
        public SupplierMutationResult? CustomMutationResult { get; set; }

        public Task<SupplierSearchResult> GetSuppliersAsync(SupplierFilterRequest request, CancellationToken cancellationToken = default)
        {
            GetSuppliersCallCount++;
            LastFilterRequest = request;

            if (CustomSearchResult != null)
            {
                return Task.FromResult(CustomSearchResult);
            }

            var query = Items.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                query = query.Where(i => i.Name.Contains(request.Search, StringComparison.OrdinalIgnoreCase));
            }

            if (request.IsActive.HasValue)
            {
                query = query.Where(i => i.IsActive == request.IsActive.Value);
            }

            var list = query.ToList();
            var page = request.Page.GetValueOrDefault(1);
            var pageSize = request.PageSize.GetValueOrDefault(20);
            var totalCount = list.Count;
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            var pagedItems = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var result = new SupplierSearchResult(
                true,
                pagedItems,
                page,
                pageSize,
                totalCount,
                totalPages);
            return Task.FromResult(result);
        }

        public Task<SupplierDetailResult> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var item = Items.FirstOrDefault(i => i.Id == id);
            return Task.FromResult(item != null
                ? new SupplierDetailResult(true, item)
                : SupplierDetailResult.Failed(SupplierFailureKind.NotFound));
        }

        public Task<SupplierMutationResult> CreateAsync(SaveSupplierRequest request, CancellationToken cancellationToken = default)
        {
            if (CustomMutationResult != null) return Task.FromResult(CustomMutationResult);

            var item = new SupplierItem(
                Guid.NewGuid(),
                request.Name ?? string.Empty,
                request.ContactName,
                request.Phone ?? string.Empty,
                request.Email,
                request.Address,
                true,
                0,
                DateTime.UtcNow,
                null);
            Items.Add(item);
            return Task.FromResult(new SupplierMutationResult(true, item));
        }

        public Task<SupplierMutationResult> UpdateAsync(Guid id, SaveSupplierRequest request, CancellationToken cancellationToken = default)
        {
            if (CustomMutationResult != null) return Task.FromResult(CustomMutationResult);

            var existing = Items.FirstOrDefault(i => i.Id == id);
            if (existing == null) return Task.FromResult(SupplierMutationResult.Failed(SupplierFailureKind.NotFound));

            Items.Remove(existing);
            var updated = existing with
            {
                Name = request.Name ?? existing.Name,
                ContactName = request.ContactName,
                Phone = request.Phone ?? existing.Phone,
                Email = request.Email,
                Address = request.Address,
                UpdatedAt = DateTime.UtcNow
            };
            Items.Add(updated);
            return Task.FromResult(new SupplierMutationResult(true, updated));
        }

        public Task<SupplierMutationResult> SetStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
        {
            if (CustomMutationResult != null) return Task.FromResult(CustomMutationResult);

            var existing = Items.FirstOrDefault(i => i.Id == id);
            if (existing == null) return Task.FromResult(SupplierMutationResult.Failed(SupplierFailureKind.NotFound));

            Items.Remove(existing);
            var updated = existing with { IsActive = isActive, UpdatedAt = DateTime.UtcNow };
            Items.Add(updated);
            return Task.FromResult(new SupplierMutationResult(true, updated));
        }
    }

    [Fact]
    public async Task SupplierListViewModel_Loading_Empty_And_Content_States()
    {
        var repo = new FakeClientSupplierRepository();
        var vm = new SupplierListViewModel(repo);

        // Initial state before load
        vm.HasLoaded.Should().BeFalse();
        vm.IsEmpty.Should().BeFalse();

        // Load empty
        await vm.LoadAsync();
        vm.HasLoaded.Should().BeTrue();
        vm.IsEmpty.Should().BeTrue();
        vm.HasError.Should().BeFalse();
        vm.Items.Should().BeEmpty();
        vm.TotalCount.Should().Be(0);

        // Add items and reload
        repo.Items.Add(new SupplierItem(Guid.NewGuid(), "Supplier A", "Contact A", "0123456789", null, null, true, 2, DateTime.UtcNow, null));
        repo.Items.Add(new SupplierItem(Guid.NewGuid(), "Supplier B", "Contact B", "0987654321", null, null, false, 0, DateTime.UtcNow, null));

        await vm.LoadAsync();
        vm.IsEmpty.Should().BeFalse();
        vm.Items.Should().HaveCount(2);
        vm.TotalCount.Should().Be(2);
        vm.TotalPages.Should().Be(1);
    }

    [Fact]
    public async Task SupplierListViewModel_Error_State()
    {
        var repo = new FakeClientSupplierRepository
        {
            CustomSearchResult = SupplierSearchResult.Failed(SupplierFailureKind.Network, "Connection failed")
        };
        var vm = new SupplierListViewModel(repo);

        await vm.LoadAsync();

        vm.HasError.Should().BeTrue();
        vm.ErrorMessage.Should().Be("Không thể tải danh sách nhà cung cấp. Vui lòng thử lại.");
        vm.IsForbidden.Should().BeFalse("a network failure can be retried");
        vm.IsEmpty.Should().BeFalse();
        vm.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task SupplierListViewModel_Filter_And_Pagination()
    {
        var repo = new FakeClientSupplierRepository();
        for (var i = 1; i <= 25; i++)
        {
            repo.Items.Add(new SupplierItem(
                Guid.NewGuid(),
                $"Supplier {i:D2}",
                null,
                "0123456789",
                null,
                null,
                i % 2 == 0,
                0,
                DateTime.UtcNow,
                null));
        }

        var vm = new SupplierListViewModel(repo);
        await vm.LoadAsync();

        vm.TotalCount.Should().Be(25);
        vm.TotalPages.Should().Be(2);
        vm.Page.Should().Be(1);
        vm.CanGoPrevious.Should().BeFalse();
        vm.CanGoNext.Should().BeTrue();

        // Next page
        await vm.NextPageCommand.ExecuteAsync(null);
        vm.Page.Should().Be(2);
        vm.CanGoPrevious.Should().BeTrue();
        vm.CanGoNext.Should().BeFalse();

        // Filter active
        vm.StatusFilter = SupplierStatusFilter.Active;
        // Wait or let OnStatusFilterChanged reload
        await vm.LoadAsync();
        repo.LastFilterRequest?.IsActive.Should().Be(true);
        vm.Page.Should().Be(1); // Reset to page 1

        // Search text
        vm.SearchText = "Supplier 02";
        await vm.LoadAsync();
        repo.LastFilterRequest?.Search.Should().Be("Supplier 02");
    }

    [Fact]
    public void SupplierListViewModel_Action_Events()
    {
        var repo = new FakeClientSupplierRepository();
        var vm = new SupplierListViewModel(repo) { CanWrite = true };

        SupplierItem? editorRequestedItem = null;
        var editorFired = false;
        vm.SupplierEditorRequested += item =>
        {
            editorFired = true;
            editorRequestedItem = item;
        };

        SupplierItem? statusRequestedItem = null;
        var statusFired = false;
        vm.SupplierStatusRequested += item =>
        {
            statusFired = true;
            statusRequestedItem = item;
        };

        // Create
        vm.CreateSupplierCommand.Execute(null);
        editorFired.Should().BeTrue();
        editorRequestedItem.Should().BeNull();

        // Edit
        var sample = new SupplierItem(Guid.NewGuid(), "Test", null, "0123", null, null, true, 0, DateTime.UtcNow, null);
        editorFired = false;
        vm.EditSupplierCommand.Execute(sample);
        editorFired.Should().BeTrue();
        editorRequestedItem.Should().BeSameAs(sample);

        // Toggle Status
        vm.ToggleStatusCommand.Execute(sample);
        statusFired.Should().BeTrue();
        statusRequestedItem.Should().BeSameAs(sample);
    }

    [Fact]
    public async Task SupplierEditorDialogViewModel_In_Memory_Validation()
    {
        var repo = new FakeClientSupplierRepository();
        var vm = new SupplierEditorDialogViewModel(repo);

        // Empty Name
        vm.Name = "";
        vm.Phone = "0987654321";
        await vm.SaveAsync();
        vm.NameError.Should().NotBeNullOrWhiteSpace();

        // Name too long (>200)
        vm.Name = new string('X', 201);
        await vm.SaveAsync();
        vm.NameError.Should().NotBeNullOrWhiteSpace();

        // Invalid phone
        vm.Name = "Valid Name";
        vm.Phone = "invalid-letters-123";
        await vm.SaveAsync();
        vm.PhoneError.Should().NotBeNullOrWhiteSpace();

        // Invalid email
        vm.Phone = "0987654321";
        vm.Email = "not-an-email";
        await vm.SaveAsync();
        vm.EmailError.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SupplierEditorDialogViewModel_Save_409_Conflict_Sets_NameError()
    {
        var repo = new FakeClientSupplierRepository
        {
            CustomMutationResult = SupplierMutationResult.Failed(SupplierFailureKind.Conflict, "Tên nhà cung cấp đã tồn tại.")
        };
        var vm = new SupplierEditorDialogViewModel(repo)
        {
            Name = "Existing Supplier",
            Phone = "0987654321"
        };

        await vm.SaveAsync();

        vm.NameError.Should().Be("Tên nhà cung cấp đã tồn tại.");
    }

    [Fact]
    public async Task SupplierEditorDialogViewModel_Save_400_BadRequest_Sets_FieldErrors()
    {
        var fieldErrors = new Dictionary<string, IReadOnlyList<string>>
        {
            ["name"] = ["Tên không hợp lệ."],
            ["phone"] = ["Số điện thoại không đúng định dạng."]
        };
        var repo = new FakeClientSupplierRepository
        {
            CustomMutationResult = SupplierMutationResult.Failed(SupplierFailureKind.BadRequest, "Validation error", fieldErrors)
        };
        var vm = new SupplierEditorDialogViewModel(repo)
        {
            Name = "Valid Name",
            Phone = "0987654321"
        };

        await vm.SaveAsync();

        vm.NameError.Should().Be("Tên không hợp lệ.");
        vm.PhoneError.Should().Be("Số điện thoại không đúng định dạng.");
    }

    [Fact]
    public async Task SupplierEditorDialogViewModel_Save_Success_Fires_Events_And_List_Reloads()
    {
        var repo = new FakeClientSupplierRepository();
        var listVm = new SupplierListViewModel(repo);
        var editorVm = new SupplierEditorDialogViewModel(repo)
        {
            Name = "New Supplier",
            Phone = "0987654321"
        };

        SupplierItem? savedItem = null;
        var closeFired = false;
        editorVm.SupplierSaved += item =>
        {
            savedItem = item;
            _ = listVm.LoadAsync(); // List reloads once after save
        };
        editorVm.RequestClose += (_, _) => closeFired = true;

        await editorVm.SaveAsync();

        savedItem.Should().NotBeNull();
        savedItem!.Name.Should().Be("New Supplier");
        closeFired.Should().BeTrue();

        listVm.Items.Should().ContainSingle();
        listVm.Items[0].Name.Should().Be("New Supplier");
        repo.GetSuppliersCallCount.Should().Be(1);
    }

    [Fact]
    public async Task ChangeStatusAsync_OnSuccess_FlipsStatusAndReloadsOnce()
    {
        // Arrange
        var repo = new FakeClientSupplierRepository();
        var supplier = new SupplierItem(Guid.NewGuid(), "FPT Trading", null, "0900", null, null, true, 0, DateTime.UtcNow, null);
        repo.Items.Add(supplier);
        var vm = new SupplierListViewModel(repo);
        await vm.LoadAsync();
        var callsBefore = repo.GetSuppliersCallCount;

        // Act
        var error = await vm.ChangeStatusAsync(supplier);

        // Assert
        error.Should().BeNull();
        repo.GetSuppliersCallCount.Should().Be(callsBefore + 1);
        vm.Items.Single().IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenForbidden_ReturnsVietnameseMessageWithoutReload()
    {
        // Arrange
        var repo = new FakeClientSupplierRepository
        {
            CustomMutationResult = SupplierMutationResult.Failed(SupplierFailureKind.Forbidden, "The API request failed with HTTP status 403.")
        };
        var supplier = new SupplierItem(Guid.NewGuid(), "FPT Trading", null, "0900", null, null, true, 0, DateTime.UtcNow, null);
        var vm = new SupplierListViewModel(repo);

        // Act
        var error = await vm.ChangeStatusAsync(supplier);

        // Assert
        error.Should().Be("Bạn không có quyền thay đổi trạng thái nhà cung cấp.");
        repo.GetSuppliersCallCount.Should().Be(0);
    }

    [Fact]
    public async Task LoadAsync_WhenForbidden_ShowsVietnamesePermissionMessage()
    {
        // Arrange
        var repo = new FakeClientSupplierRepository
        {
            CustomSearchResult = SupplierSearchResult.Failed(SupplierFailureKind.Forbidden)
        };
        var vm = new SupplierListViewModel(repo);

        // Act
        await vm.LoadAsync();

        // Assert
        vm.ErrorMessage.Should().Be("Bạn không có quyền xem danh sách nhà cung cấp.");
        vm.IsForbidden.Should().BeTrue();
    }

    [Fact]
    public void WriteCommands_WithoutWritePermission_RaiseNothing()
    {
        // Arrange
        var vm = new SupplierListViewModel(new FakeClientSupplierRepository());
        var supplier = new SupplierItem(Guid.NewGuid(), "FPT", null, "0900", null, null, true, 0, DateTime.UtcNow, null);
        var raised = 0;
        vm.SupplierEditorRequested += _ => raised++;
        vm.SupplierStatusRequested += _ => raised++;

        // Act
        vm.CreateSupplierCommand.Execute(null);
        vm.EditSupplierCommand.Execute(supplier);
        vm.ToggleStatusCommand.Execute(supplier);

        // Assert
        raised.Should().Be(0);
    }
}
