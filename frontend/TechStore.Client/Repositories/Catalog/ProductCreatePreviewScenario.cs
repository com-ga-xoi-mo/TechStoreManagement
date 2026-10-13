namespace TechStore.Client.Repositories.Catalog;

public enum ProductCreatePreviewScenario
{
    Success,
    DuplicateSku,
    DuplicateBarcode,
    Error,
    Delayed
}
