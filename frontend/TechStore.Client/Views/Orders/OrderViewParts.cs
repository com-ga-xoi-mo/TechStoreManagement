using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TechStore.Client.Styles;
using Uno.Extensions.Markup;

namespace TechStore.Client.Views.Orders;

/// <summary>Small building blocks shared by the Orders views. They declare UI only; no logic lives here.</summary>
internal static class OrderViewParts
{
    public static Visibility ShowIf(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    public static TextBlock HeaderText(string text) =>
        new TextBlock()
            .Style(AppResources.GetStyle(AppTypography.CaptionText))
            .Foreground(AppColors.Brushes.SecondaryText)
            .FontWeight(Microsoft.UI.Text.FontWeights.SemiBold)
            .Text(text);

    public static TextBlock Caption(string text) =>
        new TextBlock()
            .Style(AppResources.GetStyle(AppTypography.CaptionText))
            .Foreground(AppColors.Brushes.SecondaryText)
            .TextWrapping(TextWrapping.Wrap)
            .Text(text);
}
