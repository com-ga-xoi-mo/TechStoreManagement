using TechStore.Shared.Enums;

namespace TechStore.Api.Sales.Repositories;

public record PosSessionSearchCriteria(
    PosSessionStatus? Status,
    Guid? CashierUserId,
    int Page,
    int PageSize);
