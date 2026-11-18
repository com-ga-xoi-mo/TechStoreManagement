using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;
using Xunit.Abstractions;

namespace TechStore.UnitTests;

public sealed class M3AcceptancePostgresHttpTests(ITestOutputHelper output)
{
    [PostgresFact]
    public async Task AdminProduct_PriceZeroAndRejectedWrites_MatchCatalogContract()
    {
        await using var api = await IdentityPostgresApi.CreateAsync();
        using var admin = api.Factory.NewClient();
        await IdentityPostgresApi.LoginAsync(admin);
        var tree = await ReadAsync<List<CategoryTreeNodeDto>>(await admin.GetAsync("/api/categories/tree"), HttpStatusCode.OK);
        var category = Assert.Single(tree, row => row.Slug == "accessories");
        var valid = new CreateProductRequest
        {
            CategoryId = category.Id, Sku = "M3-ACCEPTANCE-ZERO", Name = "M3 zero-price probe",
            Brand = "M3 acceptance", BasePrice = 0m, CostPrice = 0m, IsSerialTracked = false
        };
        await using var db = api.Database.CreateContext();
        var beforeCount = await db.Products.CountAsync();
        var rejects = new (string Field, string Suffix, Action<CreateProductRequest> Mutate)[]
        {
            ("name", "empty", request => request.Name = " "),
            ("name", "201", request => request.Name = new string('x', 201)),
            ("basePrice", "negative", request => request.BasePrice = -1m),
            ("categoryId", "unknown-category", request => request.CategoryId = Guid.NewGuid())
        };
        foreach (var (field, suffix, mutate) in rejects)
        {
            var draft = new CreateProductRequest
            {
                CategoryId = valid.CategoryId, Sku = $"M3-ACCEPTANCE-{suffix}", Name = valid.Name,
                Brand = valid.Brand, BasePrice = valid.BasePrice, CostPrice = valid.CostPrice, IsSerialTracked = false
            };
            mutate(draft);
            await AssertBadRequestAsync(await admin.PostAsJsonAsync("/api/products", draft), field);
            Assert.False(await db.Products.AnyAsync(row => row.Sku == draft.Sku));
            output.WriteLine($"product {suffix}: HTTP 400 field={field}, no product");
        }
        Assert.Equal(beforeCount, await db.Products.CountAsync());

        var created = await ReadAsync<ProductDto>(await admin.PostAsJsonAsync("/api/products", valid), HttpStatusCode.Created);
        var persisted = await ReadAsync<ProductDto>(await admin.GetAsync($"/api/products/{created.Id}"), HttpStatusCode.OK);
        Assert.Equal(valid.Sku, persisted.Sku);
        Assert.Equal(0m, persisted.BasePrice);
        Assert.Equal(0m, persisted.CostPrice);
        Assert.Equal(0m, await db.Products.Where(row => row.Id == created.Id).Select(row => row.BasePrice).SingleAsync());
        await AssertStatusAsync(await admin.DeleteAsync($"/api/products/{created.Id}"), HttpStatusCode.NoContent);
        output.WriteLine("product zero: POST 201, GET 200 basePrice=0 costPrice=0, DELETE 204");
    }

    [PostgresFact]
    public async Task AdminCategory_TreeCrudAndValidation_PersistOnlyValidRequests()
    {
        await using var api = await IdentityPostgresApi.CreateAsync();
        using var admin = api.Factory.NewClient();
        await IdentityPostgresApi.LoginAsync(admin);
        const string route = "/api/categories";
        var category = await ReadAsync<CategoryDto>(await admin.PostAsJsonAsync(route,
            new CreateCategoryRequest { Name = "M3 category", Slug = "m3-acceptance-category" }), HttpStatusCode.Created);
        var tree = await ReadAsync<List<CategoryTreeNodeDto>>(await admin.GetAsync(route + "/tree"), HttpStatusCode.OK);
        var original = Assert.Single(tree, x => x.Id == category.Id);
        Assert.Equal("M3 category", original.Name);
        await AssertBadRequestAsync(await admin.PutAsJsonAsync($"{route}/{category.Id}",
            new UpdateCategoryRequest { Name = "M3 incomplete update", Slug = original.Slug }), "isActive");
        var update = await ReadAsync<CategoryDto>(await admin.PutAsJsonAsync($"{route}/{category.Id}",
            new UpdateCategoryRequest { Name = "M3 category updated", Slug = original.Slug, IsActive = original.IsActive }),
            HttpStatusCode.OK);
        Assert.Equal("M3 category updated", update.Name);
        foreach (var name in new[] { " ", new string('x', 201) })
        {
            await AssertBadRequestAsync(await admin.PostAsJsonAsync(route, new CreateCategoryRequest
                { Name = name, Slug = "m3-category-rejected" }), "name");
        }
        await AssertBadRequestAsync(await admin.PutAsJsonAsync($"{route}/{category.Id}",
            new UpdateCategoryRequest { Name = "M3 rejected update", Slug = "bad slug!", IsActive = original.IsActive }), "slug");
        var after = await ReadAsync<List<CategoryTreeNodeDto>>(await admin.GetAsync(route + "/tree"), HttpStatusCode.OK);
        var persisted = Assert.Single(after, x => x.Id == category.Id);
        Assert.Equal(update.Name, persisted.Name);
        Assert.Equal(original.Slug, persisted.Slug);
        Assert.DoesNotContain(after, x => x.Slug == "m3-category-rejected");
        await AssertStatusAsync(await admin.DeleteAsync($"{route}/{category.Id}"), HttpStatusCode.NoContent);
        var final = await ReadAsync<List<CategoryTreeNodeDto>>(await admin.GetAsync(route + "/tree"), HttpStatusCode.OK);
        Assert.DoesNotContain(final, x => x.Id == category.Id);
        output.WriteLine("category: POST 201 / tree 200 / PUT 200 / invalid names, slug and missing isActive 400 / tree unchanged / DELETE 204");
    }

    [PostgresFact]
    public async Task AdminCustomer_LifecycleSearchAndInvalidInputs_MatchCustomerContract()
    {
        await using var api = await IdentityPostgresApi.CreateAsync();
        using var admin = api.Factory.NewClient();
        await IdentityPostgresApi.LoginAsync(admin);
        const string route = "/api/customers";
        var first = await ReadAsync<CustomerDto>(await admin.PostAsJsonAsync(route,
            new CreateCustomerRequest { FullName = "M3 Acceptance Alpha", Phone = "0338182601" }), HttpStatusCode.Created);
        var second = await ReadAsync<CustomerDto>(await admin.PostAsJsonAsync(route,
            new CreateCustomerRequest { FullName = "M3 Acceptance Beta", Phone = "0338182602" }), HttpStatusCode.Created);
        Assert.True(first.IsActive);
        Assert.Equal("Standard", first.Tier);
        var updated = await ReadAsync<CustomerDto>(await admin.PutAsJsonAsync($"{route}/{first.Id}",
            new UpdateCustomerRequest { FullName = "M3 Acceptance Edited", Phone = first.Phone }), HttpStatusCode.OK);
        Assert.Equal("M3 Acceptance Edited", updated.FullName);
        var page1 = await ReadAsync<PagedResultDto<CustomerSummaryDto>>(await admin.GetAsync(
            route + "?q=M3%20Acceptance&isActive=true&tier=Standard&page=1&pageSize=1"), HttpStatusCode.OK);
        var page2 = await ReadAsync<PagedResultDto<CustomerSummaryDto>>(await admin.GetAsync(
            route + "?q=M3%20Acceptance&isActive=true&tier=Standard&page=2&pageSize=1"), HttpStatusCode.OK);
        Assert.Equal((2, 2), (page1.TotalCount, page1.TotalPages));
        Assert.Equal((2, 2), (page2.TotalCount, page2.TotalPages));
        Assert.Equal((1, 2), (page1.Page, page2.Page));
        var ids = new[] { Assert.Single(page1.Items).Id, Assert.Single(page2.Items).Id };
        Assert.Equal(2, ids.Distinct().Count());
        Assert.Contains(first.Id, ids);
        Assert.Contains(second.Id, ids);

        await AssertStatusAsync(await admin.PatchAsync($"{route}/{first.Id}/deactivate", null), HttpStatusCode.NoContent);
        var inactive = await ReadAsync<PagedResultDto<CustomerSummaryDto>>(await admin.GetAsync(
            route + $"?q={first.Phone}&isActive=false&tier=Standard"), HttpStatusCode.OK);
        Assert.Equal(first.Id, Assert.Single(inactive.Items).Id);
        await AssertStatusAsync(await admin.PatchAsync($"{route}/{first.Id}/activate", null), HttpStatusCode.NoContent);

        await AssertBadRequestAsync(await admin.PostAsJsonAsync(route,
            new CreateCustomerRequest { FullName = " ", Phone = "0338182603" }), "fullName");
        await AssertBadRequestAsync(await admin.PutAsJsonAsync($"{route}/{first.Id}",
            new UpdateCustomerRequest { FullName = "M3 rejected phone", Phone = "invalid" }), "phone");
        await AssertBadRequestAsync(await admin.GetAsync(route + "?tier=invalid"), "tier");
        var persisted = await ReadAsync<CustomerDto>(await admin.GetAsync($"{route}/{first.Id}"), HttpStatusCode.OK);
        Assert.Equal(updated.FullName, persisted.FullName);
        Assert.Equal(first.Phone, persisted.Phone);
        Assert.True(persisted.IsActive);
        await using var db = api.Database.CreateContext();
        Assert.False(await db.Customers.AnyAsync(x => x.Phone == "0338182603"));
        output.WriteLine("customer: POST 201 / PUT and GET 200 / page1+2 2 customers / inactive filter / PATCH 204 / name, phone and tier 400");
    }

    [PostgresFact]
    public async Task AdminCustomer_NameOverCatalogLimit_RoundTripsUnderCustomerTextContract()
    {
        await using var api = await IdentityPostgresApi.CreateAsync();
        using var admin = api.Factory.NewClient();
        await IdentityPostgresApi.LoginAsync(admin);
        // Customer full_name is text. The Catalog limit of 200 is not a Customer contract.
        var name = new string('x', 201);
        var created = await ReadAsync<CustomerDto>(await admin.PostAsJsonAsync("/api/customers",
            new CreateCustomerRequest { FullName = name, Phone = "0338182604" }), HttpStatusCode.Created);
        var persisted = await ReadAsync<CustomerDto>(await admin.GetAsync($"/api/customers/{created.Id}"), HttpStatusCode.OK);
        Assert.Equal(name, persisted.FullName);
        output.WriteLine($"customer name boundary: POST 201, GET 200 nameLength={persisted.FullName.Length}");
    }

    [PostgresFact]
    public async Task CashCheckout_InsufficientCash_Returns400AndPreservesCommittedState()
    {
        await using var api = await IdentityPostgresApi.CreateAsync();
        using var cashier = api.Factory.NewClient();
        await IdentityPostgresApi.LoginAsync(cashier, "cashier1", "Cashier@123");
        var shift = await ReadAsync<PosShiftDto>(await cashier.PostAsJsonAsync("/api/pos/shifts",
            new OpenShiftRequest { OpeningBalance = 2_000_000m }), HttpStatusCode.Created);
        const string imei = "490000000000004";
        var serials = await ReadAsync<PagedResultDto<SerialImeiDto>>(await cashier.GetAsync(
            $"/api/inventory/serials?search={imei}&status=InStock"), HttpStatusCode.OK);
        var serial = Assert.Single(serials.Items, x => x.Imei == imei);
        Assert.Equal(SerialImeiStatus.InStock, serial.Status);
        var before = await ObserveCheckoutAsync(api, serial, shift.Id);
        var attempt = new PosCheckoutRequest
        {
            PosSessionId = shift.Id, CheckoutKey = Guid.NewGuid(), CashTendered = 1m,
            Items = [new CreateOrderItemRequest
                { ProductId = serial.ProductId, VariantId = serial.VariantId, SerialImeiId = serial.Id, Quantity = 1 }]
        };
        await AssertBadRequestAsync(await cashier.PostAsJsonAsync("/api/pos/checkout", attempt), "cashTendered");
        var after = await ObserveCheckoutAsync(api, serial, shift.Id);
        Assert.Equal(before, after);
        await using var db = api.Database.CreateContext();
        Assert.False(await db.Orders.AnyAsync(x => x.CheckoutKey == attempt.CheckoutKey));
        output.WriteLine($"cash checkout: HTTP 400 field=cashTendered, key absent; unchanged {after}");
    }

    private static async Task<string> ObserveCheckoutAsync(IdentityPostgresApi api, SerialImeiDto serial, Guid shiftId)
    {
        await using var db = api.Database.CreateContext();
        var stock = await db.InventoryStocks.AsNoTracking()
            .Where(x => x.ProductId == serial.ProductId && x.VariantId == serial.VariantId)
            .Select(x => new { x.Id, x.Quantity, x.ReservedQuantity, x.UpdatedAt }).SingleAsync();
        Assert.True(stock.Quantity > stock.ReservedQuantity);
        var device = await db.SerialImeis.AsNoTracking().Where(x => x.Id == serial.Id)
            .Select(x => new { x.Status, x.WarrantyStartAt, x.WarrantyEndAt, x.UpdatedAt }).SingleAsync();
        var shift = await db.PosSessions.AsNoTracking().Where(x => x.Id == shiftId)
            .Select(x => new { x.Status, x.CashSalesTotal, x.VietQrSalesTotal, x.CardSalesTotal, x.UpdatedAt }).SingleAsync();
        Assert.Equal(PosSessionStatus.Open, shift.Status);
        var ledger = await db.InventoryMovements.AsNoTracking().Where(x => x.InventoryStockId == stock.Id)
            .OrderBy(x => x.Id).Select(x => new { x.Id, x.MovementType, x.QuantityChange, x.QuantityAfter }).ToArrayAsync();
        return JsonSerializer.Serialize(new
        {
            stock, device, shift, ledger, Orders = await db.Orders.CountAsync(), Lines = await db.OrderItems.CountAsync()
        });
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode status) where T : class
    {
        using (response)
        {
            Assert.Equal(status, response.StatusCode);
            return await response.Content.ReadFromJsonAsync<T>() ?? throw new InvalidOperationException("Empty response.");
        }
    }

    private static async Task AssertBadRequestAsync(HttpResponseMessage response, string key)
    {
        var problem = await ReadAsync<ValidationProblemDetails>(response, HttpStatusCode.BadRequest);
        Assert.Equal(400, problem.Status);
        Assert.Contains(key, problem.Errors.Keys, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task AssertStatusAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        using (response)
        {
            Assert.Equal(status, response.StatusCode);
            if (status == HttpStatusCode.NoContent)
                Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        }
    }
}
