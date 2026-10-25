namespace TechStore.Client.Features.Categories.Services;

public enum CategoryDialogAction { Add, Edit, Delete }

public sealed record CategoryDialogRequest(CategoryDialogAction Action, Guid? CategoryId);

public interface ICategoryDialogRequests
{
    void Request(CategoryDialogRequest request);
}

/// <summary>UI hosts subscribe only while attached; later steps supply the detailed forms.</summary>
public sealed class CategoryDialogRequests : ICategoryDialogRequests
{
    public event EventHandler<CategoryDialogRequest>? Requested;

    public void Request(CategoryDialogRequest request) => Requested?.Invoke(this, request);
}
