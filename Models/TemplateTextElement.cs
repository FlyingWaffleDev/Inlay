using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.VisualTree;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Inlay;

internal sealed class TemplateTextElement(
    VisualLine parentVisualLine,
    int length,
    TextAnchor anchor,
    TextView textView,
    TemplateTextElementGenerator owner) : VisualLineText(parentVisualLine, length)
{
    private const string ForegroundBrushResourceKey = "InlayTemplateForegroundBrush";
    private readonly TextView _textView = textView;
    private readonly TemplateTextElementGenerator _owner = owner;

    public const string PlaceholderText = "_____";

    public TextAnchor Anchor { get; } = anchor;
    public ObservableCollection<string> Options => _owner.GetOptions(this);
    public int SelectedIndex => _owner.GetSelectedIndex(this);

    internal Control FlyoutContent => _owner.GetFlyoutContent(this);
    internal bool IsFlyoutOpen => _owner.IsFlyoutOpen(this);

    public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context)
    {
        if (_textView.TryFindResource(
                ForegroundBrushResourceKey,
                _textView.ActualThemeVariant,
                out var resource) &&
            resource is IBrush foregroundBrush)
        {
            TextRunProperties.SetForegroundBrush(foregroundBrush);
        }

        TextRunProperties.SetTextDecorations(TextDecorations.Underline);
        return base.CreateTextRun(startVisualColumn, context);
    }

    protected override void OnQueryCursor(PointerEventArgs e)
    {
        if (e.Source is InputElement inputElement)
        {
            inputElement.Cursor = new Cursor(StandardCursorType.Hand);
        }

        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (!e.Handled && !IsFlyoutOpen && e.Source is Control control)
        {
            ShowFlyoutAt(control, e.GetPosition(control));
            FlyoutContent.FindDescendantOfType<TextBox>()?.Focus();
            e.Handled = true;
        }
    }

    public override int GetNextCaretPosition(
        int visualColumn,
        AvaloniaEdit.Document.LogicalDirection direction,
        CaretPositioningMode mode)
    {
        // Keep the template atomic while allowing caret stops at both edges.
        var start = VisualColumn;
        var end = VisualColumn + VisualLength;
        if (direction == AvaloniaEdit.Document.LogicalDirection.Backward)
        {
            if (visualColumn > end &&
                mode is not CaretPositioningMode.WordStart and
                    not CaretPositioningMode.WordStartOrSymbol)
            {
                return end;
            }

            return visualColumn > start ? start : -1;
        }

        if (visualColumn < start)
        {
            return start;
        }

        return visualColumn < end &&
               mode is not CaretPositioningMode.WordStart and
                   not CaretPositioningMode.WordStartOrSymbol
            ? end
            : -1;
    }

    public override bool CanSplit => false;

    internal void ShowFlyoutAt(Control placementTarget, Point position)
    {
        _owner.ShowFlyoutAt(this, placementTarget, position);
    }

    internal void CloseFlyout() => _owner.CloseFlyout(this);

    internal void Remove()
    {
        CloseFlyout();
        _owner.RemoveTemplate(this);
    }
}
