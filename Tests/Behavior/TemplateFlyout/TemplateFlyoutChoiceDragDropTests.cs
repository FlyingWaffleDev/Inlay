using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Inlay.ViewModels;
using Xunit;

namespace Inlay.Tests;

public sealed class TemplateFlyoutChoiceDragDropTests
{
    [AvaloniaFact]
    public void DroppingAChoicePastAnotherChoicesMidpointMovesItAfterThatChoice()
    {
        var options = new ObservableCollection<string>(["One", "Two", "Three"]);
        var (window, view) = CreateView(options);
        try
        {
            Drop(window, view, "One", Below(window, view, "Two"));

            Assert.Equal(["Two", "One", "Three"], options);
            Assert.Equal("One", view.OptionsList.SelectedItem);
            Assert.True(FindChoiceItem(view, "One").IsSelected);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DroppingAChoiceBeforeAnotherChoicesMidpointMovesItAheadOfThatChoice()
    {
        var options = new ObservableCollection<string>(["One", "Two", "Three"]);
        var (window, view) = CreateView(options);
        try
        {
            Drop(window, view, "Three", Above(window, view, "One"));

            Assert.Equal(["Three", "One", "Two"], options);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DragOverShowsDropIndicatorAndDragLeaveHidesIt()
    {
        var options = new ObservableCollection<string>(["One", "Two"]);
        var (window, view) = CreateView(options);
        try
        {
            using var transfer = CreateTransfer(view, "One");
            var point = Below(window, view, "Two");

            RaiseDrag(window, RawDragEventType.DragEnter, point, transfer);
            RaiseDrag(window, RawDragEventType.DragOver, point, transfer);
            Assert.True(view.ChoiceDropIndicator.IsVisible);

            RaiseDrag(window, RawDragEventType.DragLeave, point, transfer);
            Assert.False(view.ChoiceDropIndicator.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DraggingNearTheBottomStartsScrollingDownAndStopsOnLeave()
    {
        var choices = Enumerable.Range(1, 30).Select(index => $"Choice {index}").ToArray();
        var (window, view) = CreateView(choices);
        try
        {
            var scrollViewer = FindOptionsScrollViewer(view);
            Assert.True(scrollViewer.Extent.Height > scrollViewer.Viewport.Height);
            using var transfer = CreateTransfer(view, choices[0]);

            var point = NearListEdge(window, view, bottom: true);
            RaiseDrag(window, RawDragEventType.DragEnter, point, transfer);
            RaiseDrag(window, RawDragEventType.DragOver, point, transfer);

            Assert.True(scrollViewer.Offset.Y > 0);
            Assert.True(view.IsAutoScrollingChoices);

            RaiseDrag(window, RawDragEventType.DragLeave, point, transfer);
            Assert.False(view.IsAutoScrollingChoices);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DraggingNearTheTopStartsScrollingUpAndStopsOnDrop()
    {
        var choices = Enumerable.Range(1, 30).Select(index => $"Choice {index}").ToArray();
        var (window, view) = CreateView(choices);
        try
        {
            var scrollViewer = FindOptionsScrollViewer(view);
            var maximumOffset = scrollViewer.Extent.Height - scrollViewer.Viewport.Height;
            Assert.True(maximumOffset > 0);
            scrollViewer.Offset = scrollViewer.Offset.WithY(maximumOffset);
            Settle(window);
            using var transfer = CreateTransfer(view, choices[^1]);

            var point = NearListEdge(window, view, bottom: false);
            RaiseDrag(window, RawDragEventType.DragEnter, point, transfer);
            RaiseDrag(window, RawDragEventType.DragOver, point, transfer);

            Assert.True(scrollViewer.Offset.Y < maximumOffset);
            Assert.True(view.IsAutoScrollingChoices);

            RaiseDrag(window, RawDragEventType.Drop, point, transfer);
            Assert.False(view.IsAutoScrollingChoices);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ReopeningTheFlyoutHighlightsTheReorderedChoice()
    {
        var options = new ObservableCollection<string>(["One", "Two", "Three"]);
        var (window, view) = CreateView(options);
        try
        {
            Drop(window, view, "One", Below(window, view, "Two"));

            var oldViewModel = Assert.IsType<TemplateFlyoutViewModel>(view.DataContext);
            oldViewModel.Disconnect();
            view.DataContext = null;
            view.DataContext = new TemplateFlyoutViewModel(options, 1, _ => { }, () => { });
            Settle(window);

            Assert.Equal("One", view.OptionsList.SelectedItem);
            Assert.True(FindChoiceItem(view, "One").IsSelected);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DraggingAChoiceLeavesTheSelectedChoiceAlone()
    {
        var selections = new List<string>();
        var options = new ObservableCollection<string>(["One", "Two", "Three"]);
        var (window, view) = CreateView(options, selections);
        try
        {
            var viewModel = Assert.IsType<TemplateFlyoutViewModel>(view.DataContext);
            var start = Center(window, FindChoiceItem(view, "One"));

            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Vector(0, 24));
            window.MouseUp(start + new Vector(0, 24), MouseButton.Left);
            Settle(window);

            Assert.Equal("One", viewModel.SelectedChoice);
            Assert.Equal("One", view.OptionsList.SelectedItem);
            Assert.Empty(selections);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ClickingAChoiceSelectsItOnRelease()
    {
        var selections = new List<string>();
        var options = new ObservableCollection<string>(["One", "Two", "Three"]);
        var (window, view) = CreateView(options, selections);
        try
        {
            var viewModel = Assert.IsType<TemplateFlyoutViewModel>(view.DataContext);
            var point = Center(window, FindChoiceItem(view, "Two"));

            window.MouseDown(point, MouseButton.Left);
            Assert.Equal("One", viewModel.SelectedChoice);

            window.MouseUp(point, MouseButton.Left);
            Settle(window);

            Assert.Equal("Two", viewModel.SelectedChoice);
            Assert.Equal(["Two"], selections);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ClickingTheSelectedChoiceClearsIt()
    {
        var selections = new List<string>();
        var options = new ObservableCollection<string>(["One", "Two", "Three"]);
        var (window, view) = CreateView(options, selections);
        try
        {
            var viewModel = Assert.IsType<TemplateFlyoutViewModel>(view.DataContext);
            var point = Center(window, FindChoiceItem(view, "One"));

            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Settle(window);

            Assert.Null(viewModel.SelectedChoice);
            Assert.Equal([TemplateTextElement.PlaceholderText], selections);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AChoiceDragIsRejectedByAnotherFlyout()
    {
        var (sourceWindow, sourceView) = CreateView(["Source"]);
        var (targetWindow, targetView) = CreateView(["Target"]);
        try
        {
            var payload = new TemplateChoiceDragPayload
            {
                SourceView = sourceView,
                SourceChoice = "Source"
            };

            Assert.True(sourceView.CanAcceptChoiceDrop(payload));
            Assert.False(targetView.CanAcceptChoiceDrop(payload));
        }
        finally
        {
            sourceWindow.Close();
            targetWindow.Close();
        }
    }

    private static (Window Window, TemplateFlyoutView View) CreateView(
        IEnumerable<string> choices,
        List<string>? selections = null)
    {
        var options = choices as ObservableCollection<string> ?? [.. choices];
        var viewModel = new TemplateFlyoutViewModel(
            options,
            0,
            choice => selections?.Add(choice),
            () => { });
        var view = new TemplateFlyoutView { DataContext = viewModel };
        var window = new Window { Content = view };
        window.Show();
        Settle(window);
        return (window, view);
    }

    private static void Drop(
        Window window,
        TemplateFlyoutView view,
        string choice,
        Point point)
    {
        using var transfer = CreateTransfer(view, choice);
        RaiseDrag(window, RawDragEventType.DragEnter, point, transfer);
        RaiseDrag(window, RawDragEventType.DragOver, point, transfer);
        RaiseDrag(window, RawDragEventType.Drop, point, transfer);
        Settle(window);
    }

    private static DataTransfer CreateTransfer(TemplateFlyoutView view, string choice)
    {
        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.Create(
            TemplateChoiceDragPayload.Format,
            new TemplateChoiceDragPayload { SourceView = view, SourceChoice = choice }));
        return transfer;
    }

    private static void RaiseDrag(
        Window window,
        RawDragEventType type,
        Point point,
        DataTransfer transfer) =>
        window.DragDrop(point, type, transfer, DragDropEffects.Move, RawInputModifiers.None);

    private static Point Center(Window window, ListBoxItem item) =>
        item.TranslatePoint(
            new Point(item.Bounds.Width / 2, item.Bounds.Height / 2),
            window)!.Value;

    private static Point Above(Window window, TemplateFlyoutView view, string choice)
    {
        var item = FindChoiceItem(view, choice);
        return item.TranslatePoint(
            new Point(item.Bounds.Width / 2, 2),
            window)!.Value;
    }

    private static Point Below(Window window, TemplateFlyoutView view, string choice)
    {
        var item = FindChoiceItem(view, choice);
        return item.TranslatePoint(
            new Point(item.Bounds.Width / 2, item.Bounds.Height - 2),
            window)!.Value;
    }

    private static Point NearListEdge(
        Window window,
        TemplateFlyoutView view,
        bool bottom) =>
        view.OptionsList.TranslatePoint(
            new Point(
                view.OptionsList.Bounds.Width / 2,
                bottom ? view.OptionsList.Bounds.Height - 2 : 2),
            window)!.Value;

    private static ScrollViewer FindOptionsScrollViewer(TemplateFlyoutView view) =>
        Assert.Single(view.OptionsList.GetVisualDescendants().OfType<ScrollViewer>());

    private static ListBoxItem FindChoiceItem(TemplateFlyoutView view, string choice) =>
        Assert.Single(
            view.GetVisualDescendants().OfType<ListBoxItem>(),
            item => Equals(item.DataContext, choice));

    private static void Settle(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
