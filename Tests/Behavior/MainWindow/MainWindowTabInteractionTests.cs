using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Inlay.ViewModels;
using Xunit;

namespace Inlay.Tests;

public sealed class MainWindowTabInteractionTests
{
    [AvaloniaFact]
    public void SwitchingTabsKeepsEachEditorsContent()
    {
        var (window, viewModel) = MainWindowTestHost.CreateWindow();
        try
        {
            var firstTab = viewModel.SelectedDocument!;
            Assert.Empty(MainWindowTestHost.FindEditor(window).Text);
            MainWindowTestHost.FindEditor(window).Text = "Text in the first tab";

            TestCommand.Execute(viewModel.NewCommand.Execute());

            Assert.Equal(2, viewModel.Documents.Count);
            Assert.Empty(MainWindowTestHost.FindEditor(window).Text);

            viewModel.SelectedDocument = firstTab;

            Assert.Equal("Text in the first tab", MainWindowTestHost.FindEditor(window).Text);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SwitchingTabsFocusesTheEditorAtTheRestoredCaret()
    {
        var (window, viewModel) = MainWindowTestHost.CreateWindow();
        try
        {
            var firstTab = viewModel.SelectedDocument!;
            var editor = MainWindowTestHost.FindEditor(window);
            editor.Text = "abcd";
            editor.CaretOffset = 2;

            viewModel.AddNewDocument();
            window.UpdateLayout();
            var firstTabItem = window.GetVisualDescendants()
                .OfType<TabStripItem>()
                .Single(item => ReferenceEquals(item.DataContext, firstTab));

            MainWindowTestHost.Click(
                window,
                MainWindowTestHost.GetCenter(firstTabItem, window),
                MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.Same(firstTab, viewModel.SelectedDocument);
            Assert.Equal(2, editor.CaretOffset);
            Assert.True(editor.TextArea.IsFocused);

            window.KeyTextInput("X");

            Assert.Equal("abXcd", editor.Text);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DoubleClickingEmptyTabBarCreatesTab()
    {
        var (window, viewModel) = MainWindowTestHost.CreateWindow();
        try
        {
            var tabsViewport = window.FindControl<ScrollViewer>("TabsScrollViewer")!;
            var point = tabsViewport.TranslatePoint(
                new Point(tabsViewport.Bounds.Width - 10, tabsViewport.Bounds.Height / 2),
                window)!.Value;

            MainWindowTestHost.Click(window, point, MouseButton.Left);
            MainWindowTestHost.Click(window, point, MouseButton.Left);

            Assert.Equal(2, viewModel.Documents.Count);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void OverflowingTabsStayOnOneScrollableRow()
    {
        var (window, viewModel) = CreateWindowWithOverflowingTabs();
        try
        {
            var tabsViewport = window.FindControl<ScrollViewer>("TabsScrollViewer")!;
            var scrollButtons = window.FindControl<StackPanel>("TabScrollButtons")!;
            var leftButton = window.FindControl<Button>("ScrollTabsLeftButton")!;
            var rightButton = window.FindControl<Button>("ScrollTabsRightButton")!;
            var tabs = window.GetVisualDescendants().OfType<TabStripItem>().ToList();

            Assert.True(scrollButtons.IsVisible);
            Assert.True(tabsViewport.Extent.Width > tabsViewport.Viewport.Width);
            Assert.All(tabs, tab => Assert.Equal(tabs[0].Bounds.Y, tab.Bounds.Y));
            Assert.True(tabsViewport.Offset.X > 0);

            viewModel.SelectedDocument = viewModel.Documents[0];
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            Assert.Equal(0, tabsViewport.Offset.X);
            Assert.False(leftButton.IsEnabled);
            Assert.True(rightButton.IsEnabled);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void OverflowingTabsScrollWithWheelAndButtons()
    {
        var (window, viewModel) = CreateWindowWithOverflowingTabs();
        try
        {
            var tabsViewport = window.FindControl<ScrollViewer>("TabsScrollViewer")!;
            var leftButton = window.FindControl<Button>("ScrollTabsLeftButton")!;
            var rightButton = window.FindControl<Button>("ScrollTabsRightButton")!;
            viewModel.SelectedDocument = viewModel.Documents[0];
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            window.MouseWheel(
                MainWindowTestHost.GetCenter(tabsViewport, window),
                new Vector(0, -1),
                RawInputModifiers.None);
            window.UpdateLayout();

            Assert.True(tabsViewport.Offset.X > 0);
            Assert.True(leftButton.IsEnabled);

            MainWindowTestHost.Click(
                window,
                MainWindowTestHost.GetCenter(leftButton, window),
                MouseButton.Left);
            window.UpdateLayout();

            Assert.Equal(0, tabsViewport.Offset.X);
            Assert.False(leftButton.IsEnabled);

            MainWindowTestHost.Click(
                window,
                MainWindowTestHost.GetCenter(rightButton, window),
                MouseButton.Left);
            window.UpdateLayout();

            Assert.True(tabsViewport.Offset.X > 0);
            Assert.True(leftButton.IsEnabled);

            var rightOffset = tabsViewport.Offset.X;
            MainWindowTestHost.Click(
                window,
                MainWindowTestHost.GetCenter(leftButton, window),
                MouseButton.Left);
            window.UpdateLayout();

            Assert.True(tabsViewport.Offset.X < rightOffset);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void MiddleClickingTabClosesIt()
    {
        var (window, viewModel) = MainWindowTestHost.CreateWindow();
        try
        {
            var firstTab = viewModel.SelectedDocument!;
            viewModel.AddNewDocument();
            var tabItem = window.GetVisualDescendants()
                .OfType<TabStripItem>()
                .Single(item => ReferenceEquals(item.DataContext, firstTab));

            MainWindowTestHost.Click(
                window,
                MainWindowTestHost.GetCenter(tabItem, window),
                MouseButton.Middle);

            Assert.DoesNotContain(firstTab, viewModel.Documents);
            Assert.Single(viewModel.Documents);
        }
        finally
        {
            window.Close();
        }
    }

    private static (MainWindow Window, MainWindowViewModel ViewModel)
        CreateWindowWithOverflowingTabs()
    {
        var context = MainWindowTestHost.CreateWindow();
        for (var index = 0; index < 11; index++)
        {
            context.ViewModel.AddNewDocument();
        }

        context.Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        context.Window.UpdateLayout();
        return context;
    }

}
