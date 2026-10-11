using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TechStore.Api.Catalog.Entities;
using TechStore.Api.Catalog.Repositories;
using TechStore.Api.Common.Data;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Catalog.Services;

public class CategoryService(
    ICategoryRepository categoryRepository,
    IUnitOfWork unitOfWork) : ICategoryService
{
    private static readonly Regex SlugRegex = new(@"^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled);

    public async Task<CategoryTreeResult> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        var activeCategories = await categoryRepository.GetActiveCategoriesAsync(cancellationToken);

        var nodes = activeCategories.Select(c => new CategoryTreeNodeDto
        {
            Id = c.Id,
            Name = c.Name,
            Slug = c.Slug,
            Description = c.Description,
            ParentId = c.ParentId,
            IsActive = c.IsActive,
            SubCategories = []
        }).ToDictionary(n => n.Id);

        var rootNodes = new List<CategoryTreeNodeDto>();

        foreach (var node in nodes.Values)
        {
            if (node.ParentId.HasValue && nodes.TryGetValue(node.ParentId.Value, out var parentNode))
            {
                parentNode.SubCategories.Add(node);
            }
            else
            {
                rootNodes.Add(node);
            }
        }

        rootNodes = rootNodes.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var root in rootNodes)
        {
            SortSubCategoriesRecursively(root);
        }

        return new CategoryTreeResult(true, rootNodes);
    }

    private static void SortSubCategoriesRecursively(CategoryTreeNodeDto parent)
    {
        parent.SubCategories = parent.SubCategories
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var sub in parent.SubCategories)
        {
            SortSubCategoriesRecursively(sub);
        }
    }

    public async Task<CreateCategoryResult> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["name"] = ["Category name is required."];
        }
        else if (request.Name.Trim().Length > 200)
        {
            errors["name"] = ["Category name cannot exceed 200 characters."];
        }

        string slug;
        if (!string.IsNullOrWhiteSpace(request.Slug))
        {
            slug = request.Slug.Trim().ToLowerInvariant();
            if (!SlugRegex.IsMatch(slug))
            {
                errors["slug"] = ["Slug must contain only lowercase alphanumeric characters separated by single hyphens."];
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.Name))
        {
            slug = GenerateSlug(request.Name);
            if (string.IsNullOrWhiteSpace(slug))
            {
                slug = Guid.NewGuid().ToString("N")[..8];
            }
        }
        else
        {
            slug = string.Empty;
        }

        if (request.ParentId.HasValue)
        {
            var parent = await categoryRepository.GetByIdAsync(request.ParentId.Value, cancellationToken);
            if (parent == null)
            {
                errors["parentId"] = [$"Parent category with ID '{request.ParentId.Value}' does not exist."];
            }
        }

        if (errors.Count > 0)
        {
            return CreateCategoryResult.ValidationFailed(errors);
        }

        var slugExists = await categoryRepository.ExistsBySlugAsync(slug, cancellationToken);
        if (slugExists)
        {
            return CreateCategoryResult.Conflict($"Category slug '{slug}' is already in use.");
        }

        var category = new Category
        {
            Name = request.Name!.Trim(),
            Slug = slug,
            Description = request.Description?.Trim(),
            ParentId = request.ParentId,
            IsActive = request.IsActive ?? true
        };

        categoryRepository.Add(category);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateKeyException ex)
        {
            return CreateCategoryResult.Conflict($"A category with conflicting unique key already exists. {ex.Message}");
        }

        var dto = new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Slug = category.Slug,
            Description = category.Description,
            ParentId = category.ParentId,
            IsActive = category.IsActive,
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt
        };

        return CreateCategoryResult.Success(dto);
    }

    public static string GenerateSlug(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var normalizedString = text.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalizedString)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        var clean = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        clean = clean.Replace('đ', 'd').Replace('Đ', 'd');
        clean = Regex.Replace(clean, @"[^a-z0-9\s-]", "");
        clean = Regex.Replace(clean, @"\s+", "-").Trim('-');
        return Regex.Replace(clean, @"-+", "-");
    }
}
