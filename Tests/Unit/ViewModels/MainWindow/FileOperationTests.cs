using Xunit;

namespace Inlay.Tests;

public sealed class FileOperationTests
{
    [Fact]
    public async Task SavePersistsDocumentStateAndClearsDirtyState()
    {
        var context = MainWindowViewModelTestContext.Create();
        context.ViewModel.Editor.ShowLineLengthIndicators = true;
        context.ViewModel.Editor.EnforceHardLineLengthLimit = true;
        context.ViewModel.Editor.SoftLineLengthLimit = 88;
        context.ViewModel.Editor.HardLineLengthLimit = 108;
        context.Storage.SaveFile = new MemoryDocumentFile("saved.itd");

        await MainWindowViewModelTestContext.ObserveAsync(
            context.ViewModel.SaveCommand.Execute(),
            TestContext.Current.CancellationToken);

        Assert.False(context.ViewModel.IsDirty);
        Assert.Equal("document.itd", context.Storage.SuggestedName);
        Assert.Equal("saved.itd - Inlay", context.ViewModel.Title);
        Assert.Equal("saved.itd", context.ViewModel.SelectedDocument!.Header);
        Assert.NotEmpty(context.Storage.SaveFile.Contents.ToArray());

        context.Storage.SaveFile.Contents.Position = 0;
        var saved = await new JsonTemplateDocumentService().LoadAsync(
            context.Storage.SaveFile.Contents,
            TestContext.Current.CancellationToken);
        Assert.True(saved.LineLength.Show);
        Assert.True(saved.LineLength.Enforce);
        Assert.Equal(88, saved.LineLength.SoftLimit);
        Assert.Equal(108, saved.LineLength.HardLimit);
    }

    [Fact]
    public async Task OpeningAnOpenFileSelectsItsExistingTab()
    {
        var context = MainWindowViewModelTestContext.Create();
        var file = new MemoryDocumentFile("opened.itd", "shared-file");
        file.SetDocumentText("Opened once");
        context.Storage.OpenFile = file;

        await ExecuteOpen(context);
        var openedTab = context.ViewModel.SelectedDocument;
        await ExecuteOpen(context);

        Assert.Single(context.ViewModel.Documents);
        Assert.Same(openedTab, context.ViewModel.SelectedDocument);
        Assert.Equal(1, file.ReadCount);
    }

    [Fact]
    public async Task OpeningAFileReplacesTheOnlyEmptyUntitledTab()
    {
        var context = MainWindowViewModelTestContext.Create();
        var emptyTab = context.ViewModel.SelectedDocument;
        var file = new MemoryDocumentFile("opened.itd");
        file.SetDocumentText("Opened content");
        context.Storage.OpenFile = file;

        await ExecuteOpen(context);

        var openedTab = Assert.Single(context.ViewModel.Documents);
        Assert.NotSame(emptyTab, openedTab);
        Assert.Same(openedTab, context.ViewModel.SelectedDocument);
        Assert.Equal("opened.itd", openedTab.Header);
    }

    [Fact]
    public async Task OpeningAFileKeepsADirtyUntitledTab()
    {
        var context = MainWindowViewModelTestContext.Create();
        var untitledTab = context.ViewModel.SelectedDocument!;
        untitledTab.Editor.ReportContentChanged();
        var file = new MemoryDocumentFile("opened.itd");
        file.SetDocumentText("Opened content");
        context.Storage.OpenFile = file;

        await ExecuteOpen(context);

        Assert.Equal(2, context.ViewModel.Documents.Count);
        Assert.Contains(untitledTab, context.ViewModel.Documents);
        Assert.Equal("opened.itd", context.ViewModel.SelectedDocument!.Header);
    }

    private static Task ExecuteOpen(MainWindowViewModelTestContext context) =>
        MainWindowViewModelTestContext.ObserveAsync(
            context.ViewModel.OpenCommand.Execute(),
            TestContext.Current.CancellationToken);
}
