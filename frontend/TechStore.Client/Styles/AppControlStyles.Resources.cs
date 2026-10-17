using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Uno.Extensions.Markup;

namespace TechStore.Client.Styles;

public static partial class AppControlStyles
{
    public static class Styles
    {
        public static Action<IDependencyPropertyBuilder<Style>> Panel => StaticResource.Get<Style>(AppControlStyles.Panel);
        public static Action<IDependencyPropertyBuilder<Style>> Button => StaticResource.Get<Style>(AppControlStyles.Button);
        public static Action<IDependencyPropertyBuilder<Style>> PrimaryButton => StaticResource.Get<Style>(AppControlStyles.PrimaryButton);
        public static Action<IDependencyPropertyBuilder<Style>> DangerButton => StaticResource.Get<Style>(AppControlStyles.DangerButton);
        public static Action<IDependencyPropertyBuilder<Style>> QuietButton => StaticResource.Get<Style>(AppControlStyles.QuietButton);
        public static Action<IDependencyPropertyBuilder<Style>> TextBox => StaticResource.Get<Style>(AppControlStyles.TextBox);
        public static Action<IDependencyPropertyBuilder<Style>> AutoSuggestBox => StaticResource.Get<Style>(AppControlStyles.AutoSuggestBox);
        public static Action<IDependencyPropertyBuilder<Style>> ComboBox => StaticResource.Get<Style>(AppControlStyles.ComboBox);
        public static Action<IDependencyPropertyBuilder<Style>> FieldError => StaticResource.Get<Style>(AppControlStyles.FieldError);
        public static Action<IDependencyPropertyBuilder<Style>> TableRow => StaticResource.Get<Style>(AppControlStyles.TableRow);
        public static Action<IDependencyPropertyBuilder<Style>> TableHeader => StaticResource.Get<Style>(AppControlStyles.TableHeader);
    }
}
