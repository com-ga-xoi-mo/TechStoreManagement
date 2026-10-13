using CommunityToolkit.Mvvm.ComponentModel;

namespace TechStore.Client.ViewModels.Placeholder;

public sealed partial class PlaceholderViewModel : ObservableObject
{
    [ObservableProperty]
    private string title = string.Empty;

    public void Initialize(string routeLabel)
    {
        Title = routeLabel;
    }
}
