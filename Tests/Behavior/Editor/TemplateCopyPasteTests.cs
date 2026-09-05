using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using Inlay.Controls;
using Inlay.Models;
using Inlay.ViewModels;
using Xunit;

namespace Inlay.Tests;

public sealed class TemplateCopyPasteTests
{
    [AvaloniaFact]
    public async Task CopyingTemplateSetsPlainTextAndRichFormatOnClipboard()
    {
        using var host = new EditorTestHost(new TemplateDocument
        {
            Content =
            [
                DocumentPart.PlainText("Hello "),
                DocumentPart.Template(["World", "Universe"], 0),
                DocumentPart.PlainText("!")
            ]
        });
        var editor = host.Editor;

        editor.Select(0, editor.Document.TextLength);
        await editor.CopyAsync();

        using var data = await host.Window.Clipboard!.TryGetDataAsync();
        Assert.NotNull(data);

        // Plain text on clipboard matches currently displayed text and active choice
        var plainText = await data.TryGetTextAsync();
        Assert.Equal("Hello World!", plainText);

        // Rich payload contains template with all choices
        var payload = await TemplateClipboard.TryGetPayloadAsync(data);
        Assert.NotNull(payload);
        Assert.Equal(3, payload.Content.Count);
        Assert.Equal("Hello ", payload.Content[0].Text);
        Assert.Equal(DocumentPartKind.Template, payload.Content[1].Type);
        Assert.Equal(["World", "Universe"], payload.Content[1].Options);
        Assert.Equal(0, payload.Content[1].SelectedIndex);
        Assert.Equal("!", payload.Content[2].Text);
    }

    [AvaloniaFact]
    public async Task PastingInsideInlayRestoresTemplateWithChoices()
    {
        using var host = new EditorTestHost(new TemplateDocument
        {
            Content =
            [
                DocumentPart.Template(["Alpha", "Beta", "Gamma"], 1)
            ]
        });
        var editor = host.Editor;

        editor.Select(0, editor.Document.TextLength);
        await editor.CopyAsync();

        // Clear the editor
        editor.LoadDocument(new TemplateDocument());
        Assert.Equal(0, editor.Document.TextLength);

        // Paste inside Inlay
        await editor.PasteAsync();

        Assert.Equal("Beta", editor.Text);
        var exported = editor.ExportDocument();
        var part = Assert.Single(exported.Content);
        Assert.Equal(DocumentPartKind.Template, part.Type);
        Assert.Equal(["Alpha", "Beta", "Gamma"], part.Options);
        Assert.Equal(1, part.SelectedIndex);
    }

    [AvaloniaFact]
    public async Task PastingBetweenTabsPreservesTemplateAndChoicesIndependently()
    {
        var editor1 = CreateEditor();
        editor1.LoadDocument(new TemplateDocument
        {
            Content =
            [
                DocumentPart.PlainText("Choice: "),
                DocumentPart.Template(["Option 1", "Option 2"], 0)
            ]
        });

        var editor2 = CreateEditor();
        editor2.LoadDocument(new TemplateDocument());

        var window = new Window
        {
            Content = new StackPanel
            {
                Children = { editor1, editor2 }
            }
        };
        window.Show();
        try
        {
            editor1.Select(0, editor1.Document.TextLength);
            await editor1.CopyAsync();

            editor2.CaretOffset = 0;
            await editor2.PasteAsync();

            Assert.Equal("Choice: Option 1", editor2.Text);
            var doc2 = editor2.ExportDocument();
            Assert.Equal(2, doc2.Content.Count);
            Assert.Equal(["Option 1", "Option 2"], doc2.Content[1].Options);
            Assert.Equal(0, doc2.Content[1].SelectedIndex);

            // Mutating template in editor2 does not alter editor1
            doc2.Content[1].Options!.Add("Option 3");
            var doc1 = editor1.ExportDocument();
            Assert.Equal(2, doc1.Content[1].Options!.Count);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task PastingBetweenWindowsPreservesTemplateAndChoices()
    {
        using var host1 = new EditorTestHost(new TemplateDocument
        {
            Content =
            [
                DocumentPart.Template(["Choice A", "Choice B"], 1)
            ]
        });
        using var host2 = new EditorTestHost();

        var editor1 = host1.Editor;
        var editor2 = host2.Editor;

        editor1.Select(0, editor1.Document.TextLength);
        await editor1.CopyAsync();

        editor2.CaretOffset = 0;
        await editor2.PasteAsync();

        Assert.Equal("Choice B", editor2.Text);
        var doc = editor2.ExportDocument();
        var part = Assert.Single(doc.Content);
        Assert.Equal(["Choice A", "Choice B"], part.Options);
        Assert.Equal(1, part.SelectedIndex);
    }

    [AvaloniaFact]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Avalonia clipboard takes ownership of outgoing DataTransfer.")]
    public async Task PastingPlainTextFromExternalSourceInsertsAsPlainText()
    {
        using var host = new EditorTestHost();
        var editor = host.Editor;

        // Simulate external copy (only plain text on clipboard, no Inlay format)
        var transfer = TemplateClipboard.CreateTextDataTransfer("External plain text");
        await host.Window.Clipboard!.SetDataAsync(transfer);

        await editor.PasteAsync();

        Assert.Equal("External plain text", editor.Text);
        var doc = editor.ExportDocument();
        var part = Assert.Single(doc.Content);
        Assert.Equal(DocumentPartKind.Text, part.Type);
        Assert.Equal("External plain text", part.Text);
    }

    [AvaloniaFact]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Avalonia clipboard takes ownership of outgoing DataTransfer.")]
    public async Task InvalidTemplatePayloadFallsBackToPlainTextBeforeReplacingSelection()
    {
        using var host = new EditorTestHost(new TemplateDocument
        {
            Content = [DocumentPart.PlainText("Original text")]
        });
        var editor = host.Editor;
        var transfer = TemplateClipboard.CreateDataTransfer(
            "Safe fallback",
            new TemplateClipboardPayload
            {
                Content = [DocumentPart.Template([string.Empty], 0)]
            });
        await host.Window.Clipboard!.SetDataAsync(transfer);
        editor.Select(0, editor.Document.TextLength);

        await editor.PasteAsync();

        Assert.Equal("Safe fallback", editor.Text);
        var part = Assert.Single(editor.ExportDocument().Content);
        Assert.Equal(DocumentPartKind.Text, part.Type);
        Assert.Equal("Safe fallback", part.Text);
    }

    [AvaloniaFact]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Avalonia clipboard takes ownership of outgoing DataTransfer.")]
    public async Task InvalidTemplatePayloadWithoutPlainTextLeavesSelectionUntouched()
    {
        using var host = new EditorTestHost(new TemplateDocument
        {
            Content = [DocumentPart.PlainText("Original text")]
        });
        var editor = host.Editor;
        var payload = new TemplateClipboardPayload
        {
            Content = [DocumentPart.Template([string.Empty], 0)]
        };
        var transfer = new DataTransfer();
        var item = new DataTransferItem();
        item.Set(TemplateClipboardPayload.Format, TemplateClipboard.Serialize(payload));
        item.Set(TemplateClipboardPayload.InProcessFormat, payload);
        transfer.Add(item);
        await host.Window.Clipboard!.SetDataAsync(transfer);
        editor.Select(0, editor.Document.TextLength);

        await editor.PasteAsync();

        Assert.Equal("Original text", editor.Text);
        Assert.Equal("Original text", editor.SelectedText);
        Assert.False(editor.CanUndo);
    }

    [AvaloniaFact]
    public async Task CopyingWithMultipleTemplatesPreservesAllTemplatesAndInterveningText()
    {
        using var host = new EditorTestHost(new TemplateDocument
        {
            Content =
            [
                DocumentPart.PlainText("A: "),
                DocumentPart.Template(["1", "2"], 0),
                DocumentPart.PlainText(" and B: "),
                DocumentPart.Template(["X", "Y", "Z"], 2)
            ]
        });
        var editor = host.Editor;

        editor.Select(0, editor.Document.TextLength);
        await editor.CopyAsync();

        editor.LoadDocument(new TemplateDocument());
        await editor.PasteAsync();

        Assert.Equal("A: 1 and B: Z", editor.Text);
        var doc = editor.ExportDocument();
        Assert.Equal(4, doc.Content.Count);
        Assert.Equal(DocumentPartKind.Text, doc.Content[0].Type);
        Assert.Equal("A: ", doc.Content[0].Text);
        Assert.Equal(DocumentPartKind.Template, doc.Content[1].Type);
        Assert.Equal(["1", "2"], doc.Content[1].Options);
        Assert.Equal(0, doc.Content[1].SelectedIndex);
        Assert.Equal(DocumentPartKind.Text, doc.Content[2].Type);
        Assert.Equal(" and B: ", doc.Content[2].Text);
        Assert.Equal(DocumentPartKind.Template, doc.Content[3].Type);
        Assert.Equal(["X", "Y", "Z"], doc.Content[3].Options);
        Assert.Equal(2, doc.Content[3].SelectedIndex);
    }

    [AvaloniaFact]
    public async Task CuttingTemplateCopiesToClipboardAndDeletesFromDocument()
    {
        using var host = new EditorTestHost(new TemplateDocument
        {
            Content =
            [
                DocumentPart.PlainText("Before "),
                DocumentPart.Template(["CutMe", "KeepMe"], 0),
                DocumentPart.PlainText(" After")
            ]
        });
        var editor = host.Editor;

        // Select "Before CutMe"
        editor.Select(0, "Before CutMe".Length);
        var cutSuccess = await editor.CutAsync();
        Assert.True(cutSuccess);

        Assert.Equal(" After", editor.Text);
        Assert.DoesNotContain(
            editor.ExportDocument().Content,
            part => part.Type == DocumentPartKind.Template);

        // Undo restores the cut template
        editor.Undo();
        Assert.Equal("Before CutMe After", editor.Text);
        Assert.Contains(
            editor.ExportDocument().Content,
            part => part.Type == DocumentPartKind.Template);

        // Paste pastes the cut content
        editor.Select(editor.Document.TextLength, 0);
        await editor.PasteAsync();
        Assert.Equal("Before CutMe AfterBefore CutMe", editor.Text);
    }

    [AvaloniaFact]
    public async Task PasteOverSelectionReplacesSelectedContentAndIsSingleUndoStep()
    {
        using var host = new EditorTestHost(new TemplateDocument
        {
            Content =
            [
                DocumentPart.Template(["OriginalChoice"], 0)
            ]
        });
        var editor = host.Editor;

        editor.Select(0, editor.Document.TextLength);
        await editor.CopyAsync();

        editor.LoadDocument(new TemplateDocument
        {
            Content = [DocumentPart.PlainText("Replace this whole line")]
        });
        editor.Select(0, editor.Document.TextLength);

        await editor.PasteAsync();

        Assert.Equal("OriginalChoice", editor.Text);
        var doc = editor.ExportDocument();
        Assert.Contains(doc.Content, part => part.Type == DocumentPartKind.Template);

        editor.Undo();
        Assert.Equal("Replace this whole line", editor.Text);
        Assert.DoesNotContain(
            editor.ExportDocument().Content,
            part => part.Type == DocumentPartKind.Template);

        editor.Redo();
        Assert.Equal("OriginalChoice", editor.Text);
        Assert.Contains(
            editor.ExportDocument().Content,
            part => part.Type == DocumentPartKind.Template);
    }

    [AvaloniaFact]
    public async Task WholeLineCopyCopiesTemplateOnCurrentLineWhenNoSelection()
    {
        using var host = new EditorTestHost(new TemplateDocument
        {
            Content =
            [
                DocumentPart.PlainText("First\n"),
                DocumentPart.Template(["Line2Template"], 0),
                DocumentPart.PlainText("\nThird")
            ]
        });
        var editor = host.Editor;
        editor.Options.CutCopyWholeLine = true;

        // Position caret on second line with no selection
        editor.CaretOffset = "First\n".Length;
        editor.Select(editor.CaretOffset, 0);

        await editor.CopyAsync();

        // Clear and paste
        editor.LoadDocument(new TemplateDocument());
        await editor.PasteAsync();

        var doc = editor.ExportDocument();
        Assert.Contains(doc.Content, part => part.Type == DocumentPartKind.Template);
        var templatePart = doc.Content.First(part => part.Type == DocumentPartKind.Template);
        Assert.Equal(["Line2Template"], templatePart.Options);
    }

    [AvaloniaFact]
    public async Task ReadOnlyEditorDoesNotCutOrPaste()
    {
        using var host = new EditorTestHost(new TemplateDocument
        {
            Content = [DocumentPart.PlainText("Read only text")]
        });
        var editor = host.Editor;
        editor.IsReadOnly = true;

        Assert.False(editor.CanCut);
        Assert.False(editor.CanPaste);
        Assert.True(editor.CanCopy);

        editor.Select(0, editor.Document.TextLength);
        await editor.CopyAsync();

        // Cut should do nothing
        var cutResult = await editor.CutAsync();
        Assert.False(cutResult);
        Assert.Equal("Read only text", editor.Text);

        // Paste should do nothing
        await editor.PasteAsync();
        Assert.Equal("Read only text", editor.Text);
    }

    [AvaloniaFact]
    public async Task RectangularSelectionCutDeletesOnlySelectedColumns()
    {
        using var host = new EditorTestHost();
        var editor = host.Editor;
        editor.Text = "12345\n12345\n12345";
        Dispatcher.UIThread.RunJobs();

        editor.TextArea.Selection = new RectangleSelection(
            editor.TextArea,
            new TextViewPosition(1, 2),
            new TextViewPosition(3, 4));

        var cutSuccess = await editor.CutAsync();
        Assert.True(cutSuccess);
        Assert.Equal("145\n145\n145", editor.Text);

        editor.Undo();
        Assert.Equal("12345\n12345\n12345", editor.Text);
    }

    [AvaloniaFact]
    public async Task RectangularSelectionCopyDoesNotAttachRichPayload()
    {
        using var host = new EditorTestHost();
        var editor = host.Editor;
        editor.Text = "12345\n12345\n12345";
        Dispatcher.UIThread.RunJobs();

        editor.TextArea.Selection = new RectangleSelection(
            editor.TextArea,
            new TextViewPosition(1, 2),
            new TextViewPosition(3, 4));

        var copySuccess = await editor.CopyAsync();
        Assert.True(copySuccess);

        using var data = await host.Window.Clipboard!.TryGetDataAsync();
        Assert.NotNull(data);

        var text = await data.TryGetTextAsync();
        Assert.NotNull(text);
        Assert.Contains("23", text, StringComparison.Ordinal);

        // Rich payload must NOT be attached for rectangular selection
        var payload = await TemplateClipboard.TryGetPayloadAsync(data);
        Assert.Null(payload);
    }

    [AvaloniaFact]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Avalonia clipboard takes ownership of outgoing DataTransfer.")]
    public async Task RectangularSelectionPasteReplacesOnlyRectangularColumns()
    {
        using var host = new EditorTestHost();
        var editor = host.Editor;
        editor.Text = "12345\n12345\n12345";
        Dispatcher.UIThread.RunJobs();

        var transfer = TemplateClipboard.CreateTextDataTransfer("AB\nCD\nEF");
        await host.Window.Clipboard!.SetDataAsync(transfer);

        editor.TextArea.Selection = new RectangleSelection(
            editor.TextArea,
            new TextViewPosition(1, 2),
            new TextViewPosition(3, 4));

        await editor.PasteAsync();

        Assert.Equal("1AB45\n1CD45\n1EF45", editor.Text);
    }

    [AvaloniaFact]
    public async Task CutAsyncLeavesDocumentUntouchedWhenClipboardWriteFails()
    {
        using var host = new EditorTestHost();
        var editor = host.Editor;
        editor.Text = "Protected Content";
        Dispatcher.UIThread.RunJobs();
        editor.Select(0, editor.Document.TextLength);

        editor.ClipboardOverride = new FailingEditorClipboard();

        var cutResult = await editor.CutAsync();

        Assert.False(cutResult);
        Assert.Equal("Protected Content", editor.Text);
        Assert.False(editor.CanUndo);
    }

    [AvaloniaFact]
    public async Task CutAsyncAbortsWhenTabSwitchesDuringOperation()
    {
        using var host = new EditorTestHost();
        var editor = host.Editor;
        editor.Text = "Tab 1 Text";
        Dispatcher.UIThread.RunJobs();
        editor.Select(0, editor.Document.TextLength);

        var firstTabState = editor.EditorState!;
        var secondTabState = new TemplateEditorViewModel();
        var secondTabDoc = new TemplateDocument
        {
            Content = [DocumentPart.PlainText("Tab 2 Text")]
        };
        secondTabState.LoadDocument(secondTabDoc);

        var delayingClipboard = new DelayingEditorClipboard(
            new TopLevelEditorClipboard(() => host.Window.Clipboard));
        editor.ClipboardOverride = delayingClipboard;

        var cutTask = editor.CutAsync();

        // Switch to the second tab while cut is awaiting clipboard
        editor.EditorState = secondTabState;
        Dispatcher.UIThread.RunJobs();

        // Release clipboard
        delayingClipboard.Release();
        var cutResult = await cutTask;

        Assert.False(cutResult);
        Assert.Equal("Tab 2 Text", editor.Text);

        // Switch back to tab 1 and verify its text was untouched
        editor.EditorState = firstTabState;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Tab 1 Text", editor.Text);
    }

    [AvaloniaFact]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Avalonia clipboard takes ownership of outgoing DataTransfer.")]
    public async Task PasteAsyncAbortsWhenTabSwitchesDuringClipboardRead()
    {
        using var host = new EditorTestHost();
        var editor = host.Editor;
        editor.Text = "Tab 1 Text";
        Dispatcher.UIThread.RunJobs();
        editor.Select(0, 0);

        var firstTabState = editor.EditorState!;
        var secondTabState = new TemplateEditorViewModel();
        var secondTabDoc = new TemplateDocument
        {
            Content = [DocumentPart.PlainText("Tab 2 Text")]
        };
        secondTabState.LoadDocument(secondTabDoc);

        var transfer = TemplateClipboard.CreateDataTransfer(
            "Pasted Text",
            new TemplateClipboardPayload
            {
                FormatVersion = 1,
                Content = [DocumentPart.PlainText("Pasted Text")]
            });
        await host.Window.Clipboard!.SetDataAsync(transfer);

        var delayingClipboard = new DelayingEditorClipboard(
            new TopLevelEditorClipboard(() => host.Window.Clipboard));
        editor.ClipboardOverride = delayingClipboard;

        var pasteTask = editor.PasteAsync();

        // Switch to tab 2 while paste is awaiting clipboard
        editor.EditorState = secondTabState;
        Dispatcher.UIThread.RunJobs();

        delayingClipboard.Release();
        await pasteTask;

        // Tab 2 text is untouched
        Assert.Equal("Tab 2 Text", editor.Text);

        // Switch back to tab 1 and verify its text was untouched
        editor.EditorState = firstTabState;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Tab 1 Text", editor.Text);
    }

    [AvaloniaFact]
    public async Task CutAsyncAbortsWhenDocumentChangesDuringOperation()
    {
        using var host = new EditorTestHost();
        var editor = host.Editor;
        editor.Text = "Original Content";
        Dispatcher.UIThread.RunJobs();
        editor.Select(0, editor.Document.TextLength);

        var delayingClipboard = new DelayingEditorClipboard(
            new TopLevelEditorClipboard(() => host.Window.Clipboard));
        editor.ClipboardOverride = delayingClipboard;

        var cutTask = editor.CutAsync();

        // Document changed while cut was awaiting clipboard
        editor.Document.Insert(0, "Prefix ");

        delayingClipboard.Release();
        var result = await cutTask;

        Assert.False(result);
        Assert.Equal("Prefix Original Content", editor.Text);
    }

    [AvaloniaFact]
    public async Task CutAsyncAbortsWhenDocumentBecomesReadOnlyDuringOperation()
    {
        using var host = new EditorTestHost();
        var editor = host.Editor;
        editor.Text = "Original Content";
        Dispatcher.UIThread.RunJobs();
        editor.Select(0, editor.Document.TextLength);

        var delayingClipboard = new DelayingEditorClipboard(
            new TopLevelEditorClipboard(() => host.Window.Clipboard));
        editor.ClipboardOverride = delayingClipboard;

        var cutTask = editor.CutAsync();

        // Document becomes read only during await
        editor.IsReadOnly = true;

        delayingClipboard.Release();
        var result = await cutTask;

        Assert.False(result);
        Assert.Equal("Original Content", editor.Text);
    }

    [AvaloniaFact]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Avalonia clipboard takes ownership of outgoing DataTransfer.")]
    public async Task PasteAsyncNormalizesLineEndingsToDocumentConvention()
    {
        using var host = new EditorTestHost();
        var editor = host.Editor;
        // Document with LF line endings
        editor.Document.Text = "First line\nSecond line\n";
        editor.CaretOffset = editor.Document.TextLength;
        Dispatcher.UIThread.RunJobs();

        // Clipboard with CRLF line endings
        var transfer = TemplateClipboard.CreateTextDataTransfer("Alpha\r\nBeta");
        await host.Window.Clipboard!.SetDataAsync(transfer);

        await editor.PasteAsync();

        Assert.DoesNotContain("\r\n", editor.Text, StringComparison.Ordinal);
        Assert.Contains("Alpha\nBeta", editor.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Avalonia clipboard takes ownership of outgoing DataTransfer.")]
    public async Task PasteAsyncConvertsTabsToSpacesWhenEnabled()
    {
        using var host = new EditorTestHost();
        var editor = host.Editor;
        editor.Options.ConvertTabsToSpaces = true;
        editor.Options.IndentationSize = 4;
        editor.Text = string.Empty;
        Dispatcher.UIThread.RunJobs();

        var transfer = TemplateClipboard.CreateTextDataTransfer("\tHello\n\tWorld");
        await host.Window.Clipboard!.SetDataAsync(transfer);

        await editor.PasteAsync();

        Assert.DoesNotContain("\t", editor.Text, StringComparison.Ordinal);
        Assert.Equal("    Hello\n    World", editor.Text);
    }

    private sealed class EditorTestHost : IDisposable
    {
        public TemplateTextEditor Editor { get; } = CreateEditor();
        public Window Window { get; }

        public EditorTestHost(TemplateDocument? document = null)
        {
            if (document is not null)
            {
                Editor.LoadDocument(document);
            }

            Window = new Window { Content = Editor, Width = 800, Height = 600 };
            Window.Show();
            Dispatcher.UIThread.RunJobs();
        }

        public void Dispose() => Window.Close();
    }

    private sealed class FailingEditorClipboard : IEditorClipboard
    {
        public Task SetDataAsync(IAsyncDataTransfer dataTransfer) =>
            throw new InvalidOperationException("Simulated clipboard write failure.");

        public Task<IAsyncDataTransfer?> TryGetDataAsync() =>
            throw new InvalidOperationException("Simulated clipboard read failure.");
    }

    private sealed class DelayingEditorClipboard(IEditorClipboard inner) : IEditorClipboard
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release() => _gate.TrySetResult();

        public async Task SetDataAsync(IAsyncDataTransfer dataTransfer)
        {
            await _gate.Task;
            await inner.SetDataAsync(dataTransfer);
        }

        public async Task<IAsyncDataTransfer?> TryGetDataAsync()
        {
            await _gate.Task;
            return await inner.TryGetDataAsync();
        }
    }

    private static TemplateTextEditor CreateEditor() =>
        new()
        {
            Width = 600,
            Height = 400,
            EditorState = new TemplateEditorViewModel()
        };
}
