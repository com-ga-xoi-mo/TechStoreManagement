namespace TechStore.Client.Models.Catalog;

public sealed record ProductImage(Guid Id, Guid? VariantId, string Url, string ContentType, int SizeBytes, int SortOrder);
