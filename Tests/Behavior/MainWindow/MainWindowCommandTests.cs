using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Inlay.Controls;
using Xunit;

namespace Inlay.Tests;

public sealed class MainWindowCommandTests
{
    [AvaloniaFact]
    public void NativeMenusBindApplicationCommands()
    {
        var (window, viewModel) = MainWindowTestHost.CreateWindow();
        try
        {
            var menu = Assert.IsType<NativeMenu>(NativeMenu.GetMenu(window));

            Assert.Equal(
                ["File", "Edit", "View", "Help"],
                menu.Items.OfType<NativeMenuItem>().Select(item => item.Header));
            Assert.All(CommandItems(menu), item => Assert.NotNull(item.Command));
            var editMenu = menu.Items.OfType<NativeMenuItem>()
                .Single(item => item.Header == "Edit").Menu!;
            Assert.Contains(
                editMenu.Items.OfType<NativeMenuItem>(),
                item => item.Header == "Font");
            var insertTemplateItem = editMenu.Items.OfType<NativeMenuItem>()
                .Single(item => item.Header == "Insert Template");
            Assert.Equal("Ctrl+T", insertTemplateItem.Gesture?.ToString());
            Assert.Same(viewModel.InsertTemplateCommand, insertTemplateItem.Command);
            Assert.Single(window.GetVisualDescendants().OfType<NativeMenuBar>());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void StandardShortcutsAreBound()
    {
        var (window, viewModel) = MainWindowTestHost.CreateWindow();
        try
        {
            var actual = window.KeyBindings.ToDictionary(
                binding => binding.Gesture.ToString(),
                binding => binding.Command);
            var expected = new Dictionary<string, ICommand>
            {
                ["Ctrl+N"] = viewModel.NewCommand,
                ["Ctrl+Shift+N"] = viewModel.NewWindowCommand,
                ["Ctrl+O"] = viewModel.OpenCommand,
                ["Ctrl+S"] = viewModel.SaveCommand,
                ["Ctrl+Shift+S"] = viewModel.SaveAsCommand,
                ["Ctrl+W"] = viewModel.CloseTabCommand,
                ["Ctrl+Q"] = viewModel.ExitCommand,
                ["Ctrl+Z"] = viewModel.UndoCommand,
                ["Ctrl+Y"] = viewModel.RedoCommand,
                ["Ctrl+X"] = viewModel.CutCommand,
                ["Ctrl+C"] = viewModel.CopyCommand,
                ["Ctrl+V"] = viewModel.PasteCommand,
                ["Ctrl+T"] = viewModel.InsertTemplateCommand,
                ["Ctrl+A"] = viewModel.SelectAllCommand,
                ["Ctrl+F"] = viewModel.FindCommand,
                ["Ctrl+H"] = viewModel.ReplaceCommand,
                ["Ctrl+OemPlus"] = viewModel.ZoomInCommand,
                ["Ctrl+Shift+OemPlus"] = viewModel.ZoomInCommand,
                ["Ctrl+Add"] = viewModel.ZoomInCommand,
                ["Ctrl+OemMinus"] = viewModel.ZoomOutCommand,
                ["Ctrl+Subtract"] = viewModel.ZoomOutCommand,
                ["Ctrl+D0"] = viewModel.RestoreDefaultZoomCommand,
                ["Ctrl+NumPad0"] = viewModel.RestoreDefaultZoomCommand
            };

            Assert.Equal(expected.Count, actual.Count);
            Assert.All(expected, binding => Assert.Same(binding.Value, actual[binding.Key]));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SearchShortcutsControlTheEditorPanel()
    {
        var (window, _) = MainWindowTestHost.CreateWindow();
        try
        {
            var editor = MainWindowTestHost.FindEditor(window);
            editor.Text = "cat dog cat";
            editor.Select(0, 3);

            PressShortcut(window, Key.F);
            Assert.True(editor.SearchPanel.IsOpened);
            Assert.False(editor.SearchPanel.IsReplaceMode);
            Assert.Equal("cat", editor.SearchPanel.SearchPattern);

            PressShortcut(window, Key.F);
            Assert.True(editor.SearchPanel.IsClosed);

            PressShortcut(window, Key.H);
            Assert.True(editor.SearchPanel.IsOpened);
            Assert.True(editor.SearchPanel.IsReplaceMode);

            PressShortcut(window, Key.H);
            Assert.True(editor.SearchPanel.IsClosed);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DeleteKeyDeletesTheCharacterAfterTheCaret()
    {
        var (window, _) = MainWindowTestHost.CreateWindow();
        try
        {
            var editor = MainWindowTestHost.FindEditor(window);
            editor.Text = "abc";
            editor.CaretOffset = 1;
            Assert.True(editor.TextArea.Focus());
            Assert.True(editor.TextArea.IsFocused);

            window.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.None, null);
            window.KeyRelease(Key.Delete, RawInputModifiers.None, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("ac", editor.Text);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void InsertTemplateShortcutInsertsAtTheCaretAndKeepsTypingThere()
    {
        var (window, _) = MainWindowTestHost.CreateWindow();
        try
        {
            var editor = MainWindowTestHost.FindEditor(window);
            editor.Text = "ab";
            editor.CaretOffset = 1;
            Assert.True(editor.TextArea.Focus());

            PressShortcut(window, Key.T);

            Assert.Equal("a_____b", editor.Text);
            Assert.Equal(1 + TemplateTextElement.PlaceholderText.Length, editor.CaretOffset);
            Assert.True(editor.TextArea.IsFocused);

            window.KeyTextInput("X");

            Assert.Equal("a_____Xb", editor.Text);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ViewCommandsUpdateRenderedState()
    {
        var (window, viewModel) = MainWindowTestHost.CreateWindow();
        try
        {
            var editor = MainWindowTestHost.FindEditor(window);
            var statusBar = window.FindControl<Border>("StatusBar")!;

            TestCommand.Execute(viewModel.ZoomInCommand.Execute());
            TestCommand.Execute(viewModel.ToggleWordWrapCommand.Execute());
            TestCommand.Execute(viewModel.ToggleStatusBarCommand.Execute());
            TestCommand.Execute(viewModel.ToggleCurrentLineHighlightCommand.Execute());

            Assert.Equal(16, editor.FontSize);
            Assert.False(editor.ViewportWordWrap);
            Assert.False(editor.WordWrap);
            Assert.False(statusBar.IsVisible);
            Assert.False(editor.Options.HighlightCurrentLine);

            TestCommand.Execute(viewModel.RestoreDefaultZoomCommand.Execute());
            Assert.Equal(15, editor.FontSize);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task FontCommandUpdatesTheRenderedEditorFamily()
    {
        using var context = MainWindowViewModelTestContext.Create();
        var initialFont = context.ViewModel.EditorFontFamily;
        context.Interaction.SelectedFont = new FontFamily("DejaVu Sans Mono");
        var window = new MainWindow(context.ViewModel);
        window.Show();
        try
        {
            await MainWindowViewModelTestContext.ObserveAsync(
                context.ViewModel.FontCommand.Execute(),
                TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();

            var editor = MainWindowTestHost.FindEditor(window);
            Assert.Equal(initialFont, context.Interaction.CurrentFont);
            Assert.Equal("DejaVu Sans Mono", context.ViewModel.EditorFontFamily.Name);
            Assert.Equal("DejaVu Sans Mono", editor.FontFamily.Name);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ControlMouseWheelZoomsOnlyOverEditor()
    {
        var (window, viewModel) = MainWindowTestHost.CreateWindow();
        try
        {
            var editor = window.FindControl<Border>("EditorBorder")!;
            var toolbar = window.FindControl<Border>("ToolbarBar")!;

            Scroll(window, editor, 1, RawInputModifiers.Control);
            Assert.Equal(16, viewModel.EditorFontSize);

            Scroll(window, editor, -1, RawInputModifiers.Control);
            Scroll(window, editor, 1, RawInputModifiers.None);
            Scroll(window, toolbar, 1, RawInputModifiers.Control);
            Assert.Equal(15, viewModel.EditorFontSize);
        }
        finally
        {
            window.Close();
        }
    }

    private static IEnumerable<NativeMenuItem> CommandItems(NativeMenu menu)
    {
        foreach (var item in menu.Items.OfType<NativeMenuItem>()
                     .Where(item => item is not NativeMenuItemSeparator))
        {
            if (item.Menu is null)
            {
                yield return item;
                continue;
            }

            foreach (var child in CommandItems(item.Menu))
            {
                yield return child;
            }
        }
    }

    [AvaloniaFact]
    public async Task ExternalChangesDisableTheEditingFunctionality()
    {
        var file = new MemoryDocumentFile("opened.itd");
        file.SetDocumentText("Original");
        var storage = new FakeStorageService { OpenFile = file };
        var (window, viewModel) = MainWindowTestHost.CreateWindow(storage);
        try
        {
            await TestCommand.ExecuteAsync(
                viewModel.OpenCommand.Execute(),
                TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            var insertButton = ToolbarButton(window, "Insert Template");
            var editor = MainWindowTestHost.FindEditor(window);
            Assert.True(insertButton.IsEffectivelyEnabled);

            file.SetDocumentText("Changed elsewhere");
            viewModel.CheckSelectedDocumentForExternalChanges();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            var textBeforeShortcut = editor.Text;
            Assert.False(viewModel.SelectedDocument!.CanEdit);
            Assert.False(insertButton.IsEffectivelyEnabled);

            PressShortcut(window, Key.T);

            Assert.Equal(textBeforeShortcut, editor.Text);

            TestCommand.Execute(
                viewModel.IgnoreExternalChangesCommand.Execute(viewModel.SelectedDocument));
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            Assert.True(insertButton.IsEffectivelyEnabled);

            PressShortcut(window, Key.T);

            Assert.NotEqual(textBeforeShortcut, editor.Text);
        }
        finally
        {
            window.Close();
        }
    }

    private static Button ToolbarButton(MainWindow window, string automationName) =>
        window.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => AutomationProperties.GetName(button) == automationName);

    private static void PressShortcut(MainWindow window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.Control, PhysicalKey.None, null);
        window.KeyRelease(key, RawInputModifiers.Control, PhysicalKey.None, null);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Scroll(
        MainWindow window,
        Control target,
        double delta,
        RawInputModifiers modifiers) =>
        window.MouseWheel(
            MainWindowTestHost.GetCenter(target, window),
            new Vector(0, delta),
            modifiers);
}
