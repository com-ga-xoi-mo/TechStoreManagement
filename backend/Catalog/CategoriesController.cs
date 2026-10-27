using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TechStore.Api.Catalog.Services;
using TechStore.Shared.DTOs;
using TechStore.Shared.Enums;
using TechStore.Shared.Requests;
using Microsoft.AspNetCore.Authorization;

namespace TechStore.Api.Catalog;

[ApiController]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet("tree")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryTreeNodeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetTree(CancellationToken cancellationToken)
    {
        var result = await _categoryService.GetTreeAsync(cancellationToken);
        return Ok(result.Data);
    }
    [HttpPost]
    [Authorize(Roles = nameof(RoleType.Admin))]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _categoryService.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.IsConflict)
            {
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Conflict",
                    detail: result.ConflictReason ?? "A conflicting category already exists.");
            }

            var modelState = new ModelStateDictionary();
            if (result.ValidationErrors != null)
            {
                foreach (var (key, errors) in result.ValidationErrors)
                {
                    foreach (var err in errors)
                    {
                        modelState.AddModelError(key, err);
                    }
                }
            }
            return ValidationProblem(modelState);
        }

        return Created($"/api/categories/{result.Data!.Id}", result.Data);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = nameof(RoleType.Admin))]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _categoryService.UpdateAsync(id, request, cancellationToken);
        if (result.IsSuccess) return Ok(result.Data);
        if (result.IsNotFound)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Category not found", detail: "The requested category does not exist.");
        if (result.IsConflict)
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: result.ConflictReason);
        return ValidationProblem(ToModelState(result.ValidationErrors));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = nameof(RoleType.Admin))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await _categoryService.DeleteAsync(id, cancellationToken);
        if (result.IsSuccess) return NoContent();
        if (result.IsNotFound)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Category not found", detail: "The requested category does not exist.");
        if (result.IsConflict)
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: result.ConflictReason);
        return ValidationProblem(ToModelState(result.ValidationErrors));
    }

    private static ModelStateDictionary ToModelState(Dictionary<string, string[]>? errors)
    {
        var modelState = new ModelStateDictionary();
        if (errors is null) return modelState;
        foreach (var (key, messages) in errors)
            foreach (var message in messages)
                modelState.AddModelError(key, message);
        return modelState;
    }
}
