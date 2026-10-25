namespace TechStore.Client.Models.Catalog;

public sealed record CategoryOption(Guid Id, Guid? ParentId, string Name, string DisplayPath, bool IsActive = true)
{
    public string Label => IsActive ? DisplayPath : $"{DisplayPath} (đang ẩn)";
}
