using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TechStore.Client.Models.Analytics;
using TechStore.Client.ViewModels.Dashboard;

namespace TechStore.Client.Views.Dashboard;

public sealed partial class DashboardPage : Page
{
    public DashboardViewModel ViewModel { get; }

    public DashboardPage()
    {
        ViewModel = App.Services.GetRequiredService<DashboardViewModel>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        await ViewModel.ActivateAsync();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        ViewModel.Deactivate();
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        base.OnNavigatedFrom(e);
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DashboardViewModel.Snapshot))
        {
            RevenueChart.Points = ViewModel.Snapshot?.RevenuePoints ?? Array.Empty<RevenuePoint>();
        }
    }

    private void KpiGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var columnCount = e.NewSize.Width < 440 ? 1 : e.NewSize.Width < 900 ? 2 : 4;
        if (KpiGrid.ColumnDefinitions.Count == columnCount)
        {
            return;
        }

        KpiGrid.ColumnDefinitions.Clear();
        KpiGrid.RowDefinitions.Clear();
        for (var column = 0; column < columnCount; column++)
        {
            KpiGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var cards = new[] { RevenueCard, OrdersCard, ProductsCard, LowStockCard };
        var rowCount = (int)Math.Ceiling(cards.Length / (double)columnCount);
        for (var row = 0; row < rowCount; row++)
        {
            KpiGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var index = 0; index < cards.Length; index++)
        {
            Grid.SetColumn(cards[index], index % columnCount);
            Grid.SetRow(cards[index], index / columnCount);
        }
    }
}
