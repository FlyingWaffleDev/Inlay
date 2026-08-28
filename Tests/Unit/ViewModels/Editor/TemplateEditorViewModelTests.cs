using Inlay.Models;
using Inlay.ViewModels;
using Xunit;

namespace Inlay.Tests;

public sealed class TemplateEditorViewModelTests
{
    [Fact]
    public void AttachingAppliesADocumentLoadedWhileDetached()
    {
        var viewModel = new TemplateEditorViewModel();
        var document = DocumentWithText("Loaded before the view exists");
        var editor = new FakeEditorAdapter();
        viewModel.LoadDocument(document);

        viewModel.Attach(editor);

        Assert.Same(document, editor.Document);
        Assert.Equal(1, editor.LoadCount);
        Assert.Same(document, viewModel.ExportDocument());
    }

    [Fact]
    public void DetachingPreservesTheLatestDocumentForTheNextEditor()
    {
        var viewModel = new TemplateEditorViewModel();
        var firstEditor = new FakeEditorAdapter();
        viewModel.Attach(firstEditor);
        var editedDocument = DocumentWithText("Edited in the first view");
        firstEditor.Document = editedDocument;

        viewModel.Detach(firstEditor);
        var secondEditor = new FakeEditorAdapter();
        viewModel.Attach(secondEditor);

        Assert.Same(editedDocument, secondEditor.Document);
        Assert.Equal(1, secondEditor.LoadCount);
    }

    [Fact]
    public void DetachingAnInactiveEditorDoesNotDisconnectTheCurrentEditor()
    {
        var viewModel = new TemplateEditorViewModel();
        var oldEditor = new FakeEditorAdapter();
        var currentEditor = new FakeEditorAdapter();
        viewModel.Attach(oldEditor);
        viewModel.Attach(currentEditor);

        viewModel.Detach(oldEditor);
        var document = DocumentWithText("Still sent to the current view");
        viewModel.LoadDocument(document);

        Assert.Same(document, currentEditor.Document);
        Assert.Equal(1, currentEditor.LoadCount);
        Assert.Equal(0, oldEditor.LoadCount);
    }

    [Fact]
    public void SnapshotUpdatesDiagnosticsAndHistoryState()
    {
        var viewModel = new TemplateEditorViewModel();

        viewModel.UpdateSnapshot(new EditorSnapshot(
            42, 7, 3, 11,
            CanUndo: true,
            CanRedo: false,
            CanCut: true,
            CanCopy: true,
            CanPaste: true,
            CanDelete: true,
            CanSelectAll: true));

        Assert.Equal("Characters 42   Words 7   Line 3   Column 11", viewModel.Diagnostics);
        Assert.True(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);
        Assert.True(viewModel.CanCut);
        Assert.True(viewModel.CanCopy);
        Assert.True(viewModel.CanPaste);
        Assert.True(viewModel.CanDelete);
        Assert.True(viewModel.CanSelectAll);
    }

    [Fact]
    public void LineLengthSettingsLoadAndExportWithTheDocument()
    {
        var viewModel = new TemplateEditorViewModel();
        var document = new TemplateDocument
        {
            LineLength = new LineLengthSettings
            {
                Show = true,
                Enforce = true,
                SoftLimit = 70,
                HardLimit = 90
            }
        };

        viewModel.LoadDocument(document);

        Assert.True(viewModel.ShowLineLengthIndicators);
        Assert.True(viewModel.EnforceHardLineLengthLimit);
        Assert.Equal(70, viewModel.SoftLineLengthLimit);
        Assert.Equal(90, viewModel.HardLineLengthLimit);

        viewModel.HardLineLengthLimit = 100;
        var exported = viewModel.ExportDocument();
        Assert.True(exported.LineLength.Show);
        Assert.True(exported.LineLength.Enforce);
        Assert.Equal(70, exported.LineLength.SoftLimit);
        Assert.Equal(100, exported.LineLength.HardLimit);
    }

    [Fact]
    public void EditingCommandsReachTheAttachedEditor()
    {
        var viewModel = new TemplateEditorViewModel();
        var editor = new FakeEditorAdapter();
        viewModel.Attach(editor);

        viewModel.InsertTemplate();
        viewModel.Undo();
        viewModel.Redo();
        viewModel.Cut();
        viewModel.Copy();
        viewModel.Paste();
        viewModel.Delete();
        viewModel.SelectAll();
        viewModel.Find();
        viewModel.Replace();

        Assert.Equal(1, editor.InsertCount);
        Assert.Equal(1, editor.UndoCount);
        Assert.Equal(1, editor.RedoCount);
        Assert.Equal(1, editor.CutCount);
        Assert.Equal(1, editor.CopyCount);
        Assert.Equal(1, editor.PasteCount);
        Assert.Equal(1, editor.DeleteCount);
        Assert.Equal(1, editor.SelectAllCount);
        Assert.Equal(1, editor.FindCount);
        Assert.Equal(1, editor.ReplaceCount);
    }

    private static TemplateDocument DocumentWithText(string text) =>
        new() { Content = [DocumentPart.PlainText(text)] };
}
