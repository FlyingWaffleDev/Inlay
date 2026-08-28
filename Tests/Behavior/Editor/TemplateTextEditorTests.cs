using Avalonia.Headless.XUnit;
using Avalonia.Controls;
using AvaloniaEdit.Rendering;
using Inlay.Controls;
using Inlay.Models;
using Inlay.ViewModels;
using Xunit;

namespace Inlay.Tests;

public sealed class TemplateTextEditorTests
{
    [AvaloniaFact]
    public void InsertingOverASelectionIsOneUndoableEdit()
    {
        const string original = "Before selected after";
        var editor = new TemplateTextEditor { Text = original };
        editor.EditorState = new TemplateEditorViewModel();
        editor.Document.UndoStack.ClearAll();
        editor.Select("Before ".Length, "selected".Length);

        editor.InsertTemplate();

        Assert.Equal("Before _____ after", editor.Text);
        Assert.Contains(
            editor.ExportDocument().Content,
            part => part.Type == DocumentPartKind.Template);

        editor.Undo();

        Assert.Equal(original, editor.Text);
        Assert.DoesNotContain(
            editor.ExportDocument().Content,
            part => part.Type == DocumentPartKind.Template);

        editor.Redo();

        Assert.Equal("Before _____ after", editor.Text);
        Assert.Contains(
            editor.ExportDocument().Content,
            part => part.Type == DocumentPartKind.Template);
    }

    [AvaloniaFact]
    public void TextAndCaretChangesUpdateDiagnostics()
    {
        var state = new TemplateEditorViewModel();
        var editor = new TemplateTextEditor { EditorState = state };

        editor.Text = "One, two\n3";
        editor.CaretOffset = editor.Document.TextLength;

        Assert.Equal("Characters 10   Words 3   Line 2   Column 2", state.Diagnostics);
    }

    [AvaloniaFact]
    public void LineLengthIndicatorsFillACharacterCell()
    {
        var editor = new TemplateTextEditor
        {
            ShowLineLengthIndicators = true,
            SoftLineLengthLimit = 90,
            HardLineLengthLimit = 110
        };

        editor.Measure(new Avalonia.Size(800, 600));
        editor.Arrange(new Avalonia.Rect(0, 0, 800, 600));
        var renderer = Assert.Single(
            editor.TextArea.TextView.BackgroundRenderers.OfType<LineLengthIndicatorRenderer>());
        var bounds = LineLengthIndicatorRenderer.GetColumnBounds(
            editor.TextArea.TextView,
            editor.SoftLineLengthLimit);

        Assert.True(renderer.IsEnabled);
        Assert.Equal(90, renderer.SoftLimit);
        Assert.Equal(110, renderer.HardLimit);
        Assert.Equal(editor.TextArea.TextView.WideSpaceWidth, bounds.Width);
        Assert.True(bounds.Width > 1);
    }

    [AvaloniaFact]
    public void ShownIndicatorsReserveTheEntireHardLimitColumn()
    {
        var editor = new TemplateTextEditor
        {
            ShowLineLengthIndicators = true,
            EnforceHardLineLengthLimit = true,
            SoftLineLengthLimit = 20,
            HardLineLengthLimit = 40,
            Text = "short wrapped chunks that never fill the available row"
        };
        var window = new Window { Width = 800, Height = 600, Content = editor };
        window.Show();
        try
        {
            window.UpdateLayout();
            var textView = editor.TextArea.TextView;
            var hardColumn = LineLengthIndicatorRenderer.GetColumnBounds(textView, 40);

            Assert.Equal(40 * textView.WideSpaceWidth, textView.MinWidth);
            Assert.True(textView.Bounds.Width >= hardColumn.Right);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AnOffscreenHardLimitDoesNotDisplaceTheLeftMargin()
    {
        var editor = new TemplateTextEditor
        {
            ShowLineNumbers = true,
            ShowLineLengthIndicators = true,
            SoftLineLengthLimit = 80,
            HardLineLengthLimit = 500,
            Text = "The left edge stays put"
        };
        var window = new Window { Width = 400, Height = 300, Content = editor };
        window.Show();
        try
        {
            window.UpdateLayout();
            var textView = editor.TextArea.TextView;
            var marginWidth = editor.TextArea.LeftMargins.Sum(
                margin => margin.DesiredSize.Width);
            var availableWidth = editor.TextArea.Bounds.Width - marginWidth;
            var textOrigin = Avalonia.VisualExtensions.TranslatePoint(
                textView,
                default,
                editor);

            Assert.True(textView.MinWidth <= availableWidth);
            Assert.NotNull(textOrigin);
            Assert.True(textOrigin.Value.X >= marginWidth);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void EnforcedHardLimitReflowsWithoutChangingTheDocument()
    {
        var editor = new TemplateTextEditor
        {
            ViewportWordWrap = false,
            SoftLineLengthLimit = 3,
            HardLineLengthLimit = 20,
            EnforceHardLineLengthLimit = true,
            Text = "1234567890"
        };
        var window = new Window { Width = 800, Height = 600, Content = editor };
        window.Show();
        try
        {
            editor.Document.UndoStack.ClearAll();

            Assert.Equal(1, editor.Document.LineCount);
            Assert.DoesNotContain('\n', editor.Text);
            Assert.Equal(1, GetVisualRowCount(editor, window));

            editor.HardLineLengthLimit = 4;

            Assert.True(GetVisualRowCount(editor, window) > 1);
            editor.HardLineLengthLimit = 20;
            Assert.Equal(1, GetVisualRowCount(editor, window));
            Assert.Equal("1234567890", editor.Text);
            Assert.False(editor.CanUndo);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TemplateChoiceChangesReflowAtTheHardLimit()
    {
        var editor = new TemplateTextEditor
        {
            ViewportWordWrap = false,
            SoftLineLengthLimit = 8,
            HardLineLengthLimit = 12,
            EnforceHardLineLengthLimit = true
        };
        editor.LoadDocument(new TemplateDocument
        {
            Content =
            [
                DocumentPart.PlainText("Value: "),
                DocumentPart.Template(["One", "A much longer choice"], 0)
            ]
        });
        var window = new Window { Width = 800, Height = 600, Content = editor };
        window.Show();
        try
        {
            Assert.Equal(1, GetVisualRowCount(editor, window));
            GetTemplateViewModel(editor, window).SelectedIndex = 1;

            Assert.Equal(1, editor.Document.LineCount);
            Assert.DoesNotContain('\n', editor.Text);
            Assert.True(GetVisualRowCount(editor, window) > 1);

            GetTemplateViewModel(editor, window).SelectedIndex = 0;

            Assert.Equal(1, GetVisualRowCount(editor, window));
            Assert.Equal("Value: One", editor.Text);
        }
        finally
        {
            window.Close();
        }
    }

    private static int GetVisualRowCount(TemplateTextEditor editor, Window window)
    {
        window.UpdateLayout();
        editor.TextArea.TextView.EnsureVisualLines();
        return Assert.Single(editor.TextArea.TextView.VisualLines).TextLines.Count;
    }

    private static TemplateFlyoutViewModel GetTemplateViewModel(
        TemplateTextEditor editor,
        Window window)
    {
        window.UpdateLayout();
        editor.TextArea.TextView.EnsureVisualLines();
        var template = editor.TextArea.TextView.VisualLines
            .SelectMany(line => line.Elements)
            .OfType<TemplateTextElement>()
            .Single();
        return Assert.IsType<TemplateFlyoutViewModel>(template.FlyoutContent.DataContext);
    }
}
