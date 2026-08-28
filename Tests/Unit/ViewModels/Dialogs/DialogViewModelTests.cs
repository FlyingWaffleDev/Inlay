using Inlay.Models;
using Inlay.ViewModels;
using Avalonia.Media;
using Xunit;

namespace Inlay.Tests;

public sealed class DialogViewModelTests
{
    [Fact]
    public void UnsavedChangesCommandsReturnTheirMatchingChoice()
    {
        var choices = new List<UnsavedChoice>();
        var viewModel = new UnsavedChangesDialogViewModel(choices.Add);

        TestCommand.Execute(viewModel.SaveCommand.Execute());
        TestCommand.Execute(viewModel.DiscardCommand.Execute());
        TestCommand.Execute(viewModel.CancelCommand.Execute());

        Assert.Equal(
            [UnsavedChoice.Save, UnsavedChoice.Discard, UnsavedChoice.Cancel],
            choices);
    }

    [Fact]
    public void MessageDialogExposesContentAndClosesThroughItsCommand()
    {
        var closeCount = 0;
        var viewModel = new MessageDialogViewModel(
            "Could not save",
            "The destination is read-only.",
            () => closeCount++);

        TestCommand.Execute(viewModel.CloseCommand.Execute());

        Assert.Equal("Could not save", viewModel.Title);
        Assert.Equal("The destination is read-only.", viewModel.Message);
        Assert.Equal(1, closeCount);
    }

    [Fact]
    public void CloseDialogCommandInvokesTheCloseCallback()
    {
        var closeCount = 0;
        var viewModel = new CloseDialogViewModel(() => closeCount++);

        TestCommand.Execute(viewModel.CloseCommand.Execute());

        Assert.Equal(1, closeCount);
    }

    [Fact]
    public void FontDialogSelectsTheFirstAvailableFallbackFamily()
    {
        var viewModel = new FontDialogViewModel(
            new FontFamily("Cascadia Mono,Consolas,monospace"),
            _ => { });

        Assert.Null(viewModel.SelectedFont);
        Assert.Equal("Cascadia Mono", viewModel.PreviewFontFamily.Name);

        viewModel.LoadFonts(
            [new FontFamily("DejaVu Sans Mono"), new FontFamily("Consolas")]);

        Assert.Equal(
            ["DejaVu Sans Mono", "Consolas"],
            viewModel.Fonts.Select(font => font.Name));
        Assert.Equal("Consolas", viewModel.SelectedFont?.Name);
        Assert.True(viewModel.HasFonts);
        Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public void FontDialogReturnsTheSelectedFontOrNullWhenCancelled()
    {
        var results = new List<FontFamily?>();
        var viewModel = new FontDialogViewModel(
            new FontFamily("Consolas"),
            results.Add);
        viewModel.LoadFonts(
            [new FontFamily("Consolas"), new FontFamily("DejaVu Sans Mono")]);
        viewModel.SelectedFont = viewModel.Fonts[1];

        TestCommand.Execute(viewModel.ConfirmCommand.Execute());
        TestCommand.Execute(viewModel.CancelCommand.Execute());

        Assert.Equal("DejaVu Sans Mono", results[0]?.Name);
        Assert.Null(results[1]);
    }
}
