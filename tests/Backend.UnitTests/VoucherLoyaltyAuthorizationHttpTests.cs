using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using TechStore.Api.Customers.Services;
using TechStore.Api.Orders.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;

namespace TechStore.UnitTests;

// M4 Vy: who may call the voucher and loyalty-summary endpoints, with the real controllers and JWT pipeline.
public class VoucherLoyaltyAuthorizationHttpTests(VoucherLoyaltyApiFactory factory) : IClassFixture<VoucherLoyaltyApiFactory>
{
    private const string Id = "00000000-0000-0000-0000-0000000000aa";
    private readonly HttpClient _client = factory.CreateClient();

    public static TheoryData<string, string, string, int> RoleMatrix()
    {
        var data = new TheoryData<string, string, string, int>();
        // Voucher management: Admin and Manager only.
        string[][] voucherEndpoints =
        [
            ["GET", "/api/vouchers"], ["GET", $"/api/vouchers/{Id}"], ["POST", "/api/vouchers"],
            ["PUT", $"/api/vouchers/{Id}"], ["PATCH", $"/api/vouchers/{Id}/activate"], ["PATCH", $"/api/vouchers/{Id}/deactivate"]
        ];
        var allowed = new[] { 200, 200, 201, 200, 200, 200 };
        for (var i = 0; i < voucherEndpoints.Length; i++)
        {
            foreach (var role in new[] { "Admin", "Manager" })
                data.Add(role, voucherEndpoints[i][0], voucherEndpoints[i][1], allowed[i]);
            foreach (var role in new[] { "SalesStaff", "WarehouseStaff" })
                data.Add(role, voucherEndpoints[i][0], voucherEndpoints[i][1], 403);
        }

        // Loyalty summary: the counter roles may read it, the warehouse may not.
        foreach (var role in new[] { "Admin", "Manager", "SalesStaff" })
            data.Add(role, "GET", $"/api/customers/{Id}/loyalty-summary", 200);
        data.Add("WarehouseStaff", "GET", $"/api/customers/{Id}/loyalty-summary", 403);
        return data;
    }

    [Theory]
    [MemberData(nameof(RoleMatrix))]
    public async Task Endpoint_RoleMatrix_ReturnsAgreedStatus(string role, string method, string path, int expected)
    {
        // Arrange
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST") request.Content = JsonContent.Create(new CreateVoucherRequest());
        if (method == "PUT") request.Content = JsonContent.Create(new UpdateVoucherRequest());
        request.Headers.Authorization = new("Bearer", CreateToken(role));

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        ((int)response.StatusCode).Should().Be(expected, $"{role} {method} {path}");
    }

    [Theory]
    [InlineData("GET", "/api/vouchers")]
    [InlineData("POST", "/api/vouchers")]
    [InlineData("PATCH", "/api/vouchers/00000000-0000-0000-0000-0000000000aa/deactivate")]
    [InlineData("GET", "/api/customers/00000000-0000-0000-0000-0000000000aa/loyalty-summary")]
    public async Task Endpoint_WithoutToken_Returns401(string method, string path)
    {
        // Act
        var response = await _client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateVoucher_InvalidInput_Returns400WithTheFieldName()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/vouchers")
        {
            Content = JsonContent.Create(new CreateVoucherRequest { Code = "ab" })
        };
        request.Headers.Authorization = new("Bearer", CreateToken("Admin"));

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("code");
    }

    [Fact]
    public async Task CreateVoucher_DuplicateCode_Returns409()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/vouchers")
        {
            Content = JsonContent.Create(new CreateVoucherRequest { Code = "DUPLICATE" })
        };
        request.Headers.Authorization = new("Bearer", CreateToken("Manager"));

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private string CreateToken(string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(VoucherLoyaltyApiFactory.TestKey));
        var token = new JwtSecurityToken(
            issuer: "TechStore.Api",
            audience: "TechStore.Client",
            claims: [new Claim(ClaimTypes.NameIdentifier, TestJwtAuthentication.AddIdentity(factory.Services, role).ToString()), new Claim(ClaimTypes.Role, role), new Claim("token_version", "0")],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public sealed class VoucherLoyaltyApiFactory : WebApplicationFactory<Program>
{
    internal static readonly string TestKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:SecretKey", TestKey);
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.AddTestIdentities();
            services.RemoveAll<ICustomerService>();
            services.AddSingleton<ICustomerService, TestCustomerService>();
            services.RemoveAll<IVoucherService>();
            services.AddSingleton<IVoucherService, TestVoucherService>();
            services.RemoveAll<ILoyaltyService>();
            services.AddSingleton<ILoyaltyService, TestLoyaltyService>();
        });
    }
}

// Answers like the real service would on the happy path; the point here is the pipeline in front of it.
public sealed class TestVoucherService : IVoucherService
{
    public Task<CustomerOperationResult<PagedResultDto<VoucherDto>>> SearchAsync(VoucherSearchRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(CustomerOperationResult<PagedResultDto<VoucherDto>>.Success(new([], 1, 20, 0, 0)));

    public Task<CustomerOperationResult<VoucherDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(CustomerOperationResult<VoucherDto>.Success(new VoucherDto { Id = id, Code = "SALE10" }));

    public Task<CustomerOperationResult<VoucherDto>> CreateAsync(CreateVoucherRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(request.Code switch
        {
            "DUPLICATE" => CustomerOperationResult<VoucherDto>.Conflict(),
            { Length: < 3 } => CustomerOperationResult<VoucherDto>.Invalid(new() { ["code"] = ["Code must be 3-32 characters."] }),
            _ => CustomerOperationResult<VoucherDto>.Success(new VoucherDto { Id = Guid.NewGuid(), Code = request.Code ?? "" })
        });

    public Task<CustomerOperationResult<VoucherDto>> UpdateAsync(Guid id, UpdateVoucherRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(CustomerOperationResult<VoucherDto>.Success(new VoucherDto { Id = id, Code = "SALE10" }));

    public Task<CustomerOperationResult<VoucherDto>> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default) =>
        Task.FromResult(CustomerOperationResult<VoucherDto>.Success(new VoucherDto { Id = id, Code = "SALE10", IsActive = isActive }));

    public Task<VoucherUseResult> ReserveAsync(Order order, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<VoucherUseResult> RedeemAsync(Guid orderId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<VoucherUseResult> ReleaseAsync(Guid orderId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

public sealed class TestLoyaltyService : ILoyaltyService
{
    public Task<LoyaltyChange> AwardForOrderAsync(Order order, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<LoyaltyChange> ReverseForReturnAsync(Order order, Guid returnId, decimal remainingGoodsAmount, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<CustomerOperationResult<LoyaltySummaryDto>> GetSummaryAsync(Guid customerId, RoleType role, CancellationToken cancellationToken = default) =>
        Task.FromResult(CustomerOperationResult<LoyaltySummaryDto>.Success(new LoyaltySummaryDto { CustomerId = customerId, Tier = "Silver" }));
}
