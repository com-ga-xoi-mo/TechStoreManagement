using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Uno.Extensions.Markup;

namespace TechStore.Client.Styles;

public static partial class AppIcons
{
    public static class Sources
    {
        public static Action<IDependencyPropertyBuilder<IconSource>> MoneyIcon => StaticResource.Get<IconSource>(AppIcons.MoneyIcon);
        public static Action<IDependencyPropertyBuilder<IconSource>> BoxIcon => StaticResource.Get<IconSource>(AppIcons.BoxIcon);
        public static Action<IDependencyPropertyBuilder<IconSource>> WarningIcon => StaticResource.Get<IconSource>(AppIcons.WarningIcon);
        public static Action<IDependencyPropertyBuilder<IconSource>> BagIcon => StaticResource.Get<IconSource>(AppIcons.BagIcon);
        public static Action<IDependencyPropertyBuilder<IconSource>> EmptyIcon => StaticResource.Get<IconSource>(AppIcons.EmptyIcon);
    }
}
