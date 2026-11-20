using FluentAssertions;
using TechStore.Api.Inventory;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Services;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.UnitTests;

public class SupplierServiceTests
{
    private readonly FakeSupplierRepository _supplierRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly SupplierService _service;

    public SupplierServiceTests()
    {
        _service = new SupplierService(_supplierRepository, _unitOfWork);
    }

    [Fact]
    public async Task SearchAsync_Should_Filter_And_Hide_Opening_Balance_Supplier()
    {
        // Arrange
        var s1 = new Supplier { Id = Guid.NewGuid(), Name = "FPT Trading", Phone = "028 7300 7300", Email = "sales@fpt.example", IsActive = true };
        var s2 = new Supplier { Id = Guid.NewGuid(), Name = "DGW", Phone = "028 1234 5678", Email = "contact@dgw.example", IsActive = false };
        var systemSupplier = new Supplier { Id = Guid.NewGuid(), Name = SystemSuppliers.OpeningBalanceName, Phone = "N/A", IsActive = false };

        _supplierRepository.Suppliers.AddRange([s1, s2, systemSupplier]);

        // Act - Search "fpt" and active
        var result = await _service.SearchAsync(new SupplierFilterRequest { Search = "fpt", IsActive = true });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().ContainSingle();
        result.Data.Items[0].Name.Should().Be("FPT Trading");

        // Act - Search without filter
        var allResult = await _service.SearchAsync(new SupplierFilterRequest());

        // Assert - System supplier should never appear
        allResult.IsSuccess.Should().BeTrue();
        allResult.Data!.Items.Should().HaveCount(2);
        allResult.Data.Items.Should().NotContain(x => x.Name == SystemSuppliers.OpeningBalanceName);
    }

    [Fact]
    public async Task CreateAsync_With_Valid_Data_Should_Succeed()
    {
        // Act
        var result = await _service.CreateAsync(new SaveSupplierRequest
        {
            Name = "  FPT Trading  ",
            Phone = "028 7300 7300",
            ContactName = "Nguyen Van A",
            Email = "sales@fpt.example",
            Address = "123 Pham Van Dong"
        });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Name.Should().Be("FPT Trading");
        result.Data.Phone.Should().Be("028 7300 7300");
        result.Data.ContactName.Should().Be("Nguyen Van A");
        result.Data.Email.Should().Be("sales@fpt.example");
        result.Data.Address.Should().Be("123 Pham Van Dong");
        result.Data.IsActive.Should().BeTrue();
        result.Data.ReceiptCount.Should().Be(0);

        _supplierRepository.Suppliers.Should().ContainSingle();
    }

    [Theory]
    [InlineData("", "0123456789", "name")] // Empty name
    [InlineData("   ", "0123456789", "name")] // Whitespace name
    [InlineData("Valid Name", "", "phone")] // Missing phone
    [InlineData("Valid Name", "090abc123", "phone")] // Phone with letters
    public async Task CreateAsync_With_Invalid_Required_Fields_Should_Fail_Validation(
        string name, string phone, string expectedErrorField)
    {
        // Act
        var result = await _service.CreateAsync(new SaveSupplierRequest
        {
            Name = name,
            Phone = phone
        });

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors.Should().NotBeNull();
        result.ValidationErrors!.Should().ContainKey(expectedErrorField);
        _supplierRepository.Suppliers.Should().BeEmpty();
    }

    [Theory]
    [InlineData("invalid-email-without-at", "email")]
    [InlineData("missingdomain@", "email")]
    public async Task CreateAsync_With_Invalid_Email_Should_Fail_Validation(string email, string expectedErrorField)
    {
        // Act
        var result = await _service.CreateAsync(new SaveSupplierRequest
        {
            Name = "Supplier",
            Phone = "0123456789",
            Email = email
        });

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors!.Should().ContainKey(expectedErrorField);
    }

    [Fact]
    public async Task CreateAsync_With_Fields_Exceeding_Length_Should_Fail_Validation()
    {
        // Act
        var result = await _service.CreateAsync(new SaveSupplierRequest
        {
            Name = new string('A', 201),
            Phone = new string('1', 31),
            ContactName = new string('B', 201),
            Address = new string('C', 501),
            Email = new string('a', 195) + "@ex.com" // > 200 chars
        });

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ValidationErrors!.Should().ContainKeys("name", "phone", "contactName", "address", "email");
    }

    [Fact]
    public async Task CreateAsync_With_Duplicate_Name_Case_Insensitive_Should_Return_Conflict()
    {
        // Arrange
        _supplierRepository.Suppliers.Add(new Supplier
        {
            Id = Guid.NewGuid(),
            Name = "FPT Trading",
            Phone = "0123456789"
        });

        // Act
        var result = await _service.CreateAsync(new SaveSupplierRequest
        {
            Name = "  fpt trading  ",
            Phone = "0987654321"
        });

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsConflict.Should().BeTrue();
        result.ConflictReason.Should().Be("Tên nhà cung cấp đã tồn tại.");
    }

    [Fact]
    public async Task CreateAsync_With_System_Supplier_Name_Should_Return_Conflict()
    {
        // Act
        var result = await _service.CreateAsync(new SaveSupplierRequest
        {
            Name = " số dư đầu kỳ ",
            Phone = "0123456789"
        });

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsConflict.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_With_Unknown_Id_Should_Return_NotFound()
    {
        // Act
        var result = await _service.UpdateAsync(Guid.NewGuid(), new SaveSupplierRequest
        {
            Name = "Supplier",
            Phone = "0123456789"
        });

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_On_System_Supplier_Should_Return_Conflict()
    {
        // Arrange
        var systemSupplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = SystemSuppliers.OpeningBalanceName,
            Phone = "N/A"
        };
        _supplierRepository.Suppliers.Add(systemSupplier);

        // Act
        var result = await _service.UpdateAsync(systemSupplier.Id, new SaveSupplierRequest
        {
            Name = "New Name",
            Phone = "0123456789"
        });

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsConflict.Should().BeTrue();
        result.ConflictReason.Should().Contain("hệ thống");
    }

    [Fact]
    public async Task UpdateAsync_Renaming_To_Existing_Name_Should_Return_Conflict()
    {
        // Arrange
        var s1 = new Supplier { Id = Guid.NewGuid(), Name = "Supplier A", Phone = "0123456789" };
        var s2 = new Supplier { Id = Guid.NewGuid(), Name = "Supplier B", Phone = "0987654321" };
        _supplierRepository.Suppliers.AddRange([s1, s2]);

        // Act - rename s2 to "supplier a"
        var result = await _service.UpdateAsync(s2.Id, new SaveSupplierRequest
        {
            Name = "  supplier a  ",
            Phone = "0987654321"
        });

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsConflict.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_Keeping_Same_Name_Should_Succeed()
    {
        // Arrange
        var s = new Supplier { Id = Guid.NewGuid(), Name = "Supplier A", Phone = "0123456789" };
        _supplierRepository.Suppliers.Add(s);

        // Act
        var result = await _service.UpdateAsync(s.Id, new SaveSupplierRequest
        {
            Name = "Supplier A",
            Phone = "0999999999"
        });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Phone.Should().Be("0999999999");
    }

    [Fact]
    public async Task SetStatusAsync_Deactivate_And_Reactivate_Should_Succeed()
    {
        // Arrange
        var s = new Supplier { Id = Guid.NewGuid(), Name = "Supplier A", Phone = "0123456789", IsActive = true };
        _supplierRepository.Suppliers.Add(s);

        // Act - Deactivate
        var deactResult = await _service.SetStatusAsync(s.Id, new SetSupplierStatusRequest { IsActive = false });

        // Assert
        deactResult.IsSuccess.Should().BeTrue();
        deactResult.Data!.IsActive.Should().BeFalse();
        s.IsActive.Should().BeFalse();

        // Act - Reactivate
        var reactResult = await _service.SetStatusAsync(s.Id, new SetSupplierStatusRequest { IsActive = true });

        // Assert
        reactResult.IsSuccess.Should().BeTrue();
        reactResult.Data!.IsActive.Should().BeTrue();
        s.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SetStatusAsync_On_System_Supplier_Should_Return_Conflict()
    {
        // Arrange
        var systemSupplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = SystemSuppliers.OpeningBalanceName,
            Phone = "N/A",
            IsActive = false
        };
        _supplierRepository.Suppliers.Add(systemSupplier);

        // Act
        var result = await _service.SetStatusAsync(systemSupplier.Id, new SetSupplierStatusRequest { IsActive = true });

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsConflict.Should().BeTrue();
        result.ConflictReason.Should().Contain("hệ thống");
    }
}
