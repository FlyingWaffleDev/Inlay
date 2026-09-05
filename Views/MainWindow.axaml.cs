using Avalonia;
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
    private const double DragThreshold = 6;
    private bool _closeApproved;
    private bool _closePromptOpen;
    private DocumentTabViewModel? _dragCandidate;
    private TabStripItem? _dragCandidateItem;
    private PointerPressedEventArgs? _dragTrigger;
    private Point _dragStartPoint;

    internal MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

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

        DocumentTabs.AddHandler(
            PointerPressedEvent,
            OnTabPointerPressed,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);

        AddHandler(
            PointerMovedEvent,
            OnWindowPointerMoved,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);

        AddHandler(
            PointerReleasedEvent,
            OnWindowPointerReleased,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);

        DragDrop.SetAllowDrop(this, true);
        DragDrop.SetAllowDrop(TabBar, true);

        AddHandler(DragDrop.DragOverEvent, OnWindowDragOver);
        AddHandler(DragDrop.DropEvent, OnWindowDrop);

        TabBar.AddHandler(DragDrop.DragOverEvent, OnTabsDragOver);
        TabBar.AddHandler(DragDrop.DragLeaveEvent, OnTabsDragLeave);
        TabBar.AddHandler(DragDrop.DropEvent, OnTabsDrop);

        UpdateApplicationIcon();
        ActualThemeVariantChanged += (_, _) => UpdateApplicationIcon();
        Activated += OnActivated;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        ViewModel?.Dispose();
    }

    private void OnActivated(object? sender, EventArgs e) =>
        ViewModel?.CheckSelectedDocumentForExternalChanges();

    private void UpdateApplicationIcon()
    {
        var iconName = ActualThemeVariant == ThemeVariant.Dark
            ? "inlay-icon-dark.png"
            : "inlay-icon-light.png";
        using var stream = AssetLoader.Open(EmbeddedAssets.Uri(iconName));
        Icon = new WindowIcon(stream);
    }

    private void OnEmptyTabBarDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Control source &&
            (source is TabStripItem || source.FindAncestorOfType<TabStripItem>() is not null))
        {
            return;
        }

        if (ViewModel is { } viewModel)
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
            ViewModel is not { } viewModel)
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
        var properties = e.GetCurrentPoint(this).Properties;
        if (properties.PointerUpdateKind == PointerUpdateKind.MiddleButtonPressed)
        {
            if (e.Source is Control { DataContext: DocumentTabViewModel document } &&
                ViewModel is { } viewModel)
            {
                e.Handled = true;
                await viewModel.CloseDocumentAsync(document);
            }

            return;
        }

        if (!properties.IsLeftButtonPressed || IsCloseButton(e.Source))
        {
            return;
        }

        if (FindTabItem(e.Source) is { DataContext: DocumentTabViewModel tab } tabItem)
        {
            _dragCandidate = tab;
            _dragCandidateItem = tabItem;
            _dragTrigger = e;
            _dragStartPoint = e.GetPosition(this);
        }
    }

    private void OnWindowPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragCandidate is not { } tab ||
            _dragCandidateItem is not { } tabItem ||
            _dragTrigger is not { } trigger)
        {
            return;
        }

        var delta = e.GetPosition(this) - _dragStartPoint;
        if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold)
        {
            return;
        }

        ClearDragCandidate();
        _ = StartTabDragAsync(trigger, tab, tabItem);
    }

    private void OnWindowPointerReleased(object? sender, PointerReleasedEventArgs e) =>
        ClearDragCandidate();

    private void ClearDragCandidate()
    {
        _dragCandidate = null;
        _dragCandidateItem = null;
        _dragTrigger = null;
    }

    private async Task StartTabDragAsync(
        PointerPressedEventArgs trigger,
        DocumentTabViewModel tab,
        TabStripItem tabItem)
    {
        using var dataTransfer = new DataTransfer();
        dataTransfer.Add(DataTransferItem.Create(
            TabDragPayload.Format,
            new TabDragPayload { SourceWindow = this, SourceTab = tab }));

        tabItem.Opacity = 0.5;
        try
        {
            await DragDrop.DoDragDropAsync(
                trigger,
                dataTransfer,
                DragDropEffects.Move | DragDropEffects.Copy);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // The platform refused to start a drag; the tab just stays where it is.
        }
        finally
        {
            tabItem.Opacity = 1;
            HideDropIndicator();
        }
    }

    private void OnTabsDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.TryGetValue(TabDragPayload.Format) is not { } payload ||
            !CanAcceptTabDrop(payload))
        {
            e.DragEffects = DragDropEffects.None;
            HideDropIndicator();
            return;
        }

        e.DragEffects = payload.SourceWindow == this
            ? DragDropEffects.Move
            : DragDropEffects.Copy;
        ShowDropIndicator(CalculateTabDropIndex(e.GetPosition(DocumentTabs)));
        e.Handled = true;
    }

    private void OnTabsDragLeave(object? sender, DragEventArgs e) => HideDropIndicator();

    private void OnTabsDrop(object? sender, DragEventArgs e)
    {
        HideDropIndicator();
        if (e.DataTransfer.TryGetValue(TabDragPayload.Format) is not { } payload ||
            !CanAcceptTabDrop(payload) ||
            ViewModel is not { } viewModel)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        var targetSlot = CalculateTabDropIndex(e.GetPosition(DocumentTabs));
        if (payload.SourceWindow == this)
        {
            viewModel.ReorderDocument(payload.SourceTab, targetSlot);
            e.DragEffects = DragDropEffects.Move;
        }
        else
        {
            viewModel.CopyDocument(payload.SourceTab, targetSlot);
            e.DragEffects = DragDropEffects.Copy;
        }

        e.Handled = true;
    }

    // Dropping anywhere outside the other window's tab bar appends the copy instead of placing it.
    private void OnWindowDragOver(object? sender, DragEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        var accepted = e.DataTransfer.TryGetValue(TabDragPayload.Format) is { } payload &&
                       payload.SourceWindow != this &&
                       CanAcceptTabDrop(payload);
        e.DragEffects = accepted ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = accepted;
    }

    private void OnWindowDrop(object? sender, DragEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        HideDropIndicator();
        if (e.DataTransfer.TryGetValue(TabDragPayload.Format) is not { } payload ||
            payload.SourceWindow == this ||
            !CanAcceptTabDrop(payload) ||
            ViewModel is not { } viewModel)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        viewModel.CopyDocument(payload.SourceTab);
        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    internal bool CanAcceptTabDrop(TabDragPayload? payload) =>
        payload is not null &&
        ViewModel is { } viewModel &&
        (payload.SourceWindow == this || !viewModel.ContainsDocument(payload.SourceTab));

    private static bool IsCloseButton(object? source) =>
        source is Visual visual &&
        (visual as Button ?? visual.FindAncestorOfType<Button>()) is { } button &&
        button.Classes.Contains("tab-close");

    private static TabStripItem? FindTabItem(object? source) =>
        source is Visual visual
            ? visual as TabStripItem ?? visual.FindAncestorOfType<TabStripItem>()
            : null;

    // Returns the gap the pointer sits in: 0 is before the first tab, Count is after the last.
    private int CalculateTabDropIndex(Point pointInTabs)
    {
        var count = ViewModel?.Documents.Count ?? 0;
        for (var index = 0; index < count; index++)
        {
            if (DocumentTabs.ContainerFromIndex(index) is Control container &&
                container.TranslatePoint(new Point(0, 0), DocumentTabs) is { } origin &&
                pointInTabs.X < origin.X + container.Bounds.Width / 2)
            {
                return index;
            }
        }

        return count;
    }

    private void ShowDropIndicator(int slot)
    {
        var count = ViewModel?.Documents.Count ?? 0;
        var isTrailingSlot = slot >= count;
        if (DocumentTabs.ContainerFromIndex(isTrailingSlot ? count - 1 : slot) is not Control container ||
            container.TranslatePoint(new Point(0, 0), DocumentTabs) is not { } origin)
        {
            HideDropIndicator();
            return;
        }

        var x = isTrailingSlot ? origin.X + container.Bounds.Width : origin.X;
        TabDropIndicator.Margin = new Thickness(Math.Max(0, x - 1), 0, 0, 0);
        TabDropIndicator.IsVisible = true;
    }

    private void HideDropIndicator() => TabDropIndicator.IsVisible = false;

    internal void ApproveApplicationExit() => _closeApproved = true;

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (_closeApproved || ViewModel is not { } viewModel || !viewModel.IsDirty)
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
