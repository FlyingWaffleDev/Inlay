using Inlay.Models;
using Inlay.ViewModels;
using Xunit;

namespace Inlay.Tests;

public sealed class TabMoveAndCopyTests
{
    [Fact]
    public void ReorderDocumentMovesTabsIntoTheSlotAndSelectsThem()
    {
        using var context = MainWindowViewModelTestContext.Create();
        context.ViewModel.AddNewDocument();
        context.ViewModel.AddNewDocument();

        var first = context.ViewModel.Documents[0];
        var second = context.ViewModel.Documents[1];
        var third = context.ViewModel.Documents[2];

        // Slot 2 is the gap between the second and third tabs.
        context.ViewModel.ReorderDocument(first, 2);
        Assert.Equal([second, first, third], context.ViewModel.Documents);
        Assert.Same(first, context.ViewModel.SelectedDocument);

        context.ViewModel.ReorderDocument(third, 0);
        Assert.Equal([third, second, first], context.ViewModel.Documents);
        Assert.Same(third, context.ViewModel.SelectedDocument);
    }

    [Fact]
    public void ReorderDocumentClampsOutOfRangeSlotsAndIgnoresForeignTabs()
    {
        using var context = MainWindowViewModelTestContext.Create();
        context.ViewModel.AddNewDocument();
        var originalOrder = context.ViewModel.Documents.ToList();
        var first = context.ViewModel.Documents[0];

        context.ViewModel.ReorderDocument(first, 0);
        Assert.Equal(originalOrder, context.ViewModel.Documents);

        context.ViewModel.ReorderDocument(first, 99);
        Assert.Equal([originalOrder[1], first], context.ViewModel.Documents);

        using var other = MainWindowViewModelTestContext.Create();
        var foreignTab = other.ViewModel.Documents[0];
        context.ViewModel.ReorderDocument(foreignTab, 0);
        Assert.Equal([originalOrder[1], first], context.ViewModel.Documents);
    }

    [Fact]
    public void CopyDocumentReplacesSinglePristineUntitledDocument()
    {
        using var context = MainWindowViewModelTestContext.Create();
        Assert.Single(context.ViewModel.Documents);
        Assert.True(context.ViewModel.Documents[0].IsEmptyUntitled());

        var document = new TemplateDocument
        {
            Content = [DocumentPart.PlainText("Copied text")]
        };

        var copiedTab = context.ViewModel.CopyDocument(document, null, isDirty: false);

        Assert.Single(context.ViewModel.Documents);
        Assert.Same(copiedTab, context.ViewModel.SelectedDocument);
        Assert.Equal("Copied text", copiedTab.Editor.ExportDocument().Content[0].Text);
    }

    [Fact]
    public void CopyDocumentInsertsAtSpecifiedSlotAndPreservesDirtyState()
    {
        using var context = MainWindowViewModelTestContext.Create();
        context.ViewModel.AddNewDocument();
        context.ViewModel.AddNewDocument();

        var document = new TemplateDocument
        {
            Content = [DocumentPart.PlainText("New dirty document")]
        };

        var copiedTab = context.ViewModel.CopyDocument(document, null, isDirty: true, targetIndex: 1);

        Assert.Equal(4, context.ViewModel.Documents.Count);
        Assert.Same(copiedTab, context.ViewModel.Documents[1]);
        Assert.Same(copiedTab, context.ViewModel.SelectedDocument);
        Assert.True(copiedTab.IsDirty);
        Assert.EndsWith("*", copiedTab.Header, StringComparison.Ordinal);
    }

    [Fact]
    public void CopyDocumentWithExistingFileSelectsAlreadyOpenTab()
    {
        using var context = MainWindowViewModelTestContext.Create();
        var file = new MemoryDocumentFile("document.itd");
        var document = new TemplateDocument { Content = [DocumentPart.PlainText("File content")] };

        var firstCopy = context.ViewModel.CopyDocument(document, file, isDirty: false);
        context.ViewModel.AddNewDocument();

        Assert.Equal(2, context.ViewModel.Documents.Count);
        Assert.NotSame(firstCopy, context.ViewModel.SelectedDocument);

        var secondCopy = context.ViewModel.CopyDocument(document, file, isDirty: false);

        Assert.Equal(2, context.ViewModel.Documents.Count);
        Assert.Same(firstCopy, secondCopy);
        Assert.Same(firstCopy, context.ViewModel.SelectedDocument);
    }

    [Fact]
    public void CopyDocumentPreservesTemplatePartsAndLineLengthSettings()
    {
        using var context = MainWindowViewModelTestContext.Create();
        var document = new TemplateDocument
        {
            LineLength = new LineLengthSettings
            {
                Show = true,
                Enforce = true,
                SoftLimit = 70,
                HardLimit = 90
            },
            Content =
            [
                DocumentPart.PlainText("Hello "),
                DocumentPart.Template(["World", "Inlay"], 1)
            ]
        };

        var copiedTab = context.ViewModel.CopyDocument(document, null, isDirty: false);
        var exported = copiedTab.Editor.ExportDocument();

        Assert.True(exported.LineLength.Show);
        Assert.True(exported.LineLength.Enforce);
        Assert.Equal(70, exported.LineLength.SoftLimit);
        Assert.Equal(90, exported.LineLength.HardLimit);
        Assert.Equal(2, exported.Content.Count);
        Assert.Equal("World", exported.Content[1].Options?[0]);
        Assert.Equal("Inlay", exported.Content[1].Options?[1]);
        Assert.Equal(1, exported.Content[1].SelectedIndex);
    }

    [Fact]
    public void CopyDocumentWithMatchingDocumentIdSelectsExistingTabAndDoesNotDuplicate()
    {
        using var context = MainWindowViewModelTestContext.Create();
        var document = new TemplateDocument
        {
            Content = [DocumentPart.PlainText("Doc with ID")]
        };
        var id = Guid.NewGuid();

        var firstCopy = context.ViewModel.CopyDocument(document, null, isDirty: false, documentId: id);
        context.ViewModel.AddNewDocument();

        Assert.Equal(2, context.ViewModel.Documents.Count);
        Assert.NotSame(firstCopy, context.ViewModel.SelectedDocument);

        var secondCopy = context.ViewModel.CopyDocument(document, null, isDirty: false, documentId: id);

        Assert.Equal(2, context.ViewModel.Documents.Count);
        Assert.Same(firstCopy, secondCopy);
        Assert.Same(firstCopy, context.ViewModel.SelectedDocument);
    }

    [Fact]
    public void ContainsDocumentMatchesOnDocumentIdOrOnFileIdentity()
    {
        using var context = MainWindowViewModelTestContext.Create();
        using var other = MainWindowViewModelTestContext.Create();

        var untitled = context.ViewModel.Documents[0];
        Assert.True(context.ViewModel.ContainsDocument(untitled));
        Assert.False(other.ViewModel.ContainsDocument(untitled));

        // A copy keeps the document id, so the origin still recognises it.
        var copy = other.ViewModel.CopyDocument(untitled);
        Assert.True(context.ViewModel.ContainsDocument(copy));

        // A different document holding the same file counts as the same document too.
        var file = new MemoryDocumentFile("test.itd");
        var fileDoc = new TemplateDocument { Content = [DocumentPart.PlainText("File text")] };
        var fileTab = context.ViewModel.CopyDocument(fileDoc, file, isDirty: false);
        var sameFileElsewhere = other.ViewModel.CopyDocument(fileDoc, file, isDirty: false);
        Assert.NotSame(fileTab, sameFileElsewhere);
        Assert.True(context.ViewModel.ContainsDocument(sameFileElsewhere));

        using var unrelated = MainWindowViewModelTestContext.Create();
        Assert.False(context.ViewModel.ContainsDocument(unrelated.ViewModel.Documents[0]));
    }

    [Fact]
    public async Task CopyDocumentPreservesExternalChangeProtection()
    {
        using var source = MainWindowViewModelTestContext.Create();
        var file = new MemoryDocumentFile("source.itd");
        file.SetDocumentText("Original");
        source.Storage.OpenFile = file;
        await MainWindowViewModelTestContext.ObserveAsync(
            source.ViewModel.OpenCommand.Execute(),
            TestContext.Current.CancellationToken);

        file.SetDocumentText("Changed externally");
        source.ViewModel.CheckSelectedDocumentForExternalChanges();

        using var target = MainWindowViewModelTestContext.Create();
        var copiedTab = target.ViewModel.CopyDocument(source.ViewModel.SelectedDocument!);

        Assert.True(copiedTab.IsDirty);
        Assert.True(copiedTab.HasExternalChanges);
        Assert.False(copiedTab.ChangedInAnotherWindow);
        Assert.False(copiedTab.CanEdit);
    }

    [Fact]
    public async Task CopyDocumentDetectsFileChangeAfterSnapshotWasCaptured()
    {
        using var source = MainWindowViewModelTestContext.Create();
        var file = new MemoryDocumentFile("source.itd");
        file.SetDocumentText("Original");
        source.Storage.OpenFile = file;
        await MainWindowViewModelTestContext.ObserveAsync(
            source.ViewModel.OpenCommand.Execute(),
            TestContext.Current.CancellationToken);

        var sourceTab = source.ViewModel.SelectedDocument!;
        var document = sourceTab.Editor.ExportDocument();
        var fileState = sourceTab.CaptureFileState();
        file.SetDocumentText("Changed during drag");

        using var target = MainWindowViewModelTestContext.Create();
        var copiedTab = target.ViewModel.CopyDocument(
            document,
            file,
            isDirty: false,
            documentId: sourceTab.DocumentId,
            fileState: fileState);

        Assert.True(copiedTab.IsDirty);
        Assert.True(copiedTab.HasExternalChanges);
        Assert.False(copiedTab.ChangedInAnotherWindow);
        Assert.False(copiedTab.CanEdit);
    }
}
