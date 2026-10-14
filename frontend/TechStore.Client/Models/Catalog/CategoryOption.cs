namespace TechStore.Client.Models.Catalog;

public sealed record CategoryOption(Guid Id, Guid? ParentId, string Name, string DisplayPath);
