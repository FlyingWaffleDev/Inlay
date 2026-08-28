using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using Inlay.Models;
using Inlay.ViewModels;
using Xunit;

namespace Inlay.Tests;

public sealed class TemplateTextElementGeneratorTests
{
    [AvaloniaFact]
    public void LoadAndExportPreserveTextAndTemplateParts()
    {
        var editor = new TextEditor();
        var generator = AttachGenerator(editor);
        var document = new TemplateDocument
        {
            Content =
            [
                DocumentPart.PlainText("Dear "),
                DocumentPart.Template(["Ada", "Grace"], 1),
                DocumentPart.PlainText(", choose "),
                DocumentPart.Template(["tea", "coffee"], -1),
                DocumentPart.PlainText(".")
            ]
        };

        generator.LoadDocument(document);
        var exported = generator.ExportDocument();

        Assert.Equal("Dear Grace, choose _____.", editor.Text);
        Assert.Equal(5, exported.Content.Count);
        Assert.Equal("Dear ", exported.Content[0].Text);
        Assert.Equal(["Ada", "Grace"], exported.Content[1].Options);
        Assert.Equal(1, exported.Content[1].SelectedIndex);
        Assert.Equal(", choose ", exported.Content[2].Text);
        Assert.Equal(["tea", "coffee"], exported.Content[3].Options);
        Assert.Equal(-1, exported.Content[3].SelectedIndex);
        Assert.Equal(".", exported.Content[4].Text);
    }

    [AvaloniaFact]
    public void SelectingAnOptionKeepsTextAndMetadataInSyncThroughUndoAndRedo()
    {
        var editor = new TextEditor
        {
            Text = "Value: ",
            Width = 600,
            Height = 400
        };
        var generator = AttachGenerator(editor);
        generator.AddTemplate(editor.Document.TextLength, ["One", "Two"], 0);
        var template = ConstructTemplate(editor);
        var viewModel = Assert.IsType<TemplateFlyoutViewModel>(template.FlyoutContent.DataContext);

        viewModel.SelectedIndex = 1;

        Assert.Equal("Value: Two", editor.Text);
        AssertSelection(generator.ExportDocument(), "Two", 1);

        editor.Undo();

        Assert.Equal("Value: One", editor.Text);
        AssertSelection(generator.ExportDocument(), "One", 0);

        editor.Redo();

        Assert.Equal("Value: Two", editor.Text);
        AssertSelection(generator.ExportDocument(), "Two", 1);
    }

    [AvaloniaFact]
    public void EditingAroundATemplatePreservesItsPositionOnExport()
    {
        var editor = new TextEditor { Text = "BeforeAfter" };
        var generator = AttachGenerator(editor);
        generator.AddTemplate("Before".Length, ["Choice"], 0);

        editor.Document.Insert(0, "(");
        editor.Document.Insert(editor.Document.TextLength, ")");

        var content = generator.ExportDocument().Content;
        Assert.Equal("(Before", content[0].Text);
        Assert.Equal(["Choice"], content[1].Options);
        Assert.Equal("After)", content[2].Text);
    }

    [AvaloniaFact]
    public void RemovingTheSelectedChoiceUsesThePlaceholder()
    {
        var editor = new TextEditor { Width = 600, Height = 400 };
        var generator = AttachGenerator(editor);
        generator.AddTemplate(0, ["One", "Two"], 1);
        var template = ConstructTemplate(editor);
        var viewModel = Assert.IsType<TemplateFlyoutViewModel>(template.FlyoutContent.DataContext);

        viewModel.RemoveChoice("Two");

        var part = Assert.Single(generator.ExportDocument().Content);
        Assert.Equal(TemplateTextElement.PlaceholderText, editor.Text);
        Assert.Equal(["One"], part.Options);
        Assert.Equal(-1, part.SelectedIndex);
    }

    [AvaloniaFact]
    public void DeleteAndBackspaceDoNotSplitATemplate()
    {
        var editor = new TextEditor
        {
            Text = "BeforeAfter",
            Width = 600,
            Height = 400
        };
        var generator = AttachGenerator(editor);
        var offset = "Before".Length;
        generator.AddTemplate(offset, ["Choice"], 0);
        var window = new Window { Content = editor };
        window.Show();
        window.UpdateLayout();

        try
        {
            editor.CaretOffset = offset;
            Press(window, Key.Delete);
            editor.CaretOffset = offset + "Choice".Length;
            Press(window, Key.Back);

            Assert.Equal("BeforeChoiceAfter", editor.Text);
            Assert.Equal(["Choice"], generator.ExportDocument().Content[1].Options);
        }
        finally
        {
            window.Close();
        }
    }

    private static TemplateTextElementGenerator AttachGenerator(TextEditor editor)
    {
        var generator = new TemplateTextElementGenerator(editor);
        editor.TextArea.TextView.ElementGenerators.Add(generator);
        return generator;
    }

    private static TemplateTextElement ConstructTemplate(TextEditor editor)
    {
        editor.Measure(new Size(600, 400));
        editor.Arrange(new Rect(0, 0, 600, 400));
        var line = editor.TextArea.TextView.GetOrConstructVisualLine(editor.Document.Lines[0]);
        return line.Elements.OfType<TemplateTextElement>().Single();
    }

    private static void Press(Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
    }

    private static void AssertSelection(
        TemplateDocument document,
        string expectedText,
        int expectedIndex)
    {
        var template = Assert.Single(
            document.Content,
            part => part.Type == DocumentPartKind.Template);
        Assert.Equal(expectedText, template.Options![expectedIndex]);
        Assert.Equal(expectedIndex, template.SelectedIndex);
    }
}
