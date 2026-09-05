using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Inlay.ViewModels;

namespace Inlay;

internal sealed partial class TemplateFlyoutView : UserControl
{
    private const double AutoScrollEdgeSize = 24;
    private const double AutoScrollStep = 12;
    private static readonly TimeSpan AutoScrollInterval = TimeSpan.FromMilliseconds(50);
    private readonly DragCandidate<string, ListBoxItem> _dragCandidate = new();
    private readonly DispatcherTimer _autoScrollTimer;
    private Point _lastDragPosition;
    private int _autoScrollDirection;
    private ScrollViewer? _choiceScrollViewer;
    private TemplateFlyoutViewModel? _observedViewModel;

    public TemplateFlyoutView()
    {
        InitializeComponent();

        _autoScrollTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = AutoScrollInterval
        };
        _autoScrollTimer.Tick += OnAutoScrollTick;

        OptionsList.AddHandler(
            PointerPressedEvent,
            OnChoicePointerPressed,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);

        AddHandler(
            PointerMovedEvent,
            OnPointerMoved,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);

        AddHandler(
            PointerReleasedEvent,
            OnPointerReleased,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);

        DragDrop.SetAllowDrop(OptionsList, true);
        OptionsList.AddHandler(DragDrop.DragOverEvent, OnChoiceDragOver);
        OptionsList.AddHandler(DragDrop.DragLeaveEvent, OnChoiceDragLeave);
        OptionsList.AddHandler(DragDrop.DropEvent, OnChoiceDrop);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        StopAutoScroll();

        if (_observedViewModel is not null)
        {
            _observedViewModel.Options.CollectionChanged -= OnOptionsChanged;
        }

        _observedViewModel = DataContext as TemplateFlyoutViewModel;
        if (_observedViewModel is not null)
        {
            _observedViewModel.Options.CollectionChanged += OnOptionsChanged;
            ScheduleSelectionSynchronization(_observedViewModel);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        StopAutoScroll();
        base.OnDetachedFromVisualTree(e);
    }

    private void RemoveOptionClicked(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (DataContext is TemplateFlyoutViewModel viewModel &&
            sender is Button { DataContext: string choice })
        {
            viewModel.RemoveChoice(choice);
        }
    }

    private void NewChoiceKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is TemplateFlyoutViewModel viewModel &&
            viewModel.CanAddChoice)
        {
            viewModel.AddChoice();
            NewChoiceInput.Focus();
            e.Handled = true;
        }
    }

    private void AddChoiceClicked(object? sender, RoutedEventArgs e) =>
        Dispatcher.UIThread.Post(() => NewChoiceInput.Focus());

    private void OnOptionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Move &&
            _observedViewModel is { } viewModel)
        {
            ScheduleSelectionSynchronization(viewModel);
        }
    }

    private void ScheduleSelectionSynchronization(TemplateFlyoutViewModel viewModel) =>
        Dispatcher.UIThread.Post(
            () =>
            {
                if (ReferenceEquals(DataContext, viewModel))
                {
                    OptionsList.SelectedItem = viewModel.SelectedChoice;
                }
            },
            DispatcherPriority.Loaded);

    private void OnChoicePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(this).Properties;
        if (!properties.IsLeftButtonPressed ||
            DragReorder.IsInButton(e.Source, "option-remove") ||
            DataContext is not TemplateFlyoutViewModel viewModel)
        {
            return;
        }

        if (DragReorder.FindContainer<ListBoxItem>(e.Source) is { DataContext: string choice } item)
        {
            _dragCandidate.Arm(choice, item, e, e.GetPosition(this));
            viewModel.BeginSelectionHold();
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragCandidate.TryStart(e.GetPosition(this)) is not { } candidate)
        {
            return;
        }

        if (DataContext is TemplateFlyoutViewModel viewModel)
        {
            viewModel.EndSelectionHold(applyHeldChoice: false);
            ScheduleSelectionSynchronization(viewModel);
        }

        _ = StartChoiceDragAsync(candidate.Trigger, candidate.Item, candidate.Container);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragCandidate.Clear();
        (DataContext as TemplateFlyoutViewModel)?.EndSelectionHold(applyHeldChoice: true);
    }

    private async Task StartChoiceDragAsync(
        PointerPressedEventArgs trigger,
        string choice,
        ListBoxItem item)
    {
        using var dataTransfer = new DataTransfer();
        dataTransfer.Add(DataTransferItem.Create(
            TemplateChoiceDragPayload.Format,
            new TemplateChoiceDragPayload { SourceView = this, SourceChoice = choice }));

        item.Opacity = 0.5;
        try
        {
            await DragDrop.DoDragDropAsync(trigger, dataTransfer, DragDropEffects.Move);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // The platform refused to start a drag; the choice stays where it is.
        }
        finally
        {
            item.Opacity = 1;
            StopAutoScroll();
            HideDropIndicator();
        }
    }

    private void OnChoiceDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.TryGetValue(TemplateChoiceDragPayload.Format) is not { } payload ||
            !CanAcceptChoiceDrop(payload))
        {
            e.DragEffects = DragDropEffects.None;
            StopAutoScroll();
            HideDropIndicator();
            return;
        }

        var pointInList = e.GetPosition(OptionsList);
        e.DragEffects = DragDropEffects.Move;
        ShowDropIndicator(CalculateChoiceDropIndex(pointInList));
        UpdateAutoScroll(pointInList);
        e.Handled = true;
    }

    private void OnChoiceDragLeave(object? sender, DragEventArgs e)
    {
        StopAutoScroll();
        HideDropIndicator();
    }

    private void OnChoiceDrop(object? sender, DragEventArgs e)
    {
        StopAutoScroll();
        HideDropIndicator();
        if (e.DataTransfer.TryGetValue(TemplateChoiceDragPayload.Format) is not { } payload ||
            !CanAcceptChoiceDrop(payload) ||
            DataContext is not TemplateFlyoutViewModel viewModel)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        viewModel.ReorderChoice(
            payload.SourceChoice,
            CalculateChoiceDropIndex(e.GetPosition(OptionsList)));
        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void UpdateAutoScroll(Point pointInList)
    {
        _lastDragPosition = pointInList;
        var edgeSize = Math.Min(AutoScrollEdgeSize, OptionsList.Bounds.Height / 2);
        var direction = pointInList.Y <= edgeSize
            ? -1
            : pointInList.Y >= OptionsList.Bounds.Height - edgeSize
                ? 1
                : 0;

        if (direction == 0)
        {
            StopAutoScroll();
            return;
        }

        _autoScrollDirection = direction;
        if (!_autoScrollTimer.IsEnabled && ScrollChoiceList())
        {
            _autoScrollTimer.Start();
        }
    }

    private void OnAutoScrollTick(object? sender, EventArgs e)
    {
        if (!ScrollChoiceList())
        {
            StopAutoScroll();
        }
    }

    private bool ScrollChoiceList()
    {
        var scrollViewer = _choiceScrollViewer ??= OptionsList.FindDescendantOfType<ScrollViewer>();
        if (scrollViewer is null)
        {
            return false;
        }

        var maximumOffset = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
        var newOffset = Math.Clamp(
            scrollViewer.Offset.Y + _autoScrollDirection * AutoScrollStep,
            0,
            maximumOffset);
        if (Math.Abs(newOffset - scrollViewer.Offset.Y) < 0.5)
        {
            return false;
        }

        scrollViewer.Offset = scrollViewer.Offset.WithY(newOffset);
        OptionsList.UpdateLayout();
        ShowDropIndicator(CalculateChoiceDropIndex(_lastDragPosition));
        return true;
    }

    private void StopAutoScroll()
    {
        _autoScrollTimer.Stop();
        _autoScrollDirection = 0;
    }

    internal bool IsAutoScrollingChoices => _autoScrollTimer.IsEnabled;

    internal bool CanAcceptChoiceDrop(TemplateChoiceDragPayload? payload) =>
        payload is not null &&
        payload.SourceView == this &&
        DataContext is TemplateFlyoutViewModel viewModel &&
        viewModel.Options.Contains(payload.SourceChoice);

    private int ChoiceCount => (DataContext as TemplateFlyoutViewModel)?.Options.Count ?? 0;

    private int CalculateChoiceDropIndex(Point pointInList) => DragReorder.DropSlot(
        OptionsList,
        ChoiceCount,
        pointInList,
        Orientation.Vertical);

    private void ShowDropIndicator(int slot)
    {
        if (DragReorder.SlotOffset(
                OptionsList,
                ChoiceCount,
                slot,
                Orientation.Vertical,
                relativeTo: OptionsListHost) is not { } y)
        {
            HideDropIndicator();
            return;
        }

        var maximumY = Math.Max(0, OptionsListHost.Bounds.Height - ChoiceDropIndicator.Height);
        ChoiceDropIndicator.Margin = new Thickness(4, Math.Clamp(y - 1, 0, maximumY), 4, 0);
        ChoiceDropIndicator.IsVisible = true;
    }

    private void HideDropIndicator() => ChoiceDropIndicator.IsVisible = false;
}
