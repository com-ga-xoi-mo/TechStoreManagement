using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TechStore.Api.Inventory.Services;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Inventory;

[ApiController]
[Route("api/inventory/suppliers")]
[Authorize]
public class SuppliersController(ISupplierService supplierService) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Admin,Manager,WarehouseStaff")]
    [ProducesResponseType(typeof(PagedResultDto<SupplierDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSuppliers(
        [FromQuery] SupplierFilterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await supplierService.SearchAsync(request, cancellationToken);
        return Ok(result.Data);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin,Manager,WarehouseStaff")]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSupplierById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await supplierService.GetByIdAsync(id, cancellationToken);
        if (result.IsNotFound)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Supplier not found",
                detail: "Nhà cung cấp không tồn tại.");
        }

        return Ok(result.Data);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,WarehouseStaff")]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateSupplier(
        [FromBody] SaveSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var result = await supplierService.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.IsConflict)
            {
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Conflict",
                    detail: result.ConflictReason ?? "Xung đột nhà cung cấp.");
            }

            return ValidationProblem(BuildModelState(result.ValidationErrors));
        }

        return Created($"/api/inventory/suppliers/{result.Data!.Id}", result.Data);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,WarehouseStaff")]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateSupplier(
        Guid id,
        [FromBody] SaveSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var result = await supplierService.UpdateAsync(id, request, cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.IsNotFound)
            {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Supplier not found",
                    detail: "Nhà cung cấp không tồn tại.");
            }

            if (result.IsConflict)
            {
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Conflict",
                    detail: result.ConflictReason ?? "Xung đột nhà cung cấp.");
            }

            return ValidationProblem(BuildModelState(result.ValidationErrors));
        }

        return Ok(result.Data);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Admin,WarehouseStaff")]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SetSupplierStatus(
        Guid id,
        [FromBody] SetSupplierStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await supplierService.SetStatusAsync(id, request, cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.IsNotFound)
            {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Supplier not found",
                    detail: "Nhà cung cấp không tồn tại.");
            }

            if (result.IsConflict)
            {
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Conflict",
                    detail: result.ConflictReason ?? "Xung đột nhà cung cấp.");
            }

            return ValidationProblem(BuildModelState(result.ValidationErrors));
        }

        return Ok(result.Data);
    }

    private static ModelStateDictionary BuildModelState(Dictionary<string, string[]>? errors)
    {
        var modelState = new ModelStateDictionary();
        if (errors == null) return modelState;

        foreach (var (key, errList) in errors)
        {
            foreach (var err in errList)
            {
                modelState.AddModelError(key, err);
            }
        }

        return modelState;
    }
}
