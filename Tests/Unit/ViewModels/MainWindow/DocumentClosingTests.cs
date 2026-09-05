using Inlay.Models;
using Xunit;

namespace Inlay.Tests;

public sealed class DocumentClosingTests
{
    [Fact]
    public async Task ClosingDirtyTabHonorsCancelChoice()
    {
        using var context = MainWindowViewModelTestContext.Create();
        var tab = context.ViewModel.SelectedDocument!;
        tab.Editor.ReportContentChanged();
        context.Interaction.UnsavedChoice = UnsavedChoice.Cancel;

        await MainWindowViewModelTestContext.ObserveAsync(
            context.ViewModel.CloseTabCommand.Execute(tab),
            TestContext.Current.CancellationToken);

        Assert.Contains(tab, context.ViewModel.Documents);
    }

    [Fact]
    public async Task ClosingOnlyTabThroughTabCommandSelectsReplacementFirst()
    {
        using var context = MainWindowViewModelTestContext.Create();
        var original = context.ViewModel.SelectedDocument!;

        await MainWindowViewModelTestContext.ObserveAsync(
            original.CloseCommand.Execute(),
            TestContext.Current.CancellationToken);

        var replacement = Assert.Single(context.ViewModel.Documents);
        Assert.NotSame(original, replacement);
        Assert.Same(replacement, context.ViewModel.SelectedDocument);
        Assert.Same(replacement.Editor, context.ViewModel.Editor);
        Assert.Equal("Untitled", replacement.Header);
        Assert.Equal("Untitled - Inlay", context.ViewModel.Title);

        context.ViewModel.AddNewDocument();

        Assert.Equal("Untitled (2)", context.ViewModel.SelectedDocument!.Header);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public async Task CloseHonorsUnsavedChoice(int choiceValue, bool expected)
    {
        using var context = MainWindowViewModelTestContext.Create();
        context.ViewModel.Editor!.ReportContentChanged();
        context.Interaction.UnsavedChoice = (UnsavedChoice)choiceValue;

        Assert.Equal(expected, await context.ViewModel.CanCloseAsync());
    }

    [Fact]
    public async Task SaveFailurePreventsClosingADirtyDocument()
    {
        using var context = MainWindowViewModelTestContext.Create();
        var document = context.ViewModel.SelectedDocument!;
        document.Editor.ReportContentChanged();
        context.Interaction.UnsavedChoice = UnsavedChoice.Save;
        context.Storage.SaveFile.OpenWriteException = new IOException("Disk is full");

        await context.ViewModel.CloseDocumentAsync(document);

        Assert.Contains(document, context.ViewModel.Documents);
        Assert.True(document.IsDirty);
        var message = Assert.Single(context.Interaction.Messages);
        Assert.Equal("Could not save document", message.Title);
    }

    [Fact]
    public async Task SuccessfulSaveAllowsClosingADirtyDocument()
    {
        using var context = MainWindowViewModelTestContext.Create();
        var document = context.ViewModel.SelectedDocument!;
        document.Editor.ReportContentChanged();
        context.Interaction.UnsavedChoice = UnsavedChoice.Save;
        context.Storage.SaveFile = new MemoryDocumentFile("saved.itd");

        await context.ViewModel.CloseDocumentAsync(document);

        Assert.DoesNotContain(document, context.ViewModel.Documents);
        Assert.Single(context.ViewModel.Documents);
        Assert.NotEmpty(context.Storage.SaveFile.Contents.ToArray());
        Assert.Empty(context.Interaction.Messages);
    }

    [Fact]
    public async Task ClosingMultipleDirtyDocumentsStopsAtCancel()
    {
        using var context = MainWindowViewModelTestContext.Create();
        context.ViewModel.SelectedDocument!.Editor.ReportContentChanged();
        context.ViewModel.AddNewDocument();
        var second = context.ViewModel.SelectedDocument!;
        second.Editor.ReportContentChanged();
        context.Interaction.UnsavedChoices.Enqueue(UnsavedChoice.Discard);
        context.Interaction.UnsavedChoices.Enqueue(UnsavedChoice.Cancel);

        var canClose = await context.ViewModel.CanCloseAsync();

        Assert.False(canClose);
        Assert.Equal(2, context.Interaction.UnsavedConfirmationCount);
        Assert.Same(second, context.ViewModel.SelectedDocument);
    }
}
