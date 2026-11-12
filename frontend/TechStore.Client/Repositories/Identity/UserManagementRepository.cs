using System.Globalization;
using TechStore.Client.Models.Identity;
using TechStore.Client.Services.Api;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.Repositories.Identity;

public sealed class UserManagementRepository(IApiClient api) : IUserManagementRepository
{
    public async Task<UserAccountSearchResult> SearchAsync(UserAccountQuery query, CancellationToken cancellationToken = default)
    {
        var parts = new List<string> { "page=" + Math.Max(1, query.Page).ToString(CultureInfo.InvariantCulture),
            "pageSize=" + Math.Clamp(query.PageSize, 1, 100).ToString(CultureInfo.InvariantCulture) };
        if (!string.IsNullOrWhiteSpace(query.Keyword)) parts.Add("q=" + Uri.EscapeDataString(query.Keyword.Trim()));
        if (query.Role is not null) parts.Add("role=" + Uri.EscapeDataString(query.Role));
        if (query.IsActive.HasValue) parts.Add("isActive=" + (query.IsActive.Value ? "true" : "false"));
        var response = await api.GetAsync<PagedResultDto<UserAccountDto>>("api/users?" + string.Join("&", parts), cancellationToken);
        return new(response.Items.Select(Map).ToList(), response.Page, response.TotalPages, response.TotalCount);
    }

    public async Task<UserAccount> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default) =>
        Map(await api.PostAsync<CreateUserRequest, UserAccountDto>("api/users", request, cancellationToken));
    public async Task<UserAccount> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default) =>
        Map(await api.PutAsync<UpdateUserRequest, UserAccountDto>($"api/users/{id:D}", request, cancellationToken));
    public async Task<UserAccount> SetStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default) =>
        Map(await api.PatchAsync<UpdateUserStatusRequest, UserAccountDto>($"api/users/{id:D}/status", new() { IsActive = isActive }, cancellationToken));
    public async Task<UserAccount> SetRoleAsync(Guid id, string role, CancellationToken cancellationToken = default) =>
        Map(await api.PutAsync<UpdateUserRoleRequest, UserAccountDto>($"api/users/{id:D}/role", new() { Role = role }, cancellationToken));
    public Task ResetPasswordAsync(Guid id, string password, CancellationToken cancellationToken = default) =>
        api.PostAsync($"api/users/{id:D}/reset-password", new ResetUserPasswordRequest { Password = password }, cancellationToken);

    private static UserAccount Map(UserAccountDto dto) => new(dto.Id, dto.Username, dto.FullName, dto.Email, dto.Role, dto.IsActive, dto.CreatedAt, dto.UpdatedAt);
}
