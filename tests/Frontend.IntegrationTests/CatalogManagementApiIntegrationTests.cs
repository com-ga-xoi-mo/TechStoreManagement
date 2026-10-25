using System.Text.Json;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Session;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;
using Xunit;

namespace TechStore.Client.IntegrationTests;

public sealed class CatalogManagementApiIntegrationTests
{
    private const string BaseUrlEnvironmentVariable = "TECHSTORE_API_BASE_URL";

    [OptInFact]
    public async Task LiveCatalog_FullManagementLifecycleMatchesServerContract()
    {
        var baseUrl = Environment.GetEnvironmentVariable(BaseUrlEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException($"Set {BaseUrlEnvironmentVariable} to opt in to the live Catalog Management API integration test.");

        var options = new ApiClientOptions { BaseUrl = baseUrl, TimeoutSeconds = 15 };
        options.Validate();

        var session = new InMemorySessionStore();
        using var handler = new BearerTokenHandler(session)
        {
            InnerHandler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }
        };
        using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var api = new ApiClient(http, options);

        var password = Environment.GetEnvironmentVariable("TECHSTORE_TEST_ADMIN_PASSWORD") ?? "Admin@123";
        var login = await api.PostAsync<LoginRequest, LoginResponseDto>(
            "api/auth/login",
            new LoginRequest { Username = "admin", Password = password });

        session.SetSession(
            login.AccessToken,
            login.RefreshToken,
            login.User.Username,
            login.User.FullName,
            login.User.Role.ToString());

        var categoryTree = await new CategoryRepository(api).GetTreeAsync();
        Assert.NotEmpty(categoryTree);
        var category = categoryTree[0];

        IProductCommandRepository commands = new ProductRepository(api);
        var sku = ("IT-" + Guid.NewGuid().ToString("N"))[..20];
        using var dpiDoc = JsonDocument.Parse("25600");

        Guid? createdId = null;
        try
        {
            // 1. Create
            var created = await commands.CreateAsync(new CreateProductRequest
            {
                CategoryId = category.Id,
                Name = "Live Test Product",
                Sku = sku,
                Brand = "LiveBrand",
                BasePrice = 1000000m,
                CostPrice = 800000m,
                Barcode = "L-BAR",
                IsSerialTracked = true,
                Specs = new Dictionary<string, JsonElement>
                {
                    ["dpi"] = dpiDoc.RootElement.Clone()
                }
            });

            createdId = created.Id;
            Assert.Equal(sku, created.Sku);

            // 2. Update with 1 variant
            var updated = await commands.UpdateAsync(created.Id, new UpdateProductRequest
            {
                CategoryId = category.Id,
                Name = "Live Test Product",
                Brand = "LiveBrand",
                BasePrice = 1000000m,
                CostPrice = 800000m,
                Barcode = null,
                IsSerialTracked = true,
                Specs = new Dictionary<string, JsonElement>
                {
                    ["dpi"] = dpiDoc.RootElement.Clone()
                },
                Variants =
                [
                    new UpdateProductVariantRequest
                    {
                        VariantName = "V1",
                        Sku = sku + "-V1",
                        Barcode = null,
                        Price = 1100000m,
                        Specs = new Dictionary<string, JsonElement>(),
                        IsActive = true
                    }
                ]
            });

            var variant = Assert.Single(updated.Variants);
            Assert.Equal(sku + "-V1", variant.Sku);

            // 3. SetStatusAsync false
            var deactivated = await commands.SetStatusAsync(created.Id, false);
            Assert.False(deactivated.IsActive);

            // 4. SetStatusAsync true
            var reactivated = await commands.SetStatusAsync(created.Id, true);
            Assert.True(reactivated.IsActive);

            // 5. DeleteAsync
            await commands.DeleteAsync(created.Id);
            createdId = null;

            // 6. Verify deleted
            var readBack = await new ProductRepository(api).GetByIdAsync(created.Id);
            Assert.Null(readBack);
        }
        finally
        {
            if (createdId is { } idToDelete)
            {
                try
                {
                    await commands.DeleteAsync(idToDelete);
                }
                catch
                {
                    // Ignore error on teardown
                }
            }
        }
    }
}
