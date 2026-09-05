using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Inlay.Models;
using Xunit;

namespace Inlay.Tests;

public sealed class LineLengthBehaviorTests
{
    [AvaloniaFact]
    public void FlyoutControlsUpdateTheSelectedDocumentAndEditor()
    {
        var (window, viewModel) = MainWindowTestHost.CreateWindow();
        try
        {
            var button = window.FindControl<Button>("LineLengthButton")!;
            var flyout = Assert.IsType<Flyout>(button.Flyout);
            flyout.ShowAt(button);
            Dispatcher.UIThread.RunJobs();

            window.FindControl<CheckBox>("ShowLineLengthLimitsToggle")!.IsChecked = true;
            window.FindControl<CheckBox>("EnforceLineLengthLimitsToggle")!.IsChecked = true;
            var softInput = window.FindControl<NumericUpDown>("SoftLineLengthInput")!;
            var hardInput = window.FindControl<NumericUpDown>("HardLineLengthInput")!;
            softInput.Value = 88;
            hardInput.Value = 108;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(LineLengthSettings.MinimumLimit, softInput.Minimum);
            Assert.Equal(LineLengthSettings.MinimumLimit, hardInput.Minimum);
            Assert.Equal(LineLengthSettings.MaximumLimit, hardInput.Maximum);

            var editor = MainWindowTestHost.FindEditor(window);
            Assert.Equal(
                (true, true, 88, 108),
                (viewModel.Editor.ShowLineLengthIndicators,
                    viewModel.Editor.EnforceHardLineLengthLimit,
                    viewModel.Editor.SoftLineLengthLimit,
                    viewModel.Editor.HardLineLengthLimit));
            Assert.Equal(
                (true, true, 88, 108),
                (editor.ShowLineLengthIndicators,
                    editor.EnforceHardLineLengthLimit,
                    editor.SoftLineLengthLimit,
                    editor.HardLineLengthLimit));
            Assert.True(viewModel.IsDirty);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task OpeningADocumentAppliesItsLineSettingsWithoutMarkingItDirty()
    {
        using var context = MainWindowViewModelTestContext.Create();
        var file = new MemoryDocumentFile("configured.itd");
        file.SetRawContents("""
            {
              "formatVersion": 1,
              "lineLength": {
                "show": true,
                "enforce": true,
                "softLimit": 72,
                "hardLimit": 96
              },
              "content": [{ "type": "text", "text": "Loaded content" }]
            }
            """);
        context.Storage.OpenFile = file;
        var window = new MainWindow(context.ViewModel);
        window.Show();
        try
        {
            await MainWindowViewModelTestContext.ObserveAsync(
                context.ViewModel.OpenCommand.Execute(),
                TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();

            var document = context.ViewModel.SelectedDocument!;
            var editor = MainWindowTestHost.FindEditor(window);
            Assert.False(document.IsDirty);
            Assert.Equal("Loaded content", editor.Text);
            Assert.Equal(
                (true, true, 72, 96),
                (editor.ShowLineLengthIndicators,
                    editor.EnforceHardLineLengthLimit,
                    editor.SoftLineLengthLimit,
                    editor.HardLineLengthLimit));
        }
        finally
        {
            window.Close();
        }
    }
}
