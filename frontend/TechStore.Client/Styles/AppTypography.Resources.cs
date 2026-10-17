using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Uno.Extensions.Markup;

namespace TechStore.Client.Styles;

public static partial class AppTypography
{
    public static class Fonts
    {
        public static Action<IDependencyPropertyBuilder<FontFamily>> RegularFont => StaticResource.Get<FontFamily>(AppTypography.RegularFont);
        public static Action<IDependencyPropertyBuilder<FontFamily>> MediumFont => StaticResource.Get<FontFamily>(AppTypography.MediumFont);
        public static Action<IDependencyPropertyBuilder<FontFamily>> SemiBoldFont => StaticResource.Get<FontFamily>(AppTypography.SemiBoldFont);
        public static Action<IDependencyPropertyBuilder<FontFamily>> BoldFont => StaticResource.Get<FontFamily>(AppTypography.BoldFont);
        public static Action<IDependencyPropertyBuilder<FontFamily>> MonoFont => StaticResource.Get<FontFamily>(AppTypography.MonoFont);
    }

    public static class Styles
    {
        public static Action<IDependencyPropertyBuilder<Style>> BodyText => StaticResource.Get<Style>(AppTypography.BodyText);
        public static Action<IDependencyPropertyBuilder<Style>> HeadingText => StaticResource.Get<Style>(AppTypography.HeadingText);
        public static Action<IDependencyPropertyBuilder<Style>> SectionText => StaticResource.Get<Style>(AppTypography.SectionText);
        public static Action<IDependencyPropertyBuilder<Style>> CaptionText => StaticResource.Get<Style>(AppTypography.CaptionText);
        public static Action<IDependencyPropertyBuilder<Style>> MetricText => StaticResource.Get<Style>(AppTypography.MetricText);
        public static Action<IDependencyPropertyBuilder<Style>> MonoText => StaticResource.Get<Style>(AppTypography.MonoText);
    }
}
