using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TechStore.Client.Features.Categories.Models;
using TechStore.Client.Services.Authentication;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Client.Services.Api;

public sealed class CategoryApiClient(HttpClient httpClient, IAccessTokenProvider tokens) : ICategoryApiClient
{
    public async Task<IReadOnlyList<CategoryNode>> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        var token = await tokens.GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
            throw new CategoryApiException(CategoryApiFailure.SessionRequired);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/categories/tree");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new CategoryApiException(CategoryApiFailure.Unauthorized);
            if (response.StatusCode == HttpStatusCode.Forbidden)
                throw new CategoryApiException(CategoryApiFailure.Forbidden);
            if (!response.IsSuccessStatusCode)
                throw new CategoryApiException(CategoryApiFailure.Unavailable);

            var nodes = await response.Content.ReadFromJsonAsync<List<CategoryTreeNodeDto>>(cancellationToken);
            if (nodes is null)
                throw new CategoryApiException(CategoryApiFailure.InvalidResponse);
            return nodes.Select(Map).ToArray();
        }
        catch (CategoryApiException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            throw new CategoryApiException(CategoryApiFailure.Unavailable);
        }
        catch (JsonException)
        {
            throw new CategoryApiException(CategoryApiFailure.InvalidResponse);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CategoryApiException(CategoryApiFailure.Unavailable);
        }
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var token = await tokens.GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
            throw new CategoryApiException(CategoryApiFailure.SessionRequired);

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/categories")
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new CategoryApiException(CategoryApiFailure.Unauthorized);
            if (response.StatusCode == HttpStatusCode.Forbidden)
                throw new CategoryApiException(CategoryApiFailure.Forbidden);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                var problem = await TryReadProblemDetailsAsync(response, cancellationToken);
                throw new CategoryApiException(CategoryApiFailure.Conflict, problem ?? "Danh mục hoặc slug đã tồn tại.");
            }
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var problem = await TryReadProblemDetailsAsync(response, cancellationToken);
                throw new CategoryApiException(CategoryApiFailure.BadRequest, problem ?? "Thông tin danh mục không hợp lệ.");
            }
            if (!response.IsSuccessStatusCode)
                throw new CategoryApiException(CategoryApiFailure.Unavailable);

            var created = await response.Content.ReadFromJsonAsync<CategoryDto>(cancellationToken);
            if (created is null)
                throw new CategoryApiException(CategoryApiFailure.InvalidResponse);
            return created;
        }
        catch (CategoryApiException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            throw new CategoryApiException(CategoryApiFailure.Unavailable);
        }
        catch (JsonException)
        {
            throw new CategoryApiException(CategoryApiFailure.InvalidResponse);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CategoryApiException(CategoryApiFailure.Unavailable);
        }
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var token = await tokens.GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
            throw new CategoryApiException(CategoryApiFailure.SessionRequired);

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Put, $"api/categories/{id}")
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new CategoryApiException(CategoryApiFailure.Unauthorized);
            if (response.StatusCode == HttpStatusCode.Forbidden)
                throw new CategoryApiException(CategoryApiFailure.Forbidden);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                var problem = await TryReadProblemDetailsAsync(response, cancellationToken);
                throw new CategoryApiException(CategoryApiFailure.NotFound, problem ?? "Không tìm thấy danh mục cần sửa.");
            }
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                var problem = await TryReadProblemDetailsAsync(response, cancellationToken);
                throw new CategoryApiException(CategoryApiFailure.Conflict, problem ?? "Danh mục hoặc slug đã tồn tại.");
            }
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var problem = await TryReadProblemDetailsAsync(response, cancellationToken);
                throw new CategoryApiException(CategoryApiFailure.BadRequest, problem ?? "Thông tin danh mục không hợp lệ.");
            }
            if (!response.IsSuccessStatusCode)
                throw new CategoryApiException(CategoryApiFailure.Unavailable);

            var updated = await response.Content.ReadFromJsonAsync<CategoryDto>(cancellationToken);
            if (updated is null)
                throw new CategoryApiException(CategoryApiFailure.InvalidResponse);
            return updated;
        }
        catch (CategoryApiException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            throw new CategoryApiException(CategoryApiFailure.Unavailable);
        }
        catch (JsonException)
        {
            throw new CategoryApiException(CategoryApiFailure.InvalidResponse);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CategoryApiException(CategoryApiFailure.Unavailable);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var token = await tokens.GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
            throw new CategoryApiException(CategoryApiFailure.SessionRequired);

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"api/categories/{id}");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new CategoryApiException(CategoryApiFailure.Unauthorized);
            if (response.StatusCode == HttpStatusCode.Forbidden)
                throw new CategoryApiException(CategoryApiFailure.Forbidden);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                var problem = await TryReadProblemDetailsAsync(response, cancellationToken);
                throw new CategoryApiException(CategoryApiFailure.NotFound, problem ?? "Không tìm thấy danh mục cần xóa.");
            }
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                var problem = await TryReadProblemDetailsAsync(response, cancellationToken);
                throw new CategoryApiException(CategoryApiFailure.Conflict, problem ?? "Không thể xóa danh mục vì đang có sản phẩm liên kết.");
            }
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var problem = await TryReadProblemDetailsAsync(response, cancellationToken);
                throw new CategoryApiException(CategoryApiFailure.BadRequest, problem ?? "Không thể xóa danh mục vì vẫn còn danh mục con.");
            }
            if (response.StatusCode != HttpStatusCode.NoContent)
                throw new CategoryApiException(response.IsSuccessStatusCode
                    ? CategoryApiFailure.InvalidResponse
                    : CategoryApiFailure.Unavailable);
        }
        catch (CategoryApiException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            throw new CategoryApiException(CategoryApiFailure.Unavailable);
        }
        catch (JsonException)
        {
            throw new CategoryApiException(CategoryApiFailure.InvalidResponse);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CategoryApiException(CategoryApiFailure.Unavailable);
        }
    }

    private static async Task<string?> TryReadProblemDetailsAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = doc.RootElement;
            if (root.TryGetProperty("detail", out var detail) && detail.GetString() is { Length: > 0 } detailStr)
            {
                return detailStr;
            }
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var messages = new List<string>();
                foreach (var prop in errors.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var err in prop.Value.EnumerateArray())
                        {
                            if (err.GetString() is { Length: > 0 } msg)
                                messages.Add(msg);
                        }
                    }
                }
                if (messages.Count > 0)
                    return string.Join("; ", messages);
            }
            if (root.TryGetProperty("title", out var title) && title.GetString() is { Length: > 0 } titleStr)
            {
                return titleStr;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Ignore parse errors and fall back to default
        }
        return null;
    }

    private static CategoryNode Map(CategoryTreeNodeDto node) =>
        new(node.Id, node.Name, node.Slug, node.IsActive, node.SubCategories.Select(Map).ToArray(),
            ProductCount: node.ProductCount, SkuCount: node.SkuCount,
            Description: node.Description, ParentId: node.ParentId);
}
