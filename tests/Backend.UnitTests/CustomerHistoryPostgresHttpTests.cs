using System.Net;
using System.Net.Http.Json;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

// Real JWT pipeline, CustomerService, repositories and PostgreSQL for the three customer history endpoints.
public sealed class CustomerHistoryPostgresHttpTests
{
    [PostgresFact]
    public async Task HistoryEndpoints_UnknownCustomer_Return404_KnownCustomerWithoutHistory_Returns200Empty()
    {
        // Arrange
        await using var api = await IdentityPostgresApi.CreateAsync();
        using var admin = api.Factory.NewClient();
        await IdentityPostgresApi.LoginAsync(admin);
        using var created = await admin.PostAsJsonAsync("/api/customers", new CreateCustomerRequest { FullName = "Khách chưa mua", Phone = "0988000111" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var customer = (await created.Content.ReadFromJsonAsync<CustomerDto>())!;
        var unknown = Guid.NewGuid();

        foreach (var history in new[] { "orders", "loyalty-points", "purchased-items" })
        {
            // Act
            using var missing = await admin.GetAsync($"/api/customers/{unknown}/{history}");
            using var empty = await admin.GetAsync($"/api/customers/{customer.Id}/{history}");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
            var page = await empty.Content.ReadFromJsonAsync<PagedResultDto<object>>();
            Assert.NotNull(page);
            Assert.Empty(page.Items);
            Assert.Equal(0, page.TotalCount);
        }
    }
}
