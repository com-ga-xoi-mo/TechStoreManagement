using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace TechStore.Client.Styles;

public static partial class AppIcons
{
    /// <summary>
    /// Returns the registered icon instance, e.g. <c>.IconSource(AppIcons.Get(AppIcons.EmptyIcon))</c>.
    /// Pass the instance itself: a Markup <c>StaticResource</c> builder for an IconSource made the first
    /// layout of the Catalog pages take about 8.5 s on Skia (measured on the product list).
    /// </summary>
    public static IconSource Get(string key) =>
        Application.Current.Resources.TryGetValue(key, out var value) && value is IconSource icon
            ? icon
            : throw new KeyNotFoundException($"Icon resource '{key}' is not registered.");
}
