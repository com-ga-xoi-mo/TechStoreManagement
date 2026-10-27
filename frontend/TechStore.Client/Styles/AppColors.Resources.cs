using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Uno.Extensions.Markup;

namespace TechStore.Client.Styles;

public static partial class AppColors
{
    /// <summary>Theme-bound references for C# Markup: <c>.Background(AppColors.Brushes.Canvas)</c>.</summary>
    public static class Brushes
    {
        public static Action<IDependencyPropertyBuilder<Brush>> Canvas => ThemeResource.Get<Brush>(AppColors.Canvas);
        public static Action<IDependencyPropertyBuilder<Brush>> Surface => ThemeResource.Get<Brush>(AppColors.Surface);
        public static Action<IDependencyPropertyBuilder<Brush>> Subtle => ThemeResource.Get<Brush>(AppColors.Subtle);
        public static Action<IDependencyPropertyBuilder<Brush>> Text => ThemeResource.Get<Brush>(AppColors.Text);
        public static Action<IDependencyPropertyBuilder<Brush>> SecondaryText => ThemeResource.Get<Brush>(AppColors.SecondaryText);
        public static Action<IDependencyPropertyBuilder<Brush>> Stroke => ThemeResource.Get<Brush>(AppColors.Stroke);
        public static Action<IDependencyPropertyBuilder<Brush>> InputStroke => ThemeResource.Get<Brush>(AppColors.InputStroke);
        public static Action<IDependencyPropertyBuilder<Brush>> Primary => ThemeResource.Get<Brush>(AppColors.Primary);
        public static Action<IDependencyPropertyBuilder<Brush>> PrimaryHover => ThemeResource.Get<Brush>(AppColors.PrimaryHover);
        public static Action<IDependencyPropertyBuilder<Brush>> PrimaryPressed => ThemeResource.Get<Brush>(AppColors.PrimaryPressed);
        public static Action<IDependencyPropertyBuilder<Brush>> OnPrimary => ThemeResource.Get<Brush>(AppColors.OnPrimary);
        public static Action<IDependencyPropertyBuilder<Brush>> Focus => ThemeResource.Get<Brush>(AppColors.Focus);
        public static Action<IDependencyPropertyBuilder<Brush>> NeutralText => ThemeResource.Get<Brush>(AppColors.NeutralText);
        public static Action<IDependencyPropertyBuilder<Brush>> NeutralFill => ThemeResource.Get<Brush>(AppColors.NeutralFill);
        public static Action<IDependencyPropertyBuilder<Brush>> NeutralStroke => ThemeResource.Get<Brush>(AppColors.NeutralStroke);
        public static Action<IDependencyPropertyBuilder<Brush>> InfoText => ThemeResource.Get<Brush>(AppColors.InfoText);
        public static Action<IDependencyPropertyBuilder<Brush>> InfoFill => ThemeResource.Get<Brush>(AppColors.InfoFill);
        public static Action<IDependencyPropertyBuilder<Brush>> InfoStroke => ThemeResource.Get<Brush>(AppColors.InfoStroke);
        public static Action<IDependencyPropertyBuilder<Brush>> EditContextFill => ThemeResource.Get<Brush>(AppColors.EditContextFill);
        public static Action<IDependencyPropertyBuilder<Brush>> EditContextStroke => ThemeResource.Get<Brush>(AppColors.EditContextStroke);
        public static Action<IDependencyPropertyBuilder<Brush>> SuccessText => ThemeResource.Get<Brush>(AppColors.SuccessText);
        public static Action<IDependencyPropertyBuilder<Brush>> SuccessFill => ThemeResource.Get<Brush>(AppColors.SuccessFill);
        public static Action<IDependencyPropertyBuilder<Brush>> SuccessStroke => ThemeResource.Get<Brush>(AppColors.SuccessStroke);
        public static Action<IDependencyPropertyBuilder<Brush>> WarningText => ThemeResource.Get<Brush>(AppColors.WarningText);
        public static Action<IDependencyPropertyBuilder<Brush>> WarningFill => ThemeResource.Get<Brush>(AppColors.WarningFill);
        public static Action<IDependencyPropertyBuilder<Brush>> WarningStroke => ThemeResource.Get<Brush>(AppColors.WarningStroke);
        public static Action<IDependencyPropertyBuilder<Brush>> DangerText => ThemeResource.Get<Brush>(AppColors.DangerText);
        public static Action<IDependencyPropertyBuilder<Brush>> DangerFill => ThemeResource.Get<Brush>(AppColors.DangerFill);
        public static Action<IDependencyPropertyBuilder<Brush>> DangerStroke => ThemeResource.Get<Brush>(AppColors.DangerStroke);
        public static Action<IDependencyPropertyBuilder<Brush>> AccentText => ThemeResource.Get<Brush>(AppColors.AccentText);
        public static Action<IDependencyPropertyBuilder<Brush>> AccentFill => ThemeResource.Get<Brush>(AppColors.AccentFill);
        public static Action<IDependencyPropertyBuilder<Brush>> AccentStroke => ThemeResource.Get<Brush>(AppColors.AccentStroke);
    }
}
