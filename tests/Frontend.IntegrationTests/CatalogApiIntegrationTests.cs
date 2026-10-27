using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TechStore.Client.Models.Catalog;
using TechStore.Client.Repositories.Catalog;
using TechStore.Client.Services.Api;
using TechStore.Client.Services.Session;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;
using Xunit.Abstractions;

namespace TechStore.Client.IntegrationTests;

public sealed class CatalogApiIntegrationTests(ITestOutputHelper output)
{
    private const string BaseUrlEnvironmentVariable = "TECHSTORE_API_BASE_URL";

    [OptInFact]
    public async Task LiveCatalog_ReadFlowMatchesTheCurrentApiAndDataset()
    {
        var baseUrl = Environment.GetEnvironmentVariable(BaseUrlEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException($"Set {BaseUrlEnvironmentVariable} to opt in to the live Catalog API integration test.");

        var options = new ApiClientOptions { BaseUrl = baseUrl, TimeoutSeconds = 15 };
        Uri baseUri;
        try { baseUri = options.Validate(); }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Preflight failed: {BaseUrlEnvironmentVariable} is invalid.", exception);
        }

        var session = new InMemorySessionStore();
        using var handler = new BearerTokenHandler(session)
        {
            InnerHandler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }
        };
        using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        await SignInAsync(http, baseUri, session);
        var api = new ApiClient(http, options);
        var products = new ProductRepository(api);
        var categories = new CategoryRepository(api);

        ProductPage firstPage;
        IReadOnlyList<CategoryOption> categoryTree;
        try
        {
            firstPage = await products.SearchAsync(new ProductQuery { Page = 1, PageSize = 2, Sort = "name:asc" });
            categoryTree = await categories.GetTreeAsync();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Preflight failed for {baseUri}: GET /api/products and /api/categories/tree must be reachable and return valid Catalog contracts.",
                exception);
        }

        Assert.True(firstPage.TotalCount > 0, "The live API is reachable but its Catalog seed dataset is empty.");
        Assert.NotEmpty(categoryTree);
        var all = new List<ProductSummary>();
        var expectedPages = firstPage.TotalPages;
        for (var page = 1; page <= expectedPages; page++)
        {
            var result = page == 1
                ? firstPage
                : await products.SearchAsync(new ProductQuery { Page = page, PageSize = 2, Sort = "name:asc" });
            Assert.Equal(expectedPages, result.TotalPages);
            Assert.Equal(firstPage.TotalCount, result.TotalCount);
            all.AddRange(result.Items);
        }
        Assert.Equal(firstPage.TotalCount, all.Count);
        Assert.Equal(all.Count, all.Select(item => item.Id).Distinct().Count());

        var first = all[0];
        Assert.Contains(categoryTree, item => item.Id == first.CategoryId);
        var combined = await products.SearchAsync(new ProductQuery
        {
            Query = first.Sku,
            Brand = first.Brand,
            CategoryId = first.CategoryId,
            Page = 1,
            PageSize = 20
        });
        Assert.Contains(combined.Items, item => item.Id == first.Id);

        var priceFiltered = await products.SearchAsync(new ProductQuery
        {
            MinPrice = first.PriceFrom,
            MaxPrice = first.PriceTo,
            Sort = "price:asc",
            Page = 1,
            PageSize = 20
        });
        Assert.Contains(priceFiltered.Items, item => item.Id == first.Id);
        Assert.Equal(priceFiltered.Items.OrderBy(item => item.PriceFrom).Select(item => item.Id),
            priceFiltered.Items.Select(item => item.Id));

        var details = new List<ProductDetail>();
        foreach (var item in all)
        {
            var detail = await products.GetByIdAsync(item.Id);
            Assert.NotNull(detail);
            Assert.Equal(item.Id, detail.Id);
            details.Add(detail);
        }
        var spec = details
            .SelectMany(detail => detail.Variants.SelectMany(variant => variant.EffectiveSpecs.Select(pair => (pair.Key, pair.Value))))
            .FirstOrDefault(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value.ValueKind is
                JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False);
        if (string.IsNullOrWhiteSpace(spec.Key))
            throw new InvalidOperationException("Preflight failed: the live seeded Catalog dataset has no primitive variant effectiveSpecs to exercise.");
        var specFilter = await products.SearchAsync(new ProductQuery
        {
            Specs = [$"{spec.Key}:{spec.Value}"]
        });
        Assert.Contains(specFilter.Items, item => details.Any(detail => detail.Id == item.Id &&
            detail.Variants.Any(variant => variant.EffectiveSpecs.TryGetValue(spec.Key, out var value) && value.ToString() == spec.Value.ToString())));

        var noMatch = await products.SearchAsync(new ProductQuery { Query = $"no-live-match-{Guid.NewGuid():N}" });
        Assert.Empty(noMatch.Items);
        Assert.Equal(0, noMatch.TotalCount);

        var missing = await products.GetByIdAsync(Guid.NewGuid());
        Assert.Null(missing);

        using var badSort = await http.GetAsync(new Uri(baseUri,
            "api/products?sort=" + Uri.EscapeDataString("notAField:desc")));
        Assert.Equal(HttpStatusCode.BadRequest, badSort.StatusCode);
        using var badSpec = await http.GetAsync(new Uri(baseUri, "api/products?spec=invalid"));
        Assert.Equal(HttpStatusCode.BadRequest, badSpec.StatusCode);

        output.WriteLine($"API origin: {baseUri.GetLeftPart(UriPartial.Authority)}");
        output.WriteLine($"Seed dataset: {firstPage.TotalCount} products, {categoryTree.Count} active category roots, {expectedPages} pages at pageSize=2.");
        output.WriteLine($"First product: {first.Name} ({first.Sku}); category={first.CategoryId}; brand={first.Brand}; detail variants={details[0].Variants.Count}.");
        output.WriteLine($"Checks: paging/count uniqueness, keyword+brand+category, price overlap/order, effectiveSpecs, empty result, raw 400 validation, detail and random 404.");
    }

    // Category reads need a session, so the suite signs in with the seeded administrator.
    private static async Task SignInAsync(HttpClient http, Uri baseUri, InMemorySessionStore session)
    {
        using var response = await http.PostAsJsonAsync(
            new Uri(baseUri, "api/auth/login"),
            new LoginRequest { Username = "admin", Password = "Admin@123" });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponseDto>()
            ?? throw new InvalidOperationException("Login returned no body.");
        session.SetAccessToken(login.AccessToken);
    }
}

public sealed class OptInFactAttribute : FactAttribute
{
    public OptInFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TECHSTORE_API_BASE_URL")))
            Skip = "Set TECHSTORE_API_BASE_URL to opt in to the live Catalog API integration test.";
    }
}
