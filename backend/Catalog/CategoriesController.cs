using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TechStore.Api.Catalog.Services;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

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
    [ProducesResponseType(typeof(IReadOnlyList<CategoryTreeNodeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTree(CancellationToken cancellationToken)
    {
        var result = await _categoryService.GetTreeAsync(cancellationToken);
        return Ok(result.Data);
    }

    [HttpPost]
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
}
