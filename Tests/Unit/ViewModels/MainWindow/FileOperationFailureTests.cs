using Xunit;

namespace Inlay.Tests;

public sealed class FileOperationFailureTests
{
    [Fact]
    public async Task CancellingSaveAsLeavesTheDocumentDirty()
    {
        var context = MainWindowViewModelTestContext.Create();
        context.ViewModel.Editor.ReportContentChanged();
        context.Storage.CancelSaveAs = true;

        await Execute(context.ViewModel.SaveCommand.Execute());

        Assert.True(context.ViewModel.IsDirty);
        Assert.Equal("Untitled *", context.ViewModel.SelectedDocument!.Header);
        Assert.Equal(1, context.Storage.SaveAsCount);
        Assert.Empty(context.Interaction.Messages);
    }

    [Fact]
    public async Task SaveAsRequestsANewFileForANamedDocument()
    {
        var context = MainWindowViewModelTestContext.Create();
        var openedFile = new MemoryDocumentFile("opened.itd");
        openedFile.SetDocumentText("Original");
        context.Storage.OpenFile = openedFile;
        await Execute(context.ViewModel.OpenCommand.Execute());
        var newFile = new MemoryDocumentFile("copy.itd");
        context.Storage.SaveFile = newFile;

        await Execute(context.ViewModel.SaveAsCommand.Execute());

        Assert.Equal(1, context.Storage.SaveAsCount);
        Assert.Equal("opened.itd", context.Storage.SuggestedName);
        Assert.Same(newFile, context.ViewModel.SelectedDocument!.File);
        Assert.Equal("copy.itd", context.ViewModel.SelectedDocument.Header);
    }

    [Fact]
    public async Task SaveFailureReportsTheErrorAndKeepsTheDocumentDirty()
    {
        var context = MainWindowViewModelTestContext.Create();
        context.ViewModel.Editor.ReportContentChanged();
        context.Storage.SaveFile.OpenWriteException = new IOException("Disk is full");

        await Execute(context.ViewModel.SaveCommand.Execute());

        Assert.True(context.ViewModel.IsDirty);
        var message = Assert.Single(context.Interaction.Messages);
        Assert.Equal("Could not save document", message.Title);
        Assert.Equal("Disk is full", message.Message);
    }

    [Fact]
    public async Task InvalidOpenContentReportsTheErrorWithoutReplacingTheCurrentTab()
    {
        var context = MainWindowViewModelTestContext.Create();
        var originalTab = context.ViewModel.SelectedDocument;
        var file = new MemoryDocumentFile("broken.itd");
        file.SetRawContents("{not valid json");
        context.Storage.OpenFile = file;

        await Execute(context.ViewModel.OpenCommand.Execute());

        Assert.Same(originalTab, context.ViewModel.SelectedDocument);
        Assert.Single(context.ViewModel.Documents);
        var message = Assert.Single(context.Interaction.Messages);
        Assert.Equal("Could not open document", message.Title);
        Assert.NotEmpty(message.Message);
    }

    private static Task Execute<T>(IObservable<T> command) =>
        MainWindowViewModelTestContext.ObserveAsync(
            command,
            TestContext.Current.CancellationToken);
}
