using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.VisualTree;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Inlay;

internal sealed class TemplateTextElement : VisualLineText
{
    private const string ForegroundBrushResourceKey = "InlayTemplateForegroundBrush";
    private readonly TextView _textView;
    private readonly TemplateTextElementGenerator _owner;

    public const string PlaceholderText = "_____";

    public TextAnchor Anchor { get; }
    public ObservableCollection<string> Options => _owner.GetOptions(this);
    public int SelectedIndex => _owner.GetSelectedIndex(this);

    internal Control FlyoutContent => _owner.GetFlyoutContent(this);
    internal Point FlyoutPosition => _owner.GetFlyoutPosition(this);
    internal Rect FlyoutAnchorRectangle => _owner.GetFlyoutAnchorRectangle(this);
    internal PlacementMode FlyoutPlacement => _owner.GetFlyoutPlacement(this);
    internal bool IsFlyoutOpen => _owner.IsFlyoutOpen(this);

    public TemplateTextElement(
        VisualLine parentVisualLine,
        int length,
        TextAnchor anchor,
        TextView textView,
        TemplateTextElementGenerator owner)
        : base(parentVisualLine, length)
    {
        Anchor = anchor;
        _textView = textView;
        _owner = owner;
    }

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
        CaretPositioningMode mode) => -1;

    public override bool CanSplit => false;

    internal void ShowFlyoutAt(Control placementTarget, Point position)
    {
        _owner.ShowFlyoutAt(this, placementTarget, position);
    }

    internal void CaptureFlyoutPosition(Control placementTarget, Point position) =>
        _owner.CaptureFlyoutPosition(this, placementTarget, position);

    internal void CloseFlyout() => _owner.CloseFlyout(this);

    internal void Remove()
    {
        CloseFlyout();
        _owner.RemoveTemplate(this);
    }
}
