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
    public void AddingAChoiceIsUndoableWithoutChangingSelection()
    {
        var (editor, generator, viewModel) = CreateTemplate(["One"], 0);

        viewModel.NewChoice = "Two";
        viewModel.AddChoice();

        AssertTemplateState(editor, generator, viewModel, ["One", "Two"], 0);

        editor.Undo();
        AssertTemplateState(editor, generator, viewModel, ["One"], 0);

        editor.Redo();
        AssertTemplateState(editor, generator, viewModel, ["One", "Two"], 0);
    }

    [AvaloniaFact]
    public void AddingAndSelectingAreSeparateUndoSteps()
    {
        var (editor, generator, viewModel) = CreateTemplate([], -1);

        viewModel.NewChoice = "One";
        viewModel.AddChoice();
        viewModel.SelectedIndex = 0;

        editor.Undo();
        AssertTemplateState(editor, generator, viewModel, ["One"], -1);

        editor.Undo();
        AssertTemplateState(editor, generator, viewModel, [], -1);

        editor.Redo();
        editor.Redo();
        AssertTemplateState(editor, generator, viewModel, ["One"], 0);
    }

    [AvaloniaFact]
    public void RemovingTheSelectedChoiceIsUndoable()
    {
        var (editor, generator, viewModel) = CreateTemplate(["One", "Two"], 0);

        viewModel.SelectedIndex = 1;
        viewModel.RemoveChoice("Two");

        AssertTemplateState(editor, generator, viewModel, ["One"], -1);

        editor.Undo();
        AssertTemplateState(editor, generator, viewModel, ["One", "Two"], 1);

        editor.Redo();
        AssertTemplateState(editor, generator, viewModel, ["One"], -1);
    }

    [AvaloniaFact]
    public void RemovingAnEarlierChoicePreservesSelectionThroughUndoAndRedo()
    {
        var (editor, generator, viewModel) = CreateTemplate(
            ["One", "Two", "Three"],
            2);

        viewModel.RemoveChoice("One");
        AssertTemplateState(editor, generator, viewModel, ["Two", "Three"], 1);

        editor.Undo();
        AssertTemplateState(editor, generator, viewModel, ["One", "Two", "Three"], 2);

        editor.Redo();
        AssertTemplateState(editor, generator, viewModel, ["Two", "Three"], 1);
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
            Assert.True(editor.TextArea.Focus());
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

    [AvaloniaFact]
    public void ArrowKeysStopBetweenAdjacentTemplates()
    {
        var editor = new TextEditor
        {
            Width = 600,
            Height = 400
        };
        var generator = AttachGenerator(editor);
        generator.AddTemplate(0, ["One"], 0);
        generator.AddTemplate("One".Length, ["Two"], 0);
        var window = new Window { Content = editor };
        window.Show();
        window.UpdateLayout();

        try
        {
            Assert.True(editor.TextArea.Focus());
            editor.CaretOffset = 0;

            Press(window, Key.Right);
            Assert.Equal("One".Length, editor.CaretOffset);

            Press(window, Key.Right);
            Assert.Equal("OneTwo".Length, editor.CaretOffset);

            Press(window, Key.Left);
            Assert.Equal("One".Length, editor.CaretOffset);

            window.KeyTextInput("X");

            Assert.Equal("OneXTwo", editor.Text);
            Assert.Equal(
                [DocumentPartKind.Template, DocumentPartKind.Text, DocumentPartKind.Template],
                generator.ExportDocument().Content.Select(part => part.Type));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DeletingASelectionAcrossATemplateDissolvesItAndUndoRestoresIt()
    {
        var editor = new TextEditor
        {
            Text = "BeforeAfter",
            Width = 600,
            Height = 400
        };
        var generator = AttachGenerator(editor);
        var templateOffset = "Before".Length;
        generator.AddTemplate(templateOffset, ["Choice"], 0);
        editor.Document.UndoStack.ClearAll();
        var window = new Window { Content = editor };
        window.Show();
        window.UpdateLayout();

        try
        {
            editor.Focus();
            editor.Select(templateOffset - 1, "eChoiceA".Length);
            Assert.Equal("eChoiceA", editor.SelectedText);
            editor.Delete();

            Assert.Equal("Beforfter", editor.Text);
            Assert.DoesNotContain(
                generator.ExportDocument().Content,
                part => part.Type == DocumentPartKind.Template);

            editor.Undo();

            Assert.Equal("BeforeChoiceAfter", editor.Text);
            Assert.Contains(
                generator.ExportDocument().Content,
                part => part.Type == DocumentPartKind.Template);

            editor.Redo();
            Assert.Equal("Beforfter", editor.Text);
            Assert.DoesNotContain(
                generator.ExportDocument().Content,
                part => part.Type == DocumentPartKind.Template);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ReplacingPartOfATemplateTurnsItsRemainingTextIntoPlainText()
    {
        var editor = new TextEditor { Text = "BeforeAfter" };
        var generator = AttachGenerator(editor);
        var templateOffset = "Before".Length;
        generator.AddTemplate(templateOffset, ["Choice"], 0);
        editor.Document.UndoStack.ClearAll();

        editor.Document.Replace(templateOffset + 1, 2, "X");

        Assert.Equal("BeforeCXiceAfter", editor.Text);
        Assert.DoesNotContain(
            generator.ExportDocument().Content,
            part => part.Type == DocumentPartKind.Template);

        editor.Undo();
        Assert.Equal("BeforeChoiceAfter", editor.Text);
        Assert.Contains(
            generator.ExportDocument().Content,
            part => part.Type == DocumentPartKind.Template);
    }

    [AvaloniaFact]
    public void RenderingTemplatesUsesOneLazyFlyoutPerEditor()
    {
        var editor = new TextEditor
        {
            Text = "BeforeAfter",
            Width = 600,
            Height = 400
        };
        var generator = AttachGenerator(editor);
        generator.AddTemplate("Before".Length, ["Choice"], 0);
        var firstElement = ConstructTemplate(editor);

        Assert.False(generator.HasCreatedFlyout);

        var flyoutContent = firstElement.FlyoutContent;
        editor.Document.Insert(editor.Document.TextLength, "!");
        editor.TextArea.TextView.Redraw();
        var nextElement = ConstructTemplate(editor);

        Assert.True(generator.HasCreatedFlyout);
        Assert.Same(flyoutContent, nextElement.FlyoutContent);
    }

    private static TemplateTextElementGenerator AttachGenerator(TextEditor editor)
    {
        var generator = new TemplateTextElementGenerator(editor);
        editor.TextArea.TextView.ElementGenerators.Add(generator);
        return generator;
    }

    private static (
        TextEditor Editor,
        TemplateTextElementGenerator Generator,
        TemplateFlyoutViewModel ViewModel) CreateTemplate(
            string[] options,
            int selectedIndex)
    {
        var editor = new TextEditor { Width = 600, Height = 400 };
        var generator = AttachGenerator(editor);
        generator.AddTemplate(0, options, selectedIndex);
        var template = ConstructTemplate(editor);
        var viewModel = Assert.IsType<TemplateFlyoutViewModel>(
            template.FlyoutContent.DataContext);
        return (editor, generator, viewModel);
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

    private static void AssertTemplateState(
        TextEditor editor,
        TemplateTextElementGenerator generator,
        TemplateFlyoutViewModel viewModel,
        string[] expectedOptions,
        int expectedIndex)
    {
        var template = Assert.Single(generator.ExportDocument().Content);
        var expectedChoice = expectedIndex >= 0 ? expectedOptions[expectedIndex] : null;

        Assert.Equal(expectedChoice ?? TemplateTextElement.PlaceholderText, editor.Text);
        Assert.Equal(expectedOptions, template.Options);
        Assert.Equal(expectedIndex, template.SelectedIndex);
        Assert.Equal(expectedChoice, viewModel.SelectedChoice);
        Assert.Equal(expectedIndex, viewModel.SelectedIndex);
    }
}
