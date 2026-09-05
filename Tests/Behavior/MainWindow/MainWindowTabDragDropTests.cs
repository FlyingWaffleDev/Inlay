using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Inlay.ViewModels;
using Xunit;

namespace Inlay.Tests;

public sealed class MainWindowTabDragDropTests
{
    [AvaloniaFact]
    public void DroppingTabPastAnotherTabsMidpointMovesItAfterThatTab()
    {
        var (window, viewModel) = MainWindowTestHost.CreateWindow();
        try
        {
            viewModel.AddNewDocument();
            viewModel.AddNewDocument();
            Settle(window);

            var first = viewModel.Documents[0];
            var second = viewModel.Documents[1];
            var third = viewModel.Documents[2];

            Drop(window, first, RightOf(window, second));

            Assert.Equal([second, first, third], viewModel.Documents);
            Assert.Same(first, viewModel.SelectedDocument);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DroppingTabBeforeAnotherTabsMidpointMovesItAheadOfThatTab()
    {
        var (window, viewModel) = MainWindowTestHost.CreateWindow();
        try
        {
            viewModel.AddNewDocument();
            viewModel.AddNewDocument();
            Settle(window);

            var first = viewModel.Documents[0];
            var second = viewModel.Documents[1];
            var third = viewModel.Documents[2];

            Drop(window, third, LeftOf(window, first));

            Assert.Equal([third, first, second], viewModel.Documents);
            Assert.Same(third, viewModel.SelectedDocument);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DragOverShowsDropIndicatorAndDragLeaveHidesIt()
    {
        var (window, viewModel) = MainWindowTestHost.CreateWindow();
        try
        {
            viewModel.AddNewDocument();
            Settle(window);

            var second = viewModel.Documents[1];
            using var transfer = CreateTransfer(window, viewModel.Documents[0]);

            var point = TabBarPoint(window, LeftOf(window, second));
            RaiseDrag(window, RawDragEventType.DragEnter, point, transfer);
            RaiseDrag(window, RawDragEventType.DragOver, point, transfer);
            Assert.True(window.TabDropIndicator.IsVisible);

            RaiseDrag(window, RawDragEventType.DragLeave, point, transfer);
            Assert.False(window.TabDropIndicator.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DroppingTabOnAnotherWindowsTabBarCopiesItIntoThatSlot()
    {
        var (source, sourceViewModel) = MainWindowTestHost.CreateWindow();
        var (target, targetViewModel) = MainWindowTestHost.CreateWindow();
        try
        {
            MainWindowTestHost.FindEditor(source).Text = "Hello from the source window";
            targetViewModel.AddNewDocument();
            Settle(source, target);

            var sourceTab = sourceViewModel.Documents[0];
            var targetFirst = targetViewModel.Documents[0];

            Drop(target, sourceTab, LeftOf(target, targetFirst), source);

            Assert.Single(sourceViewModel.Documents);
            Assert.Same(sourceTab, sourceViewModel.Documents[0]);

            Assert.Equal(3, targetViewModel.Documents.Count);
            var copy = targetViewModel.Documents[0];
            Assert.Same(copy, targetViewModel.SelectedDocument);
            Assert.NotSame(sourceTab, copy);
            Assert.Equal("Hello from the source window", MainWindowTestHost.FindEditor(target).Text);
            Assert.True(copy.IsDirty);
        }
        finally
        {
            source.Close();
            target.Close();
        }
    }

    [AvaloniaFact]
    public void DroppingTabOutsideTheTabBarOnAnotherWindowAppendsTheCopy()
    {
        var (source, sourceViewModel) = MainWindowTestHost.CreateWindow();
        var (target, targetViewModel) = MainWindowTestHost.CreateWindow();
        try
        {
            MainWindowTestHost.FindEditor(source).Text = "Dropped on the editor";
            targetViewModel.AddNewDocument();
            Settle(source, target);

            var editorCentre = MainWindowTestHost.GetCenter(MainWindowTestHost.FindEditor(target), target);
            using var transfer = CreateTransfer(source, sourceViewModel.Documents[0]);
            RaiseDragSequence(target, editorCentre, transfer);
            Settle(target);

            Assert.Equal(3, targetViewModel.Documents.Count);
            Assert.Same(targetViewModel.Documents[2], targetViewModel.SelectedDocument);
            Assert.Equal("Dropped on the editor", MainWindowTestHost.FindEditor(target).Text);
        }
        finally
        {
            source.Close();
            target.Close();
        }
    }

    [AvaloniaFact]
    public void DroppingTabOnAWindowHoldingOnlyAPristineUntitledReplacesIt()
    {
        var (source, sourceViewModel) = MainWindowTestHost.CreateWindow();
        var (target, targetViewModel) = MainWindowTestHost.CreateWindow();
        try
        {
            MainWindowTestHost.FindEditor(source).Text = "Shared text";
            Settle(source, target);

            Assert.True(Assert.Single(targetViewModel.Documents).IsEmptyUntitled());

            Drop(target, sourceViewModel.Documents[0], LeftOf(target, targetViewModel.Documents[0]), source);

            Assert.Single(targetViewModel.Documents);
            Assert.Equal("Shared text", MainWindowTestHost.FindEditor(target).Text);
            Assert.True(targetViewModel.Documents[0].IsDirty);
            Assert.Single(sourceViewModel.Documents);
        }
        finally
        {
            source.Close();
            target.Close();
        }
    }

    [AvaloniaFact]
    public void DroppingTheSameTabTwiceSelectsTheExistingCopyInsteadOfDuplicating()
    {
        var (source, sourceViewModel) = MainWindowTestHost.CreateWindow();
        var (target, targetViewModel) = MainWindowTestHost.CreateWindow();
        try
        {
            MainWindowTestHost.FindEditor(source).Text = "Single instance tab";
            targetViewModel.AddNewDocument();
            Settle(source, target);

            var sourceTab = sourceViewModel.Documents[0];
            var slot = LeftOf(target, targetViewModel.Documents[0]);

            Drop(target, sourceTab, slot, source);
            Assert.Equal(3, targetViewModel.Documents.Count);

            Drop(target, sourceTab, slot, source);
            Assert.Equal(3, targetViewModel.Documents.Count);
            Assert.Single(sourceViewModel.Documents);
        }
        finally
        {
            source.Close();
            target.Close();
        }
    }

    [AvaloniaFact]
    public void AWindowRejectsATabItAlreadyHoldsACopyOf()
    {
        var (source, sourceViewModel) = MainWindowTestHost.CreateWindow();
        var (target, targetViewModel) = MainWindowTestHost.CreateWindow();
        try
        {
            var sourceTab = sourceViewModel.Documents[0];
            var payload = new TabDragPayload { SourceWindow = source, SourceTab = sourceTab };

            Assert.True(target.CanAcceptTabDrop(payload));
            Assert.True(source.CanAcceptTabDrop(payload));

            var copy = targetViewModel.CopyDocument(sourceTab);

            Assert.False(target.CanAcceptTabDrop(payload));
            Assert.False(source.CanAcceptTabDrop(
                new TabDragPayload { SourceWindow = target, SourceTab = copy }));
        }
        finally
        {
            source.Close();
            target.Close();
        }
    }

    [AvaloniaFact]
    public async Task SavingACopiedDocumentWarnsTheOtherWindowThatAnotherWindowChangedIt()
    {
        var (source, sourceViewModel) = MainWindowTestHost.CreateWindow();
        var (target, targetViewModel) = MainWindowTestHost.CreateWindow();
        try
        {
            var file = new MemoryDocumentFile("drag-drop-sample.itd");
            file.SetDocumentText("Initial text");
            var sourceTab = sourceViewModel.CopyDocument(
                new Inlay.Models.TemplateDocument
                {
                    Content = [Inlay.Models.DocumentPart.PlainText("Initial text")]
                },
                file,
                isDirty: false);
            Settle(source, target);

            Drop(target, sourceTab, LeftOf(target, targetViewModel.Documents[0]), source);
            Assert.Equal("drag-drop-sample.itd", targetViewModel.SelectedDocument!.FileName);

            MainWindowTestHost.FindEditor(source).Text = "Changed in the source window";
            await MainWindowViewModelTestContext.ObserveAsync(
                sourceViewModel.SaveCommand.Execute(),
                TestContext.Current.CancellationToken);

            targetViewModel.CheckSelectedDocumentForExternalChanges();

            Assert.True(targetViewModel.SelectedDocument.HasExternalChanges);
            Assert.True(targetViewModel.SelectedDocument.ChangedInAnotherWindow);
            Assert.Equal(
                "drag-drop-sample.itd was changed in another window. Reload it or ignore the changes?",
                targetViewModel.SelectedDocument.ExternalChangesMessage);
        }
        finally
        {
            source.Close();
            target.Close();
        }
    }

    [AvaloniaFact]
    public void PressingAndReleasingOnATabWithoutMovingLeavesTheOrderAlone()
    {
        var (window, viewModel) = MainWindowTestHost.CreateWindow();
        try
        {
            viewModel.AddNewDocument();
            Settle(window);

            var first = viewModel.Documents[0];
            var second = viewModel.Documents[1];
            var centre = MainWindowTestHost.GetCenter(FindTabItem(window, first), window);

            window.MouseDown(centre, MouseButton.Left, RawInputModifiers.None);
            window.MouseMove(centre + new Vector(2, 0));
            window.MouseUp(centre + new Vector(2, 0), MouseButton.Left, RawInputModifiers.None);
            Settle(window);

            Assert.Equal([first, second], viewModel.Documents);
        }
        finally
        {
            window.Close();
        }
    }

    private static void Drop(
        MainWindow target,
        DocumentTabViewModel tab,
        Point pointInTarget,
        MainWindow? sourceWindow = null)
    {
        using var transfer = CreateTransfer(sourceWindow ?? target, tab);
        RaiseDragSequence(target, TabBarPoint(target, pointInTarget), transfer);
        Settle(target);
    }

    private static DataTransfer CreateTransfer(MainWindow sourceWindow, DocumentTabViewModel tab)
    {
        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.Create(
            TabDragPayload.Format,
            new TabDragPayload { SourceWindow = sourceWindow, SourceTab = tab }));
        return transfer;
    }

    // Avalonia only routes DragOver and Drop once a drag has entered the window.
    private static void RaiseDragSequence(MainWindow window, Point point, DataTransfer transfer)
    {
        RaiseDrag(window, RawDragEventType.DragEnter, point, transfer);
        RaiseDrag(window, RawDragEventType.DragOver, point, transfer);
        RaiseDrag(window, RawDragEventType.Drop, point, transfer);
    }

    private static void RaiseDrag(
        MainWindow window,
        RawDragEventType type,
        Point point,
        DataTransfer transfer) =>
        window.DragDrop(point, type, transfer, DragDropEffects.Move | DragDropEffects.Copy, RawInputModifiers.None);

    // Drag positions are computed against the tab strip, but the headless drag API
    // hit-tests from the window, so keep the vertical position on the tab bar.
    private static Point TabBarPoint(MainWindow window, Point point) =>
        new(point.X, MainWindowTestHost.GetCenter(window.TabBar, window).Y);

    private static Point LeftOf(MainWindow window, DocumentTabViewModel tab)
    {
        var item = FindTabItem(window, tab);
        return MainWindowTestHost.GetCenter(item, window) - new Vector(item.Bounds.Width / 2 - 2, 0);
    }

    private static Point RightOf(MainWindow window, DocumentTabViewModel tab)
    {
        var item = FindTabItem(window, tab);
        return MainWindowTestHost.GetCenter(item, window) + new Vector(item.Bounds.Width / 2 - 2, 0);
    }

    private static void Settle(params MainWindow[] windows)
    {
        foreach (var window in windows)
        {
            window.UpdateLayout();
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static TabStripItem FindTabItem(MainWindow window, DocumentTabViewModel document) =>
        Assert.Single(
            window.GetVisualDescendants().OfType<TabStripItem>(),
            item => ReferenceEquals(item.DataContext, document));
}
