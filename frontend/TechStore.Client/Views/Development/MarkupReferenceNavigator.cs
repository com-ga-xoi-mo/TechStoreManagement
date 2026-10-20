using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using TechStore.Client.Services.Development;

namespace TechStore.Client.Views.Development;

/// <summary>
/// Swaps the host window content between the component gallery and the C# Markup reference specimen.
/// Lives in Views because it touches WinUI types; the contract is in Services/Development.
/// </summary>
public sealed class MarkupReferenceNavigator(IServiceProvider services) : IMarkupReferenceNavigator
{
    /// <summary>The window whose content is swapped. Set by <c>App</c> once the window exists.</summary>
    public Window? Host { get; set; }

    public void ShowReference()
    {
        if (Host is null) return;
        Host.Content = services.GetRequiredService<MarkupReferencePage>();
    }

    // Always a new gallery: its page ViewModels (PageViewModel) activate once and cannot be re-activated after
    // Unloaded, so the previous instance must not be reused. Gallery sample state therefore resets on return.
    public void ShowGallery()
    {
        if (Host is null) return;
        Host.Content = services.GetRequiredService<ComponentGalleryPage>();
    }
}
