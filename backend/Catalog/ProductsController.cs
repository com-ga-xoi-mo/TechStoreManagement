using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TechStore.Api.Catalog.Services;
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

        return Ok(result.Data);
    }

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
                    detail: $"One or more SKUs are already in use: {string.Join(", ", result.ConflictingSkus ?? [])}.");
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

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }
}
