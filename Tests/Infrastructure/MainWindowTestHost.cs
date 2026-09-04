using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Inlay.Controls;
using Inlay.ViewModels;
using Xunit;

namespace Inlay.Tests;

internal static class MainWindowTestHost
{
    public static (MainWindow Window, MainWindowViewModel ViewModel) CreateWindow(
        IDocumentStorageService? storageService = null)
    {
        var viewModel = new MainWindowViewModel(
            new JsonTemplateDocumentService(),
            storageService ?? new NullStorageService(),
            new NullInteractionService(),
            new FakeApplicationService());
        var window = new MainWindow(viewModel);
        window.Show();
        return (window, viewModel);
    }

    public static TemplateTextEditor FindEditor(MainWindow window) =>
        Assert.Single(window.GetVisualDescendants().OfType<TemplateTextEditor>());

    public static Point GetCenter(Control control, MainWindow window) =>
        control.TranslatePoint(
            new Point(control.Bounds.Width / 2, control.Bounds.Height / 2),
            window)!.Value;

    public static void Click(MainWindow window, Point point, MouseButton button)
    {
        window.MouseDown(point, button, RawInputModifiers.None);
        window.MouseUp(point, button, RawInputModifiers.None);
    }

    private sealed class NullStorageService : IDocumentStorageService
    {
        public Task<IDocumentFile?> OpenAsync() => Task.FromResult<IDocumentFile?>(null);
        public Task<IDocumentFile?> SaveAsAsync(string suggestedName) =>
            Task.FromResult<IDocumentFile?>(null);
    }

    private sealed class NullInteractionService : IUserInteractionService
    {
        public Task<UnsavedChoice> ConfirmUnsavedChangesAsync() =>
            Task.FromResult(UnsavedChoice.Discard);

        public Task<FontFamily?> SelectFontAsync(FontFamily currentFontFamily) =>
            Task.FromResult<FontFamily?>(null);

        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task ShowTemplateHelpAsync() => Task.CompletedTask;
        public Task ShowAboutAsync() => Task.CompletedTask;
    }
}
