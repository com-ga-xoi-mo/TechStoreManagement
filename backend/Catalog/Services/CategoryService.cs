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
        var categories = await categoryRepository.GetActiveCategoriesAsync(cancellationToken);

        var nodes = categories.Select(c => new CategoryTreeNodeDto
        {
            Id = c.Id,
            Name = c.Name,
            Slug = c.Slug,
            Description = c.Description,
            ParentId = c.ParentId,
            IsActive = c.IsActive,
            ProductCount = c.Products.Count,
            SkuCount = c.Products.Sum(p => p.Variants.Count == 0 ? 1 : p.Variants.Count),
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
            AggregateProductCounts(root);
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

    // Parent rows represent the complete branch in the workspace. Their product
    // count therefore includes products assigned directly to the category and to
    // every descendant, while child rows still use their own SKU count column.
    private static int AggregateProductCounts(CategoryTreeNodeDto node)
    {
        var total = node.ProductCount ?? 0;
        foreach (var child in node.SubCategories)
            total += AggregateProductCounts(child);
        node.ProductCount = total;
        return total;
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

    public async Task<UpdateCategoryResult> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        Category? category = null;
        string? originalName = null;
        string? originalSlug = null;
        Guid? originalParentId = null;
        string? originalDescription = null;
        bool originalIsActive = false;
        DateTime? originalUpdatedAt = null;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await categoryRepository.AcquireHierarchyUpdateLockAsync(cancellationToken);
            category = await categoryRepository.GetTrackedByIdAsync(id, cancellationToken);
            if (category is null)
            {
                await unitOfWork.RollbackAsync(CancellationToken.None);
                return UpdateCategoryResult.NotFound();
            }

            originalName = category.Name;
            originalSlug = category.Slug;
            originalParentId = category.ParentId;
            originalDescription = category.Description;
            originalIsActive = category.IsActive;
            originalUpdatedAt = category.UpdatedAt;

            var errors = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(request.Name))
                errors["name"] = ["Category name is required."];
            else if (request.Name.Trim().Length > 200)
                errors["name"] = ["Category name cannot exceed 200 characters."];
            if (request.IsActive is null)
                errors["isActive"] = ["IsActive is required."];

            var name = request.Name?.Trim() ?? category.Name;
            var slug = string.IsNullOrWhiteSpace(request.Slug) ? GenerateSlug(name) : request.Slug.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(slug) || !SlugRegex.IsMatch(slug))
                errors["slug"] = ["Slug must contain only lowercase alphanumeric characters separated by single hyphens."];

            if (request.ParentId == id)
                errors["parentId"] = ["A category cannot be its own parent."];
            else if (request.ParentId.HasValue)
            {
                var parent = await categoryRepository.GetByIdAsync(request.ParentId.Value, cancellationToken);
                if (parent is null)
                    errors["parentId"] = [$"Parent category with ID '{request.ParentId.Value}' does not exist."];
                else if ((await categoryRepository.GetDescendantIdsAsync(id, cancellationToken)).Contains(request.ParentId.Value))
                    errors["parentId"] = ["A category cannot be moved under one of its descendants."];
            }

            if (errors.Count > 0)
            {
                await unitOfWork.RollbackAsync(CancellationToken.None);
                return UpdateCategoryResult.ValidationFailed(errors);
            }

            if (await categoryRepository.ExistsBySlugExceptIdAsync(slug, id, cancellationToken))
            {
                await unitOfWork.RollbackAsync(CancellationToken.None);
                return UpdateCategoryResult.Conflict($"Category slug '{slug}' is already in use.");
            }

            category.Name = name;
            category.Slug = slug;
            category.ParentId = request.ParentId;
            category.Description = request.Description is null ? category.Description : string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            category.IsActive = request.IsActive!.Value;
            category.UpdatedAt = DateTime.UtcNow;

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            return UpdateCategoryResult.Success(Map(category));
        }
        catch (DuplicateKeyException)
        {
            RestoreCategory(category, originalName, originalSlug, originalParentId, originalDescription, originalIsActive, originalUpdatedAt);
            await unitOfWork.RollbackAsync(CancellationToken.None);
            return UpdateCategoryResult.Conflict("A category with the same slug already exists.");
        }
        catch
        {
            RestoreCategory(category, originalName, originalSlug, originalParentId, originalDescription, originalIsActive, originalUpdatedAt);
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<DeleteCategoryResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await categoryRepository.AcquireHierarchyUpdateLockAsync(cancellationToken);
            var category = await categoryRepository.GetTrackedByIdAsync(id, cancellationToken);
            if (category is null)
            {
                await unitOfWork.RollbackAsync(CancellationToken.None);
                return DeleteCategoryResult.NotFound();
            }

            if (await categoryRepository.HasSubCategoriesAsync(id, cancellationToken))
            {
                await unitOfWork.RollbackAsync(CancellationToken.None);
                return DeleteCategoryResult.ValidationFailed(new Dictionary<string, string[]>
                {
                    ["category"] = ["Cannot delete category that contains subcategories. Move or delete subcategories first."]
                });
            }

            if (await categoryRepository.HasProductsAsync(id, cancellationToken))
            {
                await unitOfWork.RollbackAsync(CancellationToken.None);
                return DeleteCategoryResult.Conflict("Cannot delete category that contains associated products. Reassign or delete associated products first.");
            }

            categoryRepository.Delete(category);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            return DeleteCategoryResult.Success();
        }
        catch (ForeignKeyViolationException)
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            return DeleteCategoryResult.Conflict("Cannot delete category because it is still referenced by related data.");
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static CategoryDto Map(Category category) => new()
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

    private static void RestoreCategory(
        Category? category,
        string? name,
        string? slug,
        Guid? parentId,
        string? description,
        bool isActive,
        DateTime? updatedAt)
    {
        if (category is null || name is null || slug is null) return;
        category.Name = name;
        category.Slug = slug;
        category.ParentId = parentId;
        category.Description = description;
        category.IsActive = isActive;
        category.UpdatedAt = updatedAt;
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
