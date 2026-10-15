using Microsoft.EntityFrameworkCore;
using TechStore.Api.Common.Data;
using TechStore.Api.Identity.Entities;
using TechStore.Shared.DTOs;
using TechStore.Shared.Requests;

namespace TechStore.Api.Identity.Services;

public class StoreSettingsService : IStoreSettingsService
{
    private readonly AppDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public StoreSettingsService(
        AppDbContext dbContext,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<StoreSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var setting = await _dbContext.StoreSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (setting == null)
        {
            return new StoreSettingsDto
            {
                Id = Guid.Empty,
                StoreName = "TechStore - Hệ thống Bán lẻ Công nghệ & Phụ kiện",
                Address = "123 Nguyễn Huệ, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh",
                Phone = "028 3822 6868",
                Email = "contact@techstore.vn",
                TaxId = "0315892410",
                Description = "Hệ thống quản lý bán lẻ thiết bị công nghệ & phụ kiện cao cấp",
                IsActive = true,
                UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime
            };
        }

        return MapToDto(setting);
    }

    public async Task<StoreSettingsDto> UpdateSettingsAsync(UpdateStoreSettingsRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.StoreName))
        {
            throw new ArgumentException("Tên cửa hàng không được để trống.", nameof(request.StoreName));
        }

        if (string.IsNullOrWhiteSpace(request.Address))
        {
            throw new ArgumentException("Địa chỉ không được để trống.", nameof(request.Address));
        }

        if (string.IsNullOrWhiteSpace(request.Phone))
        {
            throw new ArgumentException("Số điện thoại hotline không được để trống.", nameof(request.Phone));
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var setting = await _dbContext.StoreSettings.FirstOrDefaultAsync(cancellationToken);

        if (setting == null)
        {
            setting = new StoreSetting
            {
                StoreName = request.StoreName.Trim(),
                Address = request.Address.Trim(),
                Phone = request.Phone.Trim(),
                Email = request.Email?.Trim() ?? string.Empty,
                TaxId = request.TaxId?.Trim() ?? string.Empty,
                Description = request.Description?.Trim() ?? string.Empty,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            _dbContext.StoreSettings.Add(setting);
        }
        else
        {
            setting.StoreName = request.StoreName.Trim();
            setting.Address = request.Address.Trim();
            setting.Phone = request.Phone.Trim();
            setting.Email = request.Email?.Trim() ?? string.Empty;
            setting.TaxId = request.TaxId?.Trim() ?? string.Empty;
            setting.Description = request.Description?.Trim() ?? string.Empty;
            setting.UpdatedAt = now;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToDto(setting);
    }

    private static StoreSettingsDto MapToDto(StoreSetting setting)
    {
        return new StoreSettingsDto
        {
            Id = setting.Id,
            StoreName = setting.StoreName,
            Address = setting.Address,
            Phone = setting.Phone,
            Email = setting.Email,
            TaxId = setting.TaxId,
            Description = setting.Description,
            IsActive = setting.IsActive,
            UpdatedAt = setting.UpdatedAt ?? setting.CreatedAt
        };
    }
}
