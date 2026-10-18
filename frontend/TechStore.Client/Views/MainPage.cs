using Microsoft.UI.Xaml.Controls;
using TechStore.Client.Styles;
using Uno.Extensions.Markup;

namespace TechStore.Client.Views;

/// <summary>Legacy navigation frame specimen. The production application continues to use ShellPage.</summary>
public sealed class MainPage : Page
{
    public MainPage()
    {
        var navigation = new NavigationView
        {
            PaneTitle = "TechStore System",
            IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
            IsSettingsVisible = true
        };

        navigation.MenuItems.Add(MenuItem("Dashboard", "Dashboard", Symbol.Home, selected: true));
        navigation.MenuItems.Add(MenuItem("Sản phẩm (Catalog)", "Catalog", Symbol.Shop));
        navigation.MenuItems.Add(MenuItem("Kho hàng & IMEI (Inventory)", "Inventory", Symbol.AllApps));
        navigation.MenuItems.Add(MenuItem("Bán hàng (POS)", "POS", Symbol.Shop));
        navigation.MenuItems.Add(MenuItem("Đơn hàng (Orders)", "Orders", Symbol.Document));
        navigation.MenuItems.Add(MenuItem("Khách hàng (Customers)", "Customers", Symbol.Contact));
        navigation.MenuItems.Add(MenuItem("Tài khoản & AI (Identity)", "Identity", Symbol.Admin));

        var contentFrame = new Frame
        {
            Content = new Grid()
                .Padding(32)
                .Children(new StackPanel()
                    .Spacing(16)
                    .Children(
                        new TextBlock().Style(AppTypography.Styles.HeadingText)
                            .Text("TechStore Management System"),
                        new TextBlock().Style(AppTypography.Styles.BodyText)
                            .Text("Hệ thống Quản lý và Bán hàng Chuỗi Thiết bị Công nghệ (UDQL2 - 2026)"),
                        new Border().Style(AppControlStyles.Styles.Panel)
                            .Margin(new Microsoft.UI.Xaml.Thickness(0, 16, 0, 0))
                            .Child(new StackPanel().Spacing(8).Children(
                                new TextBlock().Style(AppTypography.Styles.SectionText)
                                    .Text("Khung Điều Hướng Ứng Dụng (Shell Navigation Ready)"),
                                new TextBlock().Style(AppTypography.Styles.BodyText)
                                    .Text("• Nền tảng: Uno Platform WinUI 3 (Hỗ trợ macOS Skia & Windows Native)"),
                                new TextBlock().Style(AppTypography.Styles.BodyText)
                                    .Text("• Backend: ASP.NET Core 10 Web API + EF Core Npgsql"),
                                new TextBlock().Style(AppTypography.Styles.BodyText)
                                    .Text("• CSDL: PostgreSQL 16 (docker-compose up -d)"),
                                new TextBlock().Style(AppTypography.Styles.BodyText)
                                    .Text("• Phân quyền RBAC: Admin, Manager, Sales Staff, Warehouse")))))
        };

        navigation.Content = contentFrame;
        Content = navigation;
    }

    private static NavigationViewItem MenuItem(string label, string tag, Symbol symbol, bool selected = false) =>
        new()
        {
            Content = label,
            Tag = tag,
            Icon = new SymbolIcon(symbol),
            IsSelected = selected
        };
}
