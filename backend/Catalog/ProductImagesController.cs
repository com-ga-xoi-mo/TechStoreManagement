using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TechStore.Api.Catalog.Services;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Catalog;

[ApiController]
[Route("api/products/{productId:guid}/images")]
[Authorize(Roles = "Admin,Manager")]
public sealed class ProductImagesController(IProductImageService service) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    [ProducesResponseType(typeof(ProductImageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Upload(Guid productId, IFormFile? file, [FromForm] Guid? variantId, CancellationToken cancellationToken)
    {
        if (file == null) return FileError("The image file is required.");
        if (file.Length > ProductImageFileInspector.MaxBytes) return FileError("The image file cannot exceed 5 MB.");
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        var result = await service.UploadAsync(productId, new(file.FileName, buffer.ToArray(), variantId), cancellationToken);
        return ToActionResult(result, dto => CreatedAtAction(nameof(ProductsController.GetById), "Products", new { id = productId }, dto));
    }

    [HttpDelete("{imageId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid productId, Guid imageId, CancellationToken cancellationToken) =>
        ToActionResult(await service.DeleteAsync(productId, imageId, cancellationToken), _ => NoContent());

    [HttpPut("layout")]
    [ProducesResponseType(typeof(List<ProductImageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SaveLayout(Guid productId, [FromBody] UpdateProductImagesLayoutRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await service.SaveLayoutAsync(productId, request, cancellationToken), dto => Ok(dto));

    private IActionResult FileError(string message) => ValidationProblem(ToModelState(new() { ["file"] = [message] }));

    private IActionResult ToActionResult<T>(ProductImageCommandResult<T> result, Func<T?, IActionResult> success) => result.Status switch
    {
        ProductImageCommandStatus.Success => success(result.Data),
        ProductImageCommandStatus.NotFound => Problem(statusCode: 404, title: "Not found", detail: "Product or image not found."),
        ProductImageCommandStatus.ValidationFailed => ValidationProblem(ToModelState(result.ValidationErrors)),
        ProductImageCommandStatus.Conflict => Problem(statusCode: 409, title: "Conflict", detail: result.Message),
        ProductImageCommandStatus.StorageUnavailable => Problem(statusCode: 503, title: "Service unavailable", detail: result.Message),
        _ => throw new InvalidOperationException($"Unexpected image command status '{result.Status}'.")
    };

    private static ModelStateDictionary ToModelState(Dictionary<string, string[]>? errors)
    {
        var state = new ModelStateDictionary();
        foreach (var (key, messages) in errors ?? [])
            foreach (var message in messages) state.AddModelError(key, message);
        return state;
    }
}
