using System.Text.Json;
using FluentAssertions;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Catalog.Services;
using TechStore.Api.Common.Data;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.UnitTests;

public class ProductServiceUpdateTests
{
    private static (FakeProductRepository ProductRepo, FakeCategoryRepository CategoryRepo, FakeUnitOfWork UnitOfWork, ProductService Service) CreateContext()
    {
        var productRepo = new FakeProductRepository();
        var categoryRepo = new FakeCategoryRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new ProductService(productRepo, categoryRepo, unitOfWork);
        return (productRepo, categoryRepo, unitOfWork, service);
    }

    // -------------------------------------------------------------
    // Task 10.8: Happy path tests
    // -------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_ValidRequest_ReplacesFieldsSavesOnceAndCommits()
    {
        // Arrange
        var (productRepo, categoryRepo, unitOfWork, service) = CreateContext();
        var category = ProductCommandTestData.NewActiveCategory();
        categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id);
        productRepo.Products[product.Id] = product;

        var request = ProductCommandTestData.NewRequestFrom(product);
        request.Name = "MacBook Pro";
        request.Brand = "Apple Inc";
        request.BasePrice = 35000000m;
        request.CostPrice = 30000000m;
        request.Description = "New desc";

        // Act
        var result = await service.UpdateAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.Success);
        result.Data!.Name.Should().Be("MacBook Pro");
        product.UpdatedAt.Should().NotBeNull();
        product.Sku.Should().Be("MBA-M3");
        product.IsActive.Should().BeTrue();
        product.CreatedAt.Should().Be(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        unitOfWork.SaveChangesCount.Should().Be(1);
        unitOfWork.CommitCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_Specs_AreReplacedNotMerged()
    {
        // Arrange
        var (productRepo, categoryRepo, unitOfWork, service) = CreateContext();
        var category = ProductCommandTestData.NewActiveCategory();
        categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id);
        productRepo.Products[product.Id] = product;

        var request = ProductCommandTestData.NewRequestFrom(product);
        request.Specs = new Dictionary<string, JsonElement>
        {
            ["ram_gb"] = JsonDocument.Parse("16").RootElement.Clone()
        };

        // Act
        var result = await service.UpdateAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.Success);
        var parsed = ProductSpecHelper.ParseJsonSpecs(product.Specs);
        parsed.Should().ContainKey("ram_gb");
        parsed.Should().NotContainKey("cpu");
        parsed.Count.Should().Be(1);
        parsed["ram_gb"].GetInt32().Should().Be(16);
    }

    [Fact]
    public async Task UpdateAsync_NullSpecs_StoresEmptyObject()
    {
        // Arrange
        var (productRepo, categoryRepo, unitOfWork, service) = CreateContext();
        var category = ProductCommandTestData.NewActiveCategory();
        categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id);
        productRepo.Products[product.Id] = product;

        var request = ProductCommandTestData.NewRequestFrom(product);
        request.Specs = null;

        // Act
        var result = await service.UpdateAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.Success);
        product.Specs.Should().Be("{}");
    }

    [Fact]
    public async Task UpdateAsync_BlankOptionalStrings_StoredAsNull()
    {
        // Arrange
        var (productRepo, categoryRepo, unitOfWork, service) = CreateContext();
        var category = ProductCommandTestData.NewActiveCategory();
        categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id, barcode: "12345");
        product.Description = "Old desc";
        product.ImageUrl = "https://example.com/img.png";
        productRepo.Products[product.Id] = product;

        var request = ProductCommandTestData.NewRequestFrom(product);
        request.Barcode = "   ";
        request.Description = "   ";
        request.ImageUrl = "   ";

        // Act
        var result = await service.UpdateAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.Success);
        product.Barcode.Should().BeNull();
        product.Description.Should().BeNull();
        product.ImageUrl.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_InactiveProduct_StaysInactive()
    {
        // Arrange
        var (productRepo, categoryRepo, unitOfWork, service) = CreateContext();
        var category = ProductCommandTestData.NewActiveCategory();
        categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id, isActive: false);
        productRepo.Products[product.Id] = product;

        var request = ProductCommandTestData.NewRequestFrom(product);

        // Act
        var result = await service.UpdateAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.Success);
        product.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_MoveToAnotherActiveCategory_ReturnsNewCategoryName()
    {
        // Arrange
        var (productRepo, categoryRepo, unitOfWork, service) = CreateContext();
        var cat1 = ProductCommandTestData.NewActiveCategory("Laptops");
        var cat2 = ProductCommandTestData.NewActiveCategory("Ultrabooks");
        categoryRepo.Categories[cat1.Id] = cat1;
        categoryRepo.Categories[cat2.Id] = cat2;

        var product = ProductCommandTestData.NewProduct(cat1.Id);
        productRepo.Products[product.Id] = product;

        var request = ProductCommandTestData.NewRequestFrom(product);
        request.CategoryId = cat2.Id;

        // Act
        var result = await service.UpdateAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.Success);
        result.Data!.CategoryName.Should().Be("Ultrabooks");
        product.CategoryId.Should().Be(cat2.Id);
    }

    // -------------------------------------------------------------
    // Task 10.9: Variant tests
    // -------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_NewVariant_IsAddedToProductAndStaged()
    {
        // Arrange
        var (productRepo, categoryRepo, unitOfWork, service) = CreateContext();
        var category = ProductCommandTestData.NewActiveCategory();
        categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id);
        productRepo.Products[product.Id] = product;

        var request = ProductCommandTestData.NewRequestFrom(product);
        request.Variants =
        [
            new UpdateProductVariantRequest
            {
                VariantName = "Space Gray",
                Sku = "MBA-M3-SG",
                Price = 27990000m
            }
        ];

        // Act
        var result = await service.UpdateAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.Success);
        product.Variants.Should().HaveCount(1);
        productRepo.AddedVariants.Should().HaveCount(1);
        var added = productRepo.AddedVariants[0];
        added.IsActive.Should().BeTrue();
        added.ProductId.Should().Be(product.Id);
        added.Sku.Should().Be("MBA-M3-SG");
    }

    [Fact]
    public async Task UpdateAsync_ExistingVariant_UpdatesPriceNameAndSpecs()
    {
        // Arrange
        var (productRepo, categoryRepo, unitOfWork, service) = CreateContext();
        var category = ProductCommandTestData.NewActiveCategory();
        categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id);
        var variant = ProductCommandTestData.NewVariant(product, "MBA-M3-ORIG", price: 25000000m);
        productRepo.Products[product.Id] = product;

        var request = ProductCommandTestData.NewRequestFrom(product);
        request.Variants =
        [
            new UpdateProductVariantRequest
            {
                Id = variant.Id,
                VariantName = "New Name",
                Price = 26000000m,
                Specs = new Dictionary<string, JsonElement>
                {
                    ["color"] = JsonDocument.Parse("\"Gold\"").RootElement.Clone()
                }
            }
        ];

        // Act
        var result = await service.UpdateAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.Success);
        variant.VariantName.Should().Be("New Name");
        variant.Price.Should().Be(26000000m);
        variant.Specs.Should().Contain("Gold");
        variant.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_ExistingVariantIsActiveFalse_RetiresOnlyThatVariant()
    {
        // Arrange
        var (productRepo, categoryRepo, unitOfWork, service) = CreateContext();
        var category = ProductCommandTestData.NewActiveCategory();
        categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id);
        var v1 = ProductCommandTestData.NewVariant(product, "V1", isActive: true);
        var v2 = ProductCommandTestData.NewVariant(product, "V2", isActive: true);
        productRepo.Products[product.Id] = product;

        var request = ProductCommandTestData.NewRequestFrom(product);
        request.Variants =
        [
            new UpdateProductVariantRequest
            {
                Id = v1.Id,
                VariantName = "V1",
                Price = 100m,
                IsActive = false
            }
        ];

        // Act
        var result = await service.UpdateAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.Success);
        v1.IsActive.Should().BeFalse();
        v2.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_ExistingVariantWithoutIsActive_KeepsFlag()
    {
        // Arrange
        var (productRepo, categoryRepo, unitOfWork, service) = CreateContext();
        var category = ProductCommandTestData.NewActiveCategory();
        categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id);
        var v1 = ProductCommandTestData.NewVariant(product, "V1", isActive: false);
        productRepo.Products[product.Id] = product;

        var request = ProductCommandTestData.NewRequestFrom(product);
        request.Variants =
        [
            new UpdateProductVariantRequest
            {
                Id = v1.Id,
                VariantName = "V1",
                Price = 100m,
                IsActive = null
            }
        ];

        // Act
        var result = await service.UpdateAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.Success);
        v1.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_UnlistedVariants_AreKept()
    {
        // Arrange
        var (productRepo, categoryRepo, unitOfWork, service) = CreateContext();
        var category = ProductCommandTestData.NewActiveCategory();
        categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id);
        var v1 = ProductCommandTestData.NewVariant(product, "V1");
        var v2 = ProductCommandTestData.NewVariant(product, "V2");
        var v3 = ProductCommandTestData.NewVariant(product, "V3");
        productRepo.Products[product.Id] = product;

        var request = ProductCommandTestData.NewRequestFrom(product);
        request.Variants =
        [
            new UpdateProductVariantRequest
            {
                Id = v1.Id,
                VariantName = "V1-Updated",
                Price = 100m
            }
        ];

        // Act
        var result = await service.UpdateAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.Success);
        product.Variants.Should().HaveCount(3);
    }

    [Fact]
    public async Task UpdateAsync_ExistingVariantSameSkuOtherCase_Succeeds()
    {
        // Arrange
        var (productRepo, categoryRepo, unitOfWork, service) = CreateContext();
        var category = ProductCommandTestData.NewActiveCategory();
        categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id);
        var v1 = ProductCommandTestData.NewVariant(product, "OLD-SKU");
        productRepo.Products[product.Id] = product;

        var request = ProductCommandTestData.NewRequestFrom(product);
        request.Variants =
        [
            new UpdateProductVariantRequest
            {
                Id = v1.Id,
                VariantName = "V1",
                Sku = "old-sku",
                Price = 100m
            }
        ];

        // Act
        var result = await service.UpdateAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.Success);
        v1.Sku.Should().Be("OLD-SKU");
    }

    [Fact]
    public async Task UpdateAsync_ProductWithVariantsUsedInStock_CanAddAnotherVariant()
    {
        // Arrange
        var (productRepo, categoryRepo, unitOfWork, service) = CreateContext();
        var category = ProductCommandTestData.NewActiveCategory();
        categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id);
        var v1 = ProductCommandTestData.NewVariant(product, "V1");
        productRepo.Products[product.Id] = product;
        productRepo.InUseProductIds.Add(product.Id);

        var request = ProductCommandTestData.NewRequestFrom(product);
        request.Variants =
        [
            new UpdateProductVariantRequest
            {
                VariantName = "V2",
                Sku = "V2",
                Price = 200m
            }
        ];

        // Act
        var result = await service.UpdateAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.Success);
        product.Variants.Should().HaveCount(2);
    }

    // -------------------------------------------------------------
    // Task 10.10: Rejection tests
    // -------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_InvalidRequest_ReturnsValidationErrorsWithoutTransaction()
    {
        // Arrange
        var (productRepo, categoryRepo, unitOfWork, service) = CreateContext();
        var category = ProductCommandTestData.NewActiveCategory();
        categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id);
        productRepo.Products[product.Id] = product;

        var request = ProductCommandTestData.NewRequestFrom(product);
        request.Name = "";

        // Act
        var result = await service.UpdateAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.ValidationFailed);
        unitOfWork.BeginTransactionCount.Should().Be(0);
        unitOfWork.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_ReturnsNotFound()
    {
        // Arrange
        var (productRepo, categoryRepo, unitOfWork, service) = CreateContext();
        var category = ProductCommandTestData.NewActiveCategory();
        categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id);

        var request = ProductCommandTestData.NewRequestFrom(product);

        // Act
        var result = await service.UpdateAsync(Guid.NewGuid(), request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.NotFound);
        unitOfWork.SaveChangesCount.Should().Be(0);
        unitOfWork.RollbackCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_UnknownCategory_ReturnsCategoryError()
    {
        // Arrange
        var (productRepo, categoryRepo, unitOfWork, service) = CreateContext();
        var category = ProductCommandTestData.NewActiveCategory();
        categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id);
        productRepo.Products[product.Id] = product;

        var request = ProductCommandTestData.NewRequestFrom(product);
        request.CategoryId = Guid.NewGuid();

        // Act
        var result = await service.UpdateAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.ValidationFailed);
        result.ValidationErrors.Should().ContainKey("categoryId");
        unitOfWork.SaveChangesCount.Should().Be(0);
        unitOfWork.RollbackCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_InactiveCategory_ReturnsCategoryError()
    {
        // Arrange
        var (productRepo, categoryRepo, unitOfWork, service) = CreateContext();
        var category = ProductCommandTestData.NewActiveCategory();
        category.IsActive = false;
        categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id);
        productRepo.Products[product.Id] = product;

        var request = ProductCommandTestData.NewRequestFrom(product);

        // Act
        var result = await service.UpdateAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.ValidationFailed);
        result.ValidationErrors.Should().ContainKey("categoryId");
        unitOfWork.SaveChangesCount.Should().Be(0);
        unitOfWork.RollbackCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_VariantIdOfAnotherProduct_ReturnsIdError()
    {
        // Arrange
        var (productRepo, categoryRepo, unitOfWork, service) = CreateContext();
        var category = ProductCommandTestData.NewActiveCategory();
        categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id);
        var otherProduct = ProductCommandTestData.NewProduct(category.Id, sku: "OTHER");
        var otherVariant = ProductCommandTestData.NewVariant(otherProduct, "OTHER-VAR");
        productRepo.Products[product.Id] = product;
        productRepo.Products[otherProduct.Id] = otherProduct;

        var request = ProductCommandTestData.NewRequestFrom(product);
        request.Variants =
        [
            new UpdateProductVariantRequest
            {
                Id = otherVariant.Id,
                VariantName = "Var",
                Price = 100m
            }
        ];

        // Act
        var result = await service.UpdateAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.ValidationFailed);
        result.ValidationErrors.Should().ContainKey("variants[0].id");
        unitOfWork.SaveChangesCount.Should().Be(0);
        unitOfWork.RollbackCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_ChangedExistingVariantSku_ReturnsSkuError()
    {
        // Arrange
        var (productRepo, categoryRepo, unitOfWork, service) = CreateContext();
        var category = ProductCommandTestData.NewActiveCategory();
        categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id);
        var v1 = ProductCommandTestData.NewVariant(product, "OLD-SKU");
        productRepo.Products[product.Id] = product;

        var request = ProductCommandTestData.NewRequestFrom(product);
        request.Variants =
        [
            new UpdateProductVariantRequest
            {
                Id = v1.Id,
                VariantName = "V1",
                Sku = "NEW-SKU",
                Price = 100m
            }
        ];

        // Act
        var result = await service.UpdateAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.ValidationFailed);
        result.ValidationErrors.Should().ContainKey("variants[0].sku");
        unitOfWork.SaveChangesCount.Should().Be(0);
        unitOfWork.RollbackCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_NewVariantSkuEqualsProductSku_ReturnsSkuError()
    {
        // Arrange
        var (productRepo, categoryRepo, unitOfWork, service) = CreateContext();
        var category = ProductCommandTestData.NewActiveCategory();
        categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id, sku: "PROD-SKU");
        productRepo.Products[product.Id] = product;

        var request = ProductCommandTestData.NewRequestFrom(product);
        request.Variants =
        [
            new UpdateProductVariantRequest
            {
                VariantName = "V1",
                Sku = "PROD-SKU",
                Price = 100m
            }
        ];

        // Act
        var result = await service.UpdateAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.ValidationFailed);
        result.ValidationErrors.Should().ContainKey("variants[0].sku");
        unitOfWork.SaveChangesCount.Should().Be(0);
        unitOfWork.RollbackCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_BarcodeKeptWhileAddingFirstVariant_ReturnsBarcodeError()
    {
        // Arrange
        var (productRepo, categoryRepo, unitOfWork, service) = CreateContext();
        var category = ProductCommandTestData.NewActiveCategory();
        categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id, barcode: "8934567890123");
        productRepo.Products[product.Id] = product;

        var request = ProductCommandTestData.NewRequestFrom(product);
        request.Barcode = "8934567890123";
        request.Variants =
        [
            new UpdateProductVariantRequest
            {
                VariantName = "V1",
                Sku = "V1",
                Price = 100m
            }
        ];

        // Act
        var result = await service.UpdateAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.ValidationFailed);
        result.ValidationErrors.Should().ContainKey("barcode");
        unitOfWork.SaveChangesCount.Should().Be(0);
        unitOfWork.RollbackCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_SerialTrackingChangeOnUsedProduct_ReturnsConflict()
    {
        // Arrange
        var (productRepo, categoryRepo, unitOfWork, service) = CreateContext();
        var category = ProductCommandTestData.NewActiveCategory();
        categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id, serialTracked: true);
        productRepo.Products[product.Id] = product;
        productRepo.InUseProductIds.Add(product.Id);

        var request = ProductCommandTestData.NewRequestFrom(product);
        request.IsSerialTracked = false;

        // Act
        var result = await service.UpdateAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.Conflict);
        product.IsSerialTracked.Should().BeTrue();
        unitOfWork.SaveChangesCount.Should().Be(0);
        unitOfWork.RollbackCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_SerialTrackingChangeOnUnusedProduct_Succeeds()
    {
        // Arrange
        var (productRepo, categoryRepo, unitOfWork, service) = CreateContext();
        var category = ProductCommandTestData.NewActiveCategory();
        categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id, serialTracked: true);
        productRepo.Products[product.Id] = product;

        var request = ProductCommandTestData.NewRequestFrom(product);
        request.IsSerialTracked = false;

        // Act
        var result = await service.UpdateAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.Success);
        product.IsSerialTracked.Should().BeFalse();
        unitOfWork.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_FirstVariantOnUsedProduct_ReturnsConflict()
    {
        // Arrange
        var (productRepo, categoryRepo, unitOfWork, service) = CreateContext();
        var category = ProductCommandTestData.NewActiveCategory();
        categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id);
        productRepo.Products[product.Id] = product;
        productRepo.InUseProductIds.Add(product.Id);

        var request = ProductCommandTestData.NewRequestFrom(product);
        request.Variants =
        [
            new UpdateProductVariantRequest
            {
                VariantName = "V1",
                Sku = "V1",
                Price = 100m
            }
        ];

        // Act
        var result = await service.UpdateAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.Conflict);
        productRepo.AddedVariants.Should().BeEmpty();
        unitOfWork.SaveChangesCount.Should().Be(0);
        unitOfWork.RollbackCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_NewVariantSkuAlreadyExists_ReturnsConflictWithSku()
    {
        // Arrange
        var (productRepo, categoryRepo, unitOfWork, service) = CreateContext();
        var category = ProductCommandTestData.NewActiveCategory();
        categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id);
        productRepo.Products[product.Id] = product;
        productRepo.ExistingSkus.Add("MBA-M3-16");

        var request = ProductCommandTestData.NewRequestFrom(product);
        request.Variants =
        [
            new UpdateProductVariantRequest
            {
                VariantName = "V1",
                Sku = "MBA-M3-16",
                Price = 100m
            }
        ];

        // Act
        var result = await service.UpdateAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.Conflict);
        result.ConflictMessage.Should().Contain("MBA-M3-16");
        unitOfWork.SaveChangesCount.Should().Be(0);
        unitOfWork.RollbackCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_SaveThrowsDuplicateKey_ReturnsConflictAndRollsBack()
    {
        // Arrange
        var (productRepo, categoryRepo, unitOfWork, service) = CreateContext();
        var category = ProductCommandTestData.NewActiveCategory();
        categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id);
        productRepo.Products[product.Id] = product;
        unitOfWork.ExceptionToThrowOnSave = new DuplicateKeyException("idx_product_variants_sku");

        var request = ProductCommandTestData.NewRequestFrom(product);
        request.Variants =
        [
            new UpdateProductVariantRequest
            {
                VariantName = "V1",
                Sku = "V1",
                Price = 100m
            }
        ];

        // Act
        var result = await service.UpdateAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.Conflict);
        result.ConflictMessage.Should().Be("A SKU in this request is already in use.");
        unitOfWork.RollbackCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_SeveralServiceErrors_ReportedTogether()
    {
        // Arrange
        var (productRepo, categoryRepo, unitOfWork, service) = CreateContext();
        var category = ProductCommandTestData.NewActiveCategory();
        category.IsActive = false;
        categoryRepo.Categories[category.Id] = category;
        var product = ProductCommandTestData.NewProduct(category.Id);
        var v1 = ProductCommandTestData.NewVariant(product, "OLD-SKU");
        productRepo.Products[product.Id] = product;

        var request = ProductCommandTestData.NewRequestFrom(product);
        request.Variants =
        [
            new UpdateProductVariantRequest
            {
                Id = v1.Id,
                VariantName = "V1",
                Sku = "NEW-SKU",
                Price = 100m
            }
        ];

        // Act
        var result = await service.UpdateAsync(product.Id, request);

        // Assert
        result.Status.Should().Be(ProductCommandStatus.ValidationFailed);
        result.ValidationErrors.Should().ContainKey("categoryId");
        result.ValidationErrors.Should().ContainKey("variants[0].sku");
        unitOfWork.SaveChangesCount.Should().Be(0);
        unitOfWork.RollbackCount.Should().Be(1);
    }
}
