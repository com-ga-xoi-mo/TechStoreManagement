using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using TechStore.Api.Customers.Services;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

public class CustomerAuthorizationHttpTests : IClassFixture<CustomerApiFactory>
{
    private readonly HttpClient _client;

    public CustomerAuthorizationHttpTests(CustomerApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task CustomersEndpoint_Should_Return401403And200ForMissingSalesAndAdmin()
    {
        var anonymousResponse = await _client.GetAsync("/api/customers");
        var salesRequest = new HttpRequestMessage(HttpMethod.Get, "/api/customers");
        salesRequest.Headers.Authorization = new("Bearer", CreateToken("SalesStaff"));
        var salesResponse = await _client.SendAsync(salesRequest);
        var adminRequest = new HttpRequestMessage(HttpMethod.Get, "/api/customers");
        adminRequest.Headers.Authorization = new("Bearer", CreateToken("Admin"));
        var adminResponse = await _client.SendAsync(adminRequest);

        anonymousResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
        salesResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        adminResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        (await adminResponse.Content.ReadAsStringAsync()).Should().Contain("totalCount");

        var unknown = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/customers/00000000-0000-0000-0000-000000000001")
        {
            Headers = { Authorization = new("Bearer", CreateToken("Admin")) }
        });
        unknown.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);

        var invalidCreate = new HttpRequestMessage(HttpMethod.Post, "/api/customers")
        {
            Content = JsonContent.Create(new CreateCustomerRequest())
        };
        invalidCreate.Headers.Authorization = new("Bearer", CreateToken("Admin"));
        var invalidResponse = await _client.SendAsync(invalidCreate);
        invalidResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        (await invalidResponse.Content.ReadAsStringAsync()).Should().Contain("fullName");

        var customerId = Guid.NewGuid();
        var pointRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/customers/{customerId}/loyalty-points");
        pointRequest.Headers.Authorization = new("Bearer", CreateToken("Admin"));
        (await _client.SendAsync(pointRequest)).StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var ordersRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/customers/{customerId}/orders");
        ordersRequest.Headers.Authorization = new("Bearer", CreateToken("Admin"));
        (await _client.SendAsync(ordersRequest)).StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        (await _client.GetAsync($"/api/customers/{customerId}/purchased-items")).StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
        var salesItemsRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/customers/{customerId}/purchased-items");
        salesItemsRequest.Headers.Authorization = new("Bearer", CreateToken("SalesStaff"));
        (await _client.SendAsync(salesItemsRequest)).StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        var adminItemsRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/customers/{customerId}/purchased-items");
        adminItemsRequest.Headers.Authorization = new("Bearer", CreateToken("Admin"));
        (await _client.SendAsync(adminItemsRequest)).StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/customers") { Content = JsonContent.Create(new CreateCustomerRequest { FullName = "API Test", Phone = "123" }) };
        createRequest.Headers.Authorization = new("Bearer", CreateToken("Admin"));
        var createResponse = await _client.SendAsync(createRequest);
        createResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        createResponse.Headers.Location!.ToString().Should().Contain("/api/customers/");

        var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/customers/{customerId}") { Content = JsonContent.Create(new UpdateCustomerRequest { FullName = "API Test", Phone = "123" }) };
        updateRequest.Headers.Authorization = new("Bearer", CreateToken("Admin"));
        (await _client.SendAsync(updateRequest)).StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var deactivateRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/customers/{customerId}/deactivate");
        deactivateRequest.Headers.Authorization = new("Bearer", CreateToken("Admin"));
        (await _client.SendAsync(deactivateRequest)).StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);

        var activateRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/customers/{customerId}/activate");
        activateRequest.Headers.Authorization = new("Bearer", CreateToken("Admin"));
        (await _client.SendAsync(activateRequest)).StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);

        var forbiddenActivation = new HttpRequestMessage(HttpMethod.Patch, $"/api/customers/{customerId}/activate");
        forbiddenActivation.Headers.Authorization = new("Bearer", CreateToken("SalesStaff"));
        (await _client.SendAsync(forbiddenActivation)).StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);

        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/customers/{customerId}");
        deleteRequest.Headers.Authorization = new("Bearer", CreateToken("Admin"));
        (await _client.SendAsync(deleteRequest)).StatusCode.Should().Be(System.Net.HttpStatusCode.MethodNotAllowed);
    }

    private static string CreateToken(string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("TechStore_Default_Secret_Key_At_Least_32_Bytes_Long_2026!"));
        var token = new JwtSecurityToken(
            issuer: "TechStore.Api",
            audience: "TechStore.Client",
            claims: [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, role)],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public sealed class CustomerApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICustomerService>();
            services.AddSingleton<ICustomerService, TestCustomerService>();
        });
    }
}

public sealed class TestCustomerService : ICustomerService
{
    public Task<CustomerOperationResult<PagedResultDto<CustomerSummaryDto>>> SearchAsync(CustomerSearchRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(CustomerOperationResult<PagedResultDto<CustomerSummaryDto>>.Success(new([], 1, 20, 0, 0)));
    public Task<CustomerOperationResult<CustomerDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(CustomerOperationResult<CustomerDto>.NotFound());
    public Task<CustomerOperationResult<PagedResultDto<CustomerLoyaltyPointDto>>> GetLoyaltyPointsAsync(Guid id, int? page, int? pageSize, CancellationToken cancellationToken = default) => Task.FromResult(CustomerOperationResult<PagedResultDto<CustomerLoyaltyPointDto>>.Success(new([], 1, 20, 0, 0)));
    public Task<CustomerOperationResult<PagedResultDto<OrderSummaryDto>>> GetOrdersAsync(Guid id, int? page, int? pageSize, CancellationToken cancellationToken = default) => Task.FromResult(CustomerOperationResult<PagedResultDto<OrderSummaryDto>>.Success(new([], 1, 20, 0, 0)));
    public Task<CustomerOperationResult<PagedResultDto<CustomerPurchasedItemDto>>> GetPurchasedItemsAsync(Guid id, int? page, int? pageSize, CancellationToken cancellationToken = default) => Task.FromResult(CustomerOperationResult<PagedResultDto<CustomerPurchasedItemDto>>.Success(new([], 1, 20, 0, 0)));
    public Task<CustomerOperationResult<CustomerDto>> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(string.IsNullOrWhiteSpace(request.FullName)
            ? CustomerOperationResult<CustomerDto>.Invalid(new() { ["fullName"] = ["Full name is required."] })
            : CustomerOperationResult<CustomerDto>.Success(new CustomerDto { Id = Guid.NewGuid(), FullName = request.FullName, Phone = request.Phone ?? "" }));
    public Task<CustomerOperationResult<CustomerDto>> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken = default) => Task.FromResult(CustomerOperationResult<CustomerDto>.Success(new CustomerDto { Id = id, FullName = request.FullName ?? "", Phone = request.Phone ?? "" }));
    public Task<CustomerOperationResult<bool>> DeactivateAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(CustomerOperationResult<bool>.Success(true));
    public Task<CustomerOperationResult<bool>> ActivateAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(CustomerOperationResult<bool>.Success(true));
}
