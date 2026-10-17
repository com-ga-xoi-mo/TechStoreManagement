using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using TechStore.Client.Services.Development;
using TechStore.Client.Views.Shell;
using TechStore.Client.ViewModels.Development;
namespace TechStore.Client.Views.Development;

public sealed partial class ComponentGalleryPage : Page
{
    public ComponentGalleryViewModel ViewModel { get; }
    public ShellDiagnosticViewModel DiagnosticViewModel { get; }
    private readonly ContentDialogPresenter _dialogPresenter;
    private readonly IMarkupReferenceNavigator? _markupReference;

    public ComponentGalleryPage(
        ComponentGalleryViewModel viewModel,
        ShellDiagnosticViewModel diagnosticViewModel,
        ContentDialogPresenter dialogPresenter,
        IMarkupReferenceNavigator? markupReference = null)
    {
        _markupReference = markupReference;
        ViewModel = viewModel;
        DiagnosticViewModel = diagnosticViewModel;
        _dialogPresenter = dialogPresenter;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ApplyTheme();
    }
    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        ViewModel.PropertyChanged -= OnViewModelChanged;
        ViewModel.PropertyChanged += OnViewModelChanged;
        ApplyTheme();
        _dialogPresenter.Attach(this, () => DiagnosticFocusButton);
        _ = DiagnosticViewModel.ActivateAsync();
    }
    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        ViewModel.PropertyChanged -= OnViewModelChanged;
        _dialogPresenter.Detach();
        DiagnosticViewModel.Deactivate();
    }
    private void OnOpenMarkupReference(object sender, RoutedEventArgs args) => _markupReference?.ShowReference();
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ViewModel.ThemeIndex)) ApplyTheme();
    }
    private void ApplyTheme() => RequestedTheme = ViewModel.ThemeIndex switch
    {
        1 => ElementTheme.Light,
        2 => ElementTheme.Dark,
        _ => ElementTheme.Default
    };
    private void OnGallerySizeChanged(object sender, SizeChangedEventArgs args)
    {
        var columns = args.NewSize.Width >= 1200 ? 4 : args.NewSize.Width >= 900 ? 2 : 1;
        var rowCount = (MetricGrid.Children.Count + columns - 1) / columns;
        while (MetricGrid.RowDefinitions.Count > rowCount)
            MetricGrid.RowDefinitions.RemoveAt(MetricGrid.RowDefinitions.Count - 1);
        while (MetricGrid.RowDefinitions.Count < rowCount)
            MetricGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        MetricGrid.Width = double.NaN;
        RevenueCard.Width = OrdersCard.Width = ProductsCard.Width = WarningsCard.Width = double.NaN;
        for (var i = 0; i < MetricGrid.ColumnDefinitions.Count; i++)
            MetricGrid.ColumnDefinitions[i].Width = i < columns ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        var specimenColumns = args.NewSize.Width >= 1000 ? 2 : 1;
        SpecimenGrid.Width = double.NaN;
        SpecimenGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
        SpecimenGrid.ColumnDefinitions[1].Width = specimenColumns == 2 ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Grid.SetRow(RevenueCard, 0);
        Grid.SetColumn(RevenueCard, 0);
        var cardsPerRow = columns;
        Grid.SetRow(OrdersCard, 1 / cardsPerRow);
        Grid.SetColumn(OrdersCard, 1 % cardsPerRow);
        Grid.SetRow(ProductsCard, 2 / cardsPerRow);
        Grid.SetColumn(ProductsCard, 2 % cardsPerRow);
        Grid.SetRow(WarningsCard, 3 / cardsPerRow);
        Grid.SetColumn(WarningsCard, 3 % cardsPerRow);
        GalleryScrollViewer.MaxHeight = Math.Max(360, args.NewSize.Height - 120);
    }
}
