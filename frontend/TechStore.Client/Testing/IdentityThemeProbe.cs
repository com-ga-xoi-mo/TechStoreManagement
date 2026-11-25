using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Input;
using TechStore.Client.Models.Identity;
using TechStore.Client.Services.Session;
using TechStore.Client.Styles;
using TechStore.Client.Views.Identity;
using TechStore.Client.Views.Settings;
using TechStore.Client.Views.Shell;

namespace TechStore.Client.Testing;

internal static class IdentityThemeProbe
{
    public static async Task<List<ProbeReportItem>> RunAsync(UserControl host, Func<Task> settle, CancellationToken ct)
    {
        AppResources.Register(Application.Current);
        var configuration = new ConfigurationBuilder().Build();
        var session = new InMemorySessionStore();
        session.SetSession("probe", "probe", "admin", "Admin", "Admin");
        var services = new ServiceCollection().AddFrontendFoundation(configuration).AddFrontendUi(configuration);
        services.AddSingleton<ISessionStore>(session);
        await using var provider = services.BuildServiceProvider();
        var settings = provider.GetRequiredService<SettingsPage>();
        var accounts = provider.GetRequiredService<UserManagementPage>();
        var account = new UserAccount(Guid.NewGuid(), "employee", "Hồ sơ giữ nguyên", "employee@test.vn", "Manager", true, DateTime.UtcNow, null);
        accounts.ViewModel.Accounts.Add(account);
        accounts.ViewModel.SelectedAccount = account;
        var presenter = provider.GetRequiredService<ContentDialogPresenter>();
        var reports = new List<ProbeReportItem>();
        var login = provider.GetRequiredService<LoginPage>();
        host.Content = login;
        foreach (var theme in new[] { ElementTheme.Light, ElementTheme.Dark, ElementTheme.Light })
        {
            ct.ThrowIfCancellationRequested();
            login.RequestedTheme = theme;
            login.ViewModel.IsPasswordVisible = false;
            await settle();
            var username = Descendants<TextBox>(login).Single(box => box.PlaceholderText.Contains("mã nhân viên"));
            var password = Descendants<PasswordBox>(login).Single();
            var card = Descendants<StackPanel>(login).Single(panel => panel.Width == 440);
            var usernameContrast = await CheckLoginHintAsync(username, username.PlaceholderText, card.Background, settle);
            var passwordContrast = await CheckLoginHintAsync(password, password.PlaceholderText, card.Background, settle);
            login.ViewModel.TogglePasswordVisibilityCommand.Execute(null);
            await settle();
            var reveal = Descendants<TextBox>(login).Distinct().Single(box => box.Name == "RevealedPasswordInput");
            var revealContrast = await CheckLoginHintAsync(reveal, reveal.PlaceholderText, card.Background, settle);
            var palette = theme == ElementTheme.Dark ? AppColors.Dark : AppColors.Light;
            var actual = $"card={Hex(card.Background)}, contrast username={usernameContrast:F2}, masked={passwordContrast:F2}, revealed={revealContrast:F2}";
            reports.Add(new($"Login placeholders in {theme}", "Mounted production login form",
                "The form follows the current theme and each focused placeholder has at least 4.5:1 contrast", actual,
                Hex(login.Background) == palette[AppColors.Canvas] && Hex(card.Background) == palette[AppColors.Surface]
                    && usernameContrast >= 4.5 && passwordContrast >= 4.5 && revealContrast >= 4.5,
                "Checks username and both masked and revealed password fields on the same page across theme changes."));
        }

        foreach (var page in new Page[] { settings, accounts })
        {
            page.Width = 800;
            page.Height = 600;
            host.Content = page;
            await settle();
            var title = Descendants<TextBlock>(page).Single(t => t.Text == (page == settings ? "Cài đặt" : "Quản lý tài khoản"));
            var form = page == accounts ? Descendants<Border>(page).Single(b => b.Child is StackPanel && b.Padding.Left == 20) : null;
            var save = Descendants<Button>(page).Single(b => page == accounts
                ? Equals(b.Content, "Lưu thông tin") : b.Content is TextBlock { Text: "Lưu thay đổi" });
            ContentDialog? dialog = null;
            Task<bool>? showing = null;
            if (page == accounts)
            {
                presenter.Attach(page, () => save);
                var changeRole = Descendants<Button>(page).Single(b => Equals(b.Content, "Đổi vai trò"));
                var focusedBefore = changeRole.Focus(FocusState.Keyboard);
                var change = accounts.ViewModel.ChangeRoleCommand.ExecuteAsync(null);
                await settle();
                var focusable = changeRole.IsEnabled;
                var pending = accounts.ViewModel.IsBusy && !change.IsCompleted;
                presenter.Detach();
                await change;
                await settle();
                var restored = ReferenceEquals(FocusManager.GetFocusedElement(page.XamlRoot!), changeRole);
                reports.Add(new("Identity confirmation restores initiating button", "Production ChangeRoleCommand + ContentDialogPresenter",
                    "Button remains focusable; cancelled confirmation restores its focus; no write", $"focusedBefore={focusedBefore}, pending={pending}, enabled={focusable}, restored={restored}, busy={accounts.ViewModel.IsBusy}",
                    focusedBefore && pending && focusable && restored && !accounts.ViewModel.IsBusy, "Cancels the dialog through presenter detachment; uses the real account command and dialog service."));
                presenter.Attach(page, () => save);
                dialog = new ContentDialog { Title = "Khóa tài khoản?", Content = "Thu hồi các phiên hiện có.", PrimaryButtonText = "Xác nhận", CloseButtonText = "Hủy" };
                showing = presenter.TryShowAsync(() => dialog, ct);
                await settle();
            }
            try
            {
                foreach (var theme in new[] { ElementTheme.Light, ElementTheme.Dark, ElementTheme.Default, ElementTheme.Light })
                {
                    ct.ThrowIfCancellationRequested();
                    settings.ViewModel.SelectedThemeIndex = theme switch { ElementTheme.Light => 1, ElementTheme.Dark => 2, _ => 0 };
                    page.RequestedTheme = theme;
                    await settle();
                    page.UpdateLayout();
                    var palette = page.ActualTheme == ElementTheme.Dark ? AppColors.Dark : AppColors.Light;
                    var actual = $"canvas={Hex(page.Background)}, text={Hex(title.Foreground)}, button={Hex(save.Background)}/{Hex(save.Foreground)}, surface={Hex(form?.Background)}, dialog={dialog?.RequestedTheme}/{dialog?.ActualTheme}";
                    var passed = page.ActualWidth == 800 && page.ActualHeight == 600
                        && Hex(page.Background) == palette[AppColors.Canvas] && Hex(title.Foreground) == palette[AppColors.Text]
                        && Hex(save.Background) == palette[AppColors.Primary] && Hex(save.Foreground) == palette[AppColors.OnPrimary]
                        && (form is null || Hex(form.Background) == palette[AppColors.Surface])
                        && (dialog is null || dialog.RequestedTheme == theme && dialog.ActualTheme == page.ActualTheme)
                        && accounts.ViewModel.FullName == account.FullName;
                    reports.Add(new($"Identity {page.GetType().Name} live {theme}", "Mounted production page + SettingsViewModel + ContentDialogPresenter",
                        $"800x600; {page.ActualTheme} palette; active dialog follows preference; profile preserved", actual, passed, "Same page/dialog instances throughout Light → Dark → System → Light; no API writes."));
                }
            }
            finally
            {
                dialog?.Hide();
                if (showing is not null) await showing;
                presenter.Detach();
            }
        }
        host.Content = null;
        return reports;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private static string? Hex(Brush? brush) => brush is SolidColorBrush b ? $"#{b.Color.R:X2}{b.Color.G:X2}{b.Color.B:X2}" : null;

    private static async Task<double> CheckLoginHintAsync(Control input, string placeholder, Brush? card, Func<Task> settle)
    {
        if (!input.Focus(FocusState.Keyboard))
            return 0;

        await settle();
        var hint = Descendants<TextBlock>(input).FirstOrDefault(text => text.Text == placeholder && text.ActualWidth > 0);
        var border = Descendants<Border>(input).FirstOrDefault(part => part.Name == "BorderElement");
        if (hint is not { Visibility: Visibility.Visible, Foreground: SolidColorBrush foreground } || card is not SolidColorBrush surface)
            return 0;

        var background = (border?.Background ?? input.Background) as SolidColorBrush;
        var cardRgb = (R: (double)surface.Color.R, G: (double)surface.Color.G, B: (double)surface.Color.B);
        var inputRgb = background is null ? cardRgb : Blend(background, cardRgb);
        var inputLuminance = Luminance(inputRgb);
        var textLuminance = Luminance(Blend(foreground, inputRgb));
        return (Math.Max(inputLuminance, textLuminance) + 0.05) / (Math.Min(inputLuminance, textLuminance) + 0.05);
    }

    private static (double R, double G, double B) Blend(SolidColorBrush brush, (double R, double G, double B) backdrop)
    {
        var alpha = brush.Color.A / 255.0 * brush.Opacity;
        return (brush.Color.R * alpha + backdrop.R * (1 - alpha),
            brush.Color.G * alpha + backdrop.G * (1 - alpha),
            brush.Color.B * alpha + backdrop.B * (1 - alpha));
    }

    private static double Luminance((double R, double G, double B) rgb)
    {
        static double Linear(double channel)
        {
            var srgb = channel / 255;
            return srgb <= 0.04045 ? srgb / 12.92 : Math.Pow((srgb + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Linear(rgb.R) + 0.7152 * Linear(rgb.G) + 0.0722 * Linear(rgb.B);
    }
}
