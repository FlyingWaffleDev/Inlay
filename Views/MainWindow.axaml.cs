using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Inlay.ViewModels;

namespace Inlay;

internal sealed partial class MainWindow : Window
{
    private const double TabScrollStep = 96;
    private const double TabWheelScrollStep = 64;
    private bool _closeApproved;
    private bool _closePromptOpen;

    public MainWindow()
    {
        InitializeView();
    }

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeView();
        DataContext = viewModel;
    }

    private void InitializeView()
    {
        InitializeComponent();
        EditorBorder.AddHandler(
            PointerWheelChangedEvent,
            OnEditorPointerWheelChanged,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        UpdateApplicationIcon();
        ActualThemeVariantChanged += (_, _) => UpdateApplicationIcon();
        Activated += OnActivated;
    }

    private void OnActivated(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.CheckSelectedDocumentForExternalChanges();
        }
    }

    private void UpdateApplicationIcon()
    {
        var iconName = ActualThemeVariant == ThemeVariant.Dark
            ? "inlay-icon-dark.png"
            : "inlay-icon-light.png";
        using var stream = AssetLoader.Open(new Uri($"avares://Inlay/Assets/{iconName}"));
        Icon = new WindowIcon(stream);
    }

    private void OnEmptyTabBarDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Control source &&
            (source is TabStripItem || source.FindAncestorOfType<TabStripItem>() is not null))
        {
            return;
        }

        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.AddNewDocument();
            e.Handled = true;
        }
    }

    private void OnTabsScrollChanged(object? sender, ScrollChangedEventArgs e) =>
        UpdateTabScrollButtons();

    private void OnTabsScrollViewerSizeChanged(object? sender, SizeChangedEventArgs e) =>
        UpdateTabScrollButtons();

    private void OnDocumentTabSelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        Dispatcher.UIThread.Post(BringSelectedTabIntoView, DispatcherPriority.Loaded);

    private void ScrollTabsLeftClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ScrollTabsBy(-TabScrollStep);
    }

    private void ScrollTabsRightClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ScrollTabsBy(TabScrollStep);
    }

    private void OnTabsPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var wheelDelta = Math.Abs(e.Delta.X) > Math.Abs(e.Delta.Y)
            ? e.Delta.X
            : e.Delta.Y;
        if (wheelDelta == 0)
        {
            return;
        }

        var initialOffset = TabsScrollViewer.Offset.X;
        ScrollTabsBy(-wheelDelta * TabWheelScrollStep);
        e.Handled = Math.Abs(TabsScrollViewer.Offset.X - initialOffset) > 0.5;
    }

    private void OnEditorPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if ((e.KeyModifiers & KeyModifiers.Control) == 0 || e.Delta.Y == 0 ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.AdjustEditorZoom(e.Delta.Y > 0 ? 1 : -1);
        e.Handled = true;
    }

    private void ScrollTabsBy(double distance)
    {
        var maximumOffset = Math.Max(0, TabsScrollViewer.Extent.Width - TabsScrollViewer.Viewport.Width);
        var horizontalOffset = Math.Clamp(TabsScrollViewer.Offset.X + distance, 0, maximumOffset);
        TabsScrollViewer.Offset = TabsScrollViewer.Offset.WithX(horizontalOffset);
        UpdateTabScrollButtons();
    }

    private void UpdateTabScrollButtons()
    {
        const double tolerance = 0.5;
        var buttonWidth = TabScrollButtons.IsVisible
            ? TabScrollButtons.Bounds.Width + TabScrollButtons.Margin.Left + TabScrollButtons.Margin.Right
            : 0;
        var availableWidth = TabsScrollViewer.Viewport.Width + buttonWidth;
        var hasOverflow = TabsScrollViewer.Extent.Width > availableWidth + tolerance;

        if (TabScrollButtons.IsVisible != hasOverflow)
        {
            TabScrollButtons.IsVisible = hasOverflow;
            if (!hasOverflow)
            {
                TabsScrollViewer.Offset = TabsScrollViewer.Offset.WithX(0);
            }
            else
            {
                Dispatcher.UIThread.Post(BringSelectedTabIntoView, DispatcherPriority.Loaded);
            }
        }

        var maximumOffset = Math.Max(0, TabsScrollViewer.Extent.Width - TabsScrollViewer.Viewport.Width);
        ScrollTabsLeftButton.IsEnabled = hasOverflow && TabsScrollViewer.Offset.X > tolerance;
        ScrollTabsRightButton.IsEnabled = hasOverflow &&
            TabsScrollViewer.Offset.X < maximumOffset - tolerance;
    }

    private void BringSelectedTabIntoView()
    {
        if (DocumentTabs.SelectedItem is { } selectedItem &&
            DocumentTabs.ContainerFromItem(selectedItem) is Control selectedTab)
        {
            selectedTab.BringIntoView();
            if (DocumentTabs.SelectedIndex == 0)
            {
                TabsScrollViewer.Offset = TabsScrollViewer.Offset.WithX(0);
            }
        }
    }

    private async void OnTabPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind != PointerUpdateKind.MiddleButtonPressed ||
            e.Source is not Control { DataContext: DocumentTabViewModel document } ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        e.Handled = true;
        await viewModel.CloseDocumentAsync(document);
    }

    internal void ApproveApplicationExit() => _closeApproved = true;

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (_closeApproved || DataContext is not MainWindowViewModel viewModel || !viewModel.IsDirty)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        base.OnClosing(e);
        if (_closePromptOpen)
        {
            return;
        }

        _closePromptOpen = true;
        try
        {
            if (await viewModel.CanCloseAsync())
            {
                _closeApproved = true;
                Close();
            }
        }
        finally
        {
            _closePromptOpen = false;
        }
    }
}
