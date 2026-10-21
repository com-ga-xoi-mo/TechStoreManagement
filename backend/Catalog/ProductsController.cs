using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TechStore.Api.Catalog.Services;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Catalog;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] ProductSearchRequest request, CancellationToken cancellationToken)
    {
        var result = await _productService.SearchAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ValidationProblem(ToModelState(result.ValidationErrors));
        }

        return Ok(result.Data);
    }

    [HttpGet("spec-keys")]
    public async Task<IActionResult> GetSpecKeys([FromQuery] Guid? categoryId, CancellationToken cancellationToken) =>
        Ok(await _productService.GetSpecKeysAsync(categoryId, cancellationToken));

    [HttpGet("brands")]
    public async Task<IActionResult> GetBrands(CancellationToken cancellationToken) =>
        Ok(await _productService.GetBrandsAsync(cancellationToken));

    [HttpGet("spec-definitions")]
    public async Task<IActionResult> GetSpecDefinitions(CancellationToken cancellationToken) =>
        Ok(await _productService.GetSpecDefinitionsAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _productService.GetByIdAsync(id, cancellationToken);
        if (!result.Found)
        {
            return NotFound();
        }

        return Ok(result.Data);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request, CancellationToken cancellationToken)
    {
        var result = await _productService.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.IsConflict)
            {
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Conflict",
                    detail: result.ConflictMessage ?? $"One or more SKUs are already in use: {string.Join(", ", result.ConflictingSkus ?? [])}.");
            }

            return ValidationProblem(ToModelState(result.ValidationErrors));
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductRequest request, CancellationToken cancellationToken)
    {
        return ToActionResult(await _productService.UpdateAsync(id, request, cancellationToken), dto => Ok(dto));
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] UpdateProductStatusRequest request, CancellationToken cancellationToken)
    {
        return ToActionResult(await _productService.SetStatusAsync(id, request, cancellationToken), dto => Ok(dto));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        return ToActionResult(await _productService.DeleteAsync(id, cancellationToken), _ => NoContent());
    }

    private static ModelStateDictionary ToModelState(Dictionary<string, string[]>? errors)
    {
        var modelState = new ModelStateDictionary();
        if (errors != null)
        {
            foreach (var (key, errList) in errors)
            {
                foreach (var err in errList)
                {
                    modelState.AddModelError(key, err);
                }
            }
        }
        return modelState;
    }

    private IActionResult ToActionResult(ProductCommandResult result, Func<ProductDto?, IActionResult> onSuccess)
    {
        return result.Status switch
        {
            ProductCommandStatus.NotFound => NotFound(),
            ProductCommandStatus.ValidationFailed => ValidationProblem(ToModelState(result.ValidationErrors)),
            ProductCommandStatus.Conflict => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict",
                detail: result.ConflictMessage),
            ProductCommandStatus.Success => onSuccess(result.Data),
            _ => throw new InvalidOperationException($"Unexpected command status '{result.Status}'.")
        };
    }
}
