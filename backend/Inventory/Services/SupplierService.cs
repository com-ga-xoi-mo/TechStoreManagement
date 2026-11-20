using System.Net.Mail;
using System.Text.RegularExpressions;
using TechStore.Api.Common.Data;
using TechStore.Api.Inventory.Entities;
using TechStore.Api.Inventory.Repositories;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Inventory.Services;

public partial class SupplierService(
    ISupplierRepository supplierRepository,
    IUnitOfWork unitOfWork,
    TimeProvider? timeProvider = null) : ISupplierService
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    [GeneratedRegex(@"^[0-9 +().-]{1,30}$")]
    private static partial Regex PhoneRegex();

    public async Task<SupplierResult<PagedResultDto<SupplierDto>>> SearchAsync(
        SupplierFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var page = request.Page.GetValueOrDefault(1);
        if (page < 1) page = 1;

        var pageSize = request.PageSize.GetValueOrDefault(20);
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var criteria = new SupplierSearchCriteria(
            request.Search,
            request.IsActive,
            page,
            pageSize);

        var (items, totalCount) = await supplierRepository.SearchAsync(criteria, cancellationToken);
        var totalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 0;

        var dtos = items.Select(x => MapToDto(x.Supplier, x.ReceiptCount)).ToList();
        return SupplierResult<PagedResultDto<SupplierDto>>.Success(
            new PagedResultDto<SupplierDto>(dtos, page, pageSize, totalCount, totalPages));
    }

    public async Task<SupplierResult<SupplierDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var view = await supplierRepository.GetViewByIdAsync(id, cancellationToken);
        if (view == null || view.Supplier.Name == SystemSuppliers.OpeningBalanceName)
        {
            return SupplierResult<SupplierDto>.NotFound();
        }

        return SupplierResult<SupplierDto>.Success(MapToDto(view.Supplier, view.ReceiptCount));
    }

    public async Task<SupplierResult<SupplierDto>> CreateAsync(
        SaveSupplierRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidateSaveRequest(request, out var trimmedName, out var trimmedPhone, out var trimmedContact, out var trimmedEmail, out var trimmedAddress);
        if (errors.Count > 0)
        {
            return SupplierResult<SupplierDto>.ValidationFailed(errors);
        }

        if (trimmedName.Equals(SystemSuppliers.OpeningBalanceName, StringComparison.OrdinalIgnoreCase))
        {
            return SupplierResult<SupplierDto>.Conflict("Không thể sử dụng tên nhà cung cấp hệ thống.");
        }

        if (await supplierRepository.NameExistsAsync(trimmedName, null, cancellationToken))
        {
            return SupplierResult<SupplierDto>.Conflict("Tên nhà cung cấp đã tồn tại.");
        }

        var supplier = new Supplier
        {
            Name = trimmedName,
            Phone = trimmedPhone,
            ContactName = trimmedContact,
            Email = trimmedEmail,
            Address = trimmedAddress,
            IsActive = true,
            CreatedAt = _time.GetUtcNow().UtcDateTime
        };

        supplierRepository.Add(supplier);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            return SupplierResult<SupplierDto>.Conflict("Tên nhà cung cấp đã tồn tại.");
        }

        return SupplierResult<SupplierDto>.Success(MapToDto(supplier, 0));
    }

    public async Task<SupplierResult<SupplierDto>> UpdateAsync(
        Guid id,
        SaveSupplierRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidateSaveRequest(request, out var trimmedName, out var trimmedPhone, out var trimmedContact, out var trimmedEmail, out var trimmedAddress);
        if (errors.Count > 0)
        {
            return SupplierResult<SupplierDto>.ValidationFailed(errors);
        }

        var existing = await supplierRepository.GetTrackedByIdAsync(id, cancellationToken);
        if (existing == null)
        {
            return SupplierResult<SupplierDto>.NotFound();
        }

        if (existing.Name == SystemSuppliers.OpeningBalanceName)
        {
            return SupplierResult<SupplierDto>.Conflict("Không thể chỉnh sửa nhà cung cấp hệ thống.");
        }

        if (trimmedName.Equals(SystemSuppliers.OpeningBalanceName, StringComparison.OrdinalIgnoreCase))
        {
            return SupplierResult<SupplierDto>.Conflict("Không thể sử dụng tên nhà cung cấp hệ thống.");
        }

        if (await supplierRepository.NameExistsAsync(trimmedName, id, cancellationToken))
        {
            return SupplierResult<SupplierDto>.Conflict("Tên nhà cung cấp đã tồn tại.");
        }

        existing.Name = trimmedName;
        existing.Phone = trimmedPhone;
        existing.ContactName = trimmedContact;
        existing.Email = trimmedEmail;
        existing.Address = trimmedAddress;
        existing.UpdatedAt = _time.GetUtcNow().UtcDateTime;

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            return SupplierResult<SupplierDto>.Conflict("Tên nhà cung cấp đã tồn tại.");
        }

        var view = await supplierRepository.GetViewByIdAsync(id, cancellationToken);
        var receiptCount = view?.ReceiptCount ?? existing.PurchaseOrders?.Count ?? 0;

        return SupplierResult<SupplierDto>.Success(MapToDto(existing, receiptCount));
    }

    public async Task<SupplierResult<SupplierDto>> SetStatusAsync(
        Guid id,
        SetSupplierStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.IsActive.HasValue)
        {
            return SupplierResult<SupplierDto>.ValidationFailed(new Dictionary<string, string[]>
            {
                ["isActive"] = ["Trạng thái hoạt động là bắt buộc."]
            });
        }

        var existing = await supplierRepository.GetTrackedByIdAsync(id, cancellationToken);
        if (existing == null)
        {
            return SupplierResult<SupplierDto>.NotFound();
        }

        if (existing.Name == SystemSuppliers.OpeningBalanceName)
        {
            return SupplierResult<SupplierDto>.Conflict("Không thể thay đổi trạng thái nhà cung cấp hệ thống.");
        }

        existing.IsActive = request.IsActive.Value;
        existing.UpdatedAt = _time.GetUtcNow().UtcDateTime;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var view = await supplierRepository.GetViewByIdAsync(id, cancellationToken);
        var receiptCount = view?.ReceiptCount ?? existing.PurchaseOrders?.Count ?? 0;

        return SupplierResult<SupplierDto>.Success(MapToDto(existing, receiptCount));
    }

    private static Dictionary<string, string[]> ValidateSaveRequest(
        SaveSupplierRequest request,
        out string trimmedName,
        out string trimmedPhone,
        out string? trimmedContact,
        out string? trimmedEmail,
        out string? trimmedAddress)
    {
        var errors = new Dictionary<string, string[]>();
        trimmedName = (request.Name ?? string.Empty).Trim();
        trimmedPhone = (request.Phone ?? string.Empty).Trim();
        trimmedContact = string.IsNullOrWhiteSpace(request.ContactName) ? null : request.ContactName.Trim();
        trimmedEmail = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        trimmedAddress = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();

        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            errors["name"] = ["Tên nhà cung cấp là bắt buộc."];
        }
        else if (trimmedName.Length > 200)
        {
            errors["name"] = ["Tên nhà cung cấp không được vượt quá 200 ký tự."];
        }

        if (string.IsNullOrWhiteSpace(trimmedPhone))
        {
            errors["phone"] = ["Số điện thoại là bắt buộc."];
        }
        else if (trimmedPhone.Length > 30)
        {
            errors["phone"] = ["Số điện thoại không được vượt quá 30 ký tự."];
        }
        else if (!PhoneRegex().IsMatch(trimmedPhone))
        {
            errors["phone"] = ["Số điện thoại không hợp lệ."];
        }

        if (trimmedContact != null && trimmedContact.Length > 200)
        {
            errors["contactName"] = ["Tên người liên hệ không được vượt quá 200 ký tự."];
        }

        if (trimmedAddress != null && trimmedAddress.Length > 500)
        {
            errors["address"] = ["Địa chỉ không được vượt quá 500 ký tự."];
        }

        if (trimmedEmail != null)
        {
            if (trimmedEmail.Length > 200)
            {
                errors["email"] = ["Email không được vượt quá 200 ký tự."];
            }
            else if (!trimmedEmail.Contains('@') || !MailAddress.TryCreate(trimmedEmail, out var mail) || mail.Address != trimmedEmail)
            {
                errors["email"] = ["Email không hợp lệ."];
            }
        }

        return errors;
    }

    private static SupplierDto MapToDto(Supplier s, int receiptCount) =>
        new()
        {
            Id = s.Id,
            Name = s.Name,
            ContactName = s.ContactName,
            Phone = s.Phone,
            Email = s.Email,
            Address = s.Address,
            IsActive = s.IsActive,
            ReceiptCount = receiptCount,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt
        };
}
