using Inlay.Models;
using Inlay.ViewModels;
using Xunit;

namespace Inlay.Tests;

public sealed class DocumentTabViewModelTests
{
    [Fact]
    public void AnUnchangedFileVersionDoesNotMarkTheDocumentDirty()
    {
        var file = new MemoryDocumentFile("document.itd");
        file.SetDocumentText("Original");
        using var tab = CreateTab(DocumentWithText("Original"), file);

        tab.CheckForExternalChanges();

        Assert.False(tab.IsDirty);
        Assert.False(tab.HasExternalChanges);
        Assert.True(tab.CanEdit);
    }

    [Fact]
    public void AnUnavailableFileVersionDoesNotReportAnExternalChange()
    {
        var file = new MemoryDocumentFile("document.itd");
        file.SetDocumentText("Original");
        using var tab = CreateTab(DocumentWithText("Original"), file);

        file.HasVersion = false;
        tab.CheckForExternalChanges();
        file.HasVersion = true;
        tab.CheckForExternalChanges();

        Assert.False(tab.IsDirty);
        Assert.False(tab.HasExternalChanges);
    }

    [Fact]
    public void ReloadClearsDirtyAndExternalChangeState()
    {
        var file = new MemoryDocumentFile("document.itd");
        file.SetDocumentText("Original");
        using var tab = CreateTab(DocumentWithText("Original"), file);
        var editor = new FakeEditorAdapter();
        tab.Editor.Attach(editor);
        editor.Document = DocumentWithText("Local edit");
        tab.Editor.ReportContentChanged();
        file.SetDocumentText("Changed elsewhere");
        tab.CheckForExternalChanges();
        var reloaded = DocumentWithText("Changed elsewhere");

        tab.Reload(reloaded);

        Assert.False(tab.IsDirty);
        Assert.False(tab.HasExternalChanges);
        Assert.True(tab.CanEdit);
        Assert.Same(reloaded, editor.Document);
    }

    [Fact]
    public void ChangingLineLengthSettingsMarksTheDocumentDirty()
    {
        using var tab = CreateTab(new TemplateDocument());

        tab.Editor.ShowLineLengthIndicators = true;

        Assert.True(tab.IsDirty);
        Assert.True(tab.Editor.ExportDocument().LineLength.Show);
    }

    [Theory]
    [InlineData(0, "Untitled (Hello Ada)")]
    [InlineData(-1, "Untitled (Hello _____)")]
    public void UntitledHeaderUsesTheTemplateDisplayText(int selectedIndex, string expectedHeader)
    {
        var document = new TemplateDocument
        {
            Content =
            [
                DocumentPart.PlainText("Hello "),
                DocumentPart.Template(["Ada", "Grace"], selectedIndex)
            ]
        };

        using var tab = CreateTab(document);

        Assert.Equal(expectedHeader, tab.Header);
    }

    private static DocumentTabViewModel CreateTab(
        TemplateDocument document,
        IDocumentFile? file = null) =>
        new(document, _ => Task.CompletedTask, file);

    private static TemplateDocument DocumentWithText(string text) =>
        new() { Content = [DocumentPart.PlainText(text)] };
}
