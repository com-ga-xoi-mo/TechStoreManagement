namespace TechStore.Client.Repositories.Catalog;

public enum ProductEditPreviewScenario
{
    Success,
    DelayedLoad,
    DelayedSave,
    NotFound,
    LoadError,
    SaveError,
    DuplicateSku,
    DuplicateBarcode,
    VariantHasTransactions,
    ConcurrentUpdate
}
