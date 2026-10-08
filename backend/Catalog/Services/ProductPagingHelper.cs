namespace TechStore.Api.Catalog.Services;

public static class ProductPagingHelper
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MinPageSize = 1;
    public const int MaxPageSize = 100;

    public static (int page, int pageSize) Normalize(int? requestedPage, int? requestedPageSize)
    {
        var page = !requestedPage.HasValue || requestedPage.Value < 1 ? DefaultPage : requestedPage.Value;
        int pageSize;
        if (!requestedPageSize.HasValue)
        {
            pageSize = DefaultPageSize;
        }
        else if (requestedPageSize.Value < MinPageSize)
        {
            pageSize = MinPageSize;
        }
        else if (requestedPageSize.Value > MaxPageSize)
        {
            pageSize = MaxPageSize;
        }
        else
        {
            pageSize = requestedPageSize.Value;
        }

        return (page, pageSize);
    }

    public static int CalculateTotalPages(int totalCount, int pageSize)
    {
        if (totalCount <= 0 || pageSize <= 0)
        {
            return 0;
        }

        return (int)Math.Ceiling((double)totalCount / pageSize);
    }
}
