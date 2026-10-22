namespace TechStore.Client.Models.Catalog;

public sealed record ImageUpload(string FileName, string ContentType, byte[] Content, Guid? VariantId);
