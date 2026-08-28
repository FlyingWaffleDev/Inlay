using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using Xunit;

namespace Inlay.Tests;

public sealed class TemplateRemovalTests
{
    [AvaloniaFact]
    public void UndoAndRedoCloseTheFlyoutWhenTheyRemoveItsTemplate()
    {
        var editor = new TextEditor { Width = 600, Height = 400 };
        var generator = new TemplateTextElementGenerator(editor);
        editor.TextArea.TextView.ElementGenerators.Add(generator);
        generator.AddTemplate(0, ["choice"], 0);
        var window = new Window { Content = editor };
        window.Show();
        window.UpdateLayout();

        try
        {
            var addedTemplate = OpenTemplateFlyout(editor);

            editor.Undo();
            Dispatcher.UIThread.RunJobs();

            Assert.False(addedTemplate.IsFlyoutOpen);

            editor.Redo();
            var templateToRemove = ConstructTemplate(editor);
            templateToRemove.Remove();
            editor.Undo();
            var restoredTemplate = OpenTemplateFlyout(editor);

            editor.Redo();
            Dispatcher.UIThread.RunJobs();

            Assert.False(restoredTemplate.IsFlyoutOpen);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void RemovingTemplateAtEndOfLineKeepsEditorValid()
    {
        VerifyRemoval("First line\nLast line", "First line".Length);
        VerifyRemoval("First line\nLast line", "First line\nLast line".Length);
    }

    private static void VerifyRemoval(string originalText, int templateOffset)
    {
        var editor = new TextEditor
        {
            Text = originalText,
            Width = 600,
            Height = 400
        };
        var generator = new TemplateTextElementGenerator(editor);
        editor.TextArea.TextView.ElementGenerators.Add(generator);
        generator.AddTemplate(templateOffset, ["choice"], 0);
        editor.CaretOffset = templateOffset + "choice".Length;

        editor.Measure(new Size(600, 400));
        editor.Arrange(new Rect(0, 0, 600, 400));
        var documentLine = editor.Document.GetLineByOffset(templateOffset);
        var visualLine = editor.TextArea.TextView.GetOrConstructVisualLine(documentLine);
        var template = visualLine.Elements.OfType<TemplateTextElement>().Single();

        template.Remove();

        Assert.Equal(originalText, editor.Text);
        Assert.InRange(editor.CaretOffset, 0, editor.Document.TextLength);
        Assert.DoesNotContain(
            generator.ExportDocument().Content,
            part => part.Type == Models.DocumentPartKind.Template);
        editor.TextArea.TextView.EnsureVisualLines();

        editor.Undo();
        Assert.Equal(originalText.Insert(templateOffset, "choice"), editor.Text);
        Assert.Contains(
            generator.ExportDocument().Content,
            part => part.Type == Models.DocumentPartKind.Template);

        editor.Redo();
        Assert.Equal(originalText, editor.Text);
        Assert.DoesNotContain(
            generator.ExportDocument().Content,
            part => part.Type == Models.DocumentPartKind.Template);
    }

    private static TemplateTextElement ConstructTemplate(TextEditor editor)
    {
        editor.TextArea.TextView.EnsureVisualLines();
        var line = editor.TextArea.TextView.GetOrConstructVisualLine(editor.Document.Lines[0]);
        return line.Elements.OfType<TemplateTextElement>().Single();
    }

    private static TemplateTextElement OpenTemplateFlyout(TextEditor editor)
    {
        var template = ConstructTemplate(editor);
        template.ShowFlyoutAt(editor, new Point(0, 0));
        Dispatcher.UIThread.RunJobs();
        Assert.True(template.IsFlyoutOpen);
        return template;
    }
}
