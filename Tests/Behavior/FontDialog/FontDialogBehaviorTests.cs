using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Inlay.ViewModels;
using Xunit;

namespace Inlay.Tests;

public sealed class FontDialogBehaviorTests
{
    [AvaloniaFact]
    public void DialogOpensWhileFontsLoad()
    {
        var fontsCompletion = new TaskCompletionSource<IReadOnlyList<FontFamily>>();
        var dialog = ShowDialog(fontsCompletion.Task);
        try
        {
            var viewModel = Assert.IsType<FontDialogViewModel>(dialog.DataContext);

            Assert.True(dialog.IsVisible);
            Assert.True(viewModel.IsLoading);
            Assert.Empty(viewModel.Fonts);
            Assert.Equal("Finding installed fonts...", viewModel.StatusMessage);
        }
        finally
        {
            fontsCompletion.TrySetResult([]);
            Dispatcher.UIThread.RunJobs();
            dialog.Close();
        }
    }

    [AvaloniaFact]
    public void SelectingAFontUpdatesThePreview()
    {
        var dialog = ShowDialog(Task.FromResult<IReadOnlyList<FontFamily>>(
            [new FontFamily("Consolas"), new FontFamily("DejaVu Sans Mono")]));
        try
        {
            var viewModel = Assert.IsType<FontDialogViewModel>(dialog.DataContext);
            var fontList = dialog.FindControl<ListBox>("FontList")!;

            Assert.Equal("DejaVu Sans Mono", viewModel.SelectedFont?.Name);
            Assert.Same(viewModel.SelectedFont, fontList.SelectedItem);

            fontList.SelectedItem = viewModel.Fonts[0];
            Dispatcher.UIThread.RunJobs();

            var preview = dialog.GetVisualDescendants()
                .OfType<TextBlock>()
                .Single(text => text.Text?.StartsWith(
                    "The quick brown fox",
                    StringComparison.Ordinal) == true);
            Assert.Equal("Consolas", viewModel.SelectedFont?.Name);
            Assert.Equal("Consolas", preview.FontFamily.Name);
        }
        finally
        {
            dialog.Close();
        }
    }

    [AvaloniaFact]
    public void EmptyFontListDisablesConfirmation()
    {
        var dialog = ShowDialog(Task.FromResult<IReadOnlyList<FontFamily>>([]));
        try
        {
            var viewModel = Assert.IsType<FontDialogViewModel>(dialog.DataContext);

            Assert.False(viewModel.IsLoading);
            Assert.False(viewModel.HasFonts);
            Assert.Null(viewModel.SelectedFont);
            Assert.Equal("No monospace fonts found.", viewModel.StatusMessage);
            Assert.False(dialog.FindControl<Button>("ConfirmButton")!.IsEnabled);
        }
        finally
        {
            dialog.Close();
        }
    }

    [AvaloniaFact]
    public void FontLoadFailureLeavesTheDialogCancellable()
    {
        var dialog = ShowDialog(Task.FromException<IReadOnlyList<FontFamily>>(
            new IOException("Font discovery failed.")));
        try
        {
            var viewModel = Assert.IsType<FontDialogViewModel>(dialog.DataContext);
            var cancel = dialog.GetVisualDescendants()
                .OfType<Button>()
                .Single(button => Equals(button.Content, "Cancel"));

            Assert.True(dialog.IsVisible);
            Assert.False(viewModel.IsLoading);
            Assert.Empty(viewModel.Fonts);
            Assert.Equal("Could not load installed fonts.", viewModel.StatusMessage);
            Assert.True(cancel.IsEnabled);
            Assert.False(dialog.FindControl<Button>("ConfirmButton")!.IsEnabled);
        }
        finally
        {
            dialog.Close();
        }
    }

    private static FontDialog ShowDialog(
        Task<IReadOnlyList<FontFamily>> fonts,
        string currentFont = "DejaVu Sans Mono")
    {
        var dialog = new FontDialog(fonts, new FontFamily(currentFont));
        dialog.Show();
        Dispatcher.UIThread.RunJobs();
        return dialog;
    }
}
