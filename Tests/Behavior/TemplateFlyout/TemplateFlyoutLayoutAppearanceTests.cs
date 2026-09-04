using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using Xunit;

namespace Inlay.Tests;

public sealed class TemplateFlyoutLayoutAppearanceTests
{
    [AvaloniaFact]
    public void LongOptionDoesNotChangeFlyoutWidth()
    {
        var (template, content) = CreateTemplate(["Short"], 0);
        content.Measure(Size.Infinity);
        var initialWidth = content.DesiredSize.Width;

        template.Options.Add(new string('W', 100));
        content.Measure(Size.Infinity);

        Assert.Equal(initialWidth, content.DesiredSize.Width);
    }

    [AvaloniaFact]
    public void OptionsListTracksWhetherChoicesExist()
    {
        var (template, content) = CreateTemplate([], -1);
        var panel = Assert.IsType<StackPanel>(content);
        var listBox = panel.Children.OfType<ListBox>().Single();

        Assert.False(listBox.IsVisible);

        template.Options.Add("Choice");

        Assert.True(listBox.IsVisible);
    }

    [AvaloniaFact]
    public void AddingAnOptionDoesNotChangeCapturedFlyoutPosition()
    {
        var editor = new TextEditor
        {
            Text = "Template: ",
            Width = 600,
            Height = 400
        };
        var canvas = new Canvas();
        Canvas.SetLeft(editor, 80);
        Canvas.SetTop(editor, 60);
        canvas.Children.Add(editor);
        var window = new Window { Content = canvas, Width = 800, Height = 600 };
        var generator = new TemplateTextElementGenerator(editor);
        editor.TextArea.TextView.ElementGenerators.Add(generator);
        generator.AddTemplate(editor.Document.TextLength, [], -1);

        window.Show();
        window.UpdateLayout();
        try
        {
            var visualLine = editor.TextArea.TextView.GetOrConstructVisualLine(editor.Document.Lines[0]);
            var template = visualLine.Elements.OfType<TemplateTextElement>().Single();
            var clickPosition = new Point(125, 75);
            var expectedPosition = editor.TranslatePoint(clickPosition, window)!.Value;

            generator.CaptureFlyoutPosition(template, editor, clickPosition);
            var initialAnchor = generator.FlyoutAnchorRectangle;
            template.Options.Add("Choice");

            Assert.Equal(PlacementMode.Custom, generator.FlyoutPlacement);
            Assert.NotEqual(clickPosition, expectedPosition);
            Assert.Equal(new Rect(expectedPosition, new Size(1, 1)), initialAnchor);
            Assert.Equal(expectedPosition, initialAnchor.TopLeft);
            Assert.Equal(initialAnchor, generator.FlyoutAnchorRectangle);
        }
        finally
        {
            window.Close();
        }
    }

    private static (TemplateTextElement Template, Control Content) CreateTemplate(
        IEnumerable<string> options,
        int selectedIndex)
    {
        var editor = new TextEditor { Text = "Template: ", Width = 600, Height = 400 };
        var generator = new TemplateTextElementGenerator(editor);
        editor.TextArea.TextView.ElementGenerators.Add(generator);
        generator.AddTemplate(editor.Document.TextLength, options, selectedIndex);

        editor.Measure(new Size(600, 400));
        editor.Arrange(new Rect(0, 0, 600, 400));
        var visualLine = editor.TextArea.TextView.GetOrConstructVisualLine(editor.Document.Lines[0]);
        var template = visualLine.Elements.OfType<TemplateTextElement>().Single();
        return (template, template.FlyoutContent);
    }
}
