using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.VisualTree;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Inlay.ViewModels;

namespace Inlay;

internal sealed class TemplateTextElement : VisualLineText
{
    private static readonly Size ClickAnchorSize = new(1, 1);
    private const string ForegroundBrushResourceKey = "InlayTemplateForegroundBrush";
    private readonly TextView _textView;
    private readonly Flyout _flyout;
    private readonly TemplateFlyoutViewModel _viewModel;
    private Rect _flyoutAnchorRectangle;

    public const string PlaceholderText = "_____";

    public TextAnchor Anchor { get; }
    public ObservableCollection<string> Options => _viewModel.Options;
    public int SelectedIndex => _viewModel.SelectedIndex;

    internal Control FlyoutContent => (Control)((TemplateFlyoutView)_flyout.Content!).Content!;
    internal Point FlyoutPosition => _flyoutAnchorRectangle.TopLeft;
    internal Rect FlyoutAnchorRectangle => _flyoutAnchorRectangle;
    internal PlacementMode FlyoutPlacement => _flyout.Placement;

    public event Action<TemplateTextElement>? Removed;
    public event Action<TemplateTextElement, string>? OptionSelected;

    public TemplateTextElement(
        VisualLine parentVisualLine,
        int length,
        TextAnchor anchor,
        ObservableCollection<string> options,
        int selectedIndex,
        TextView textView)
        : base(parentVisualLine, length)
    {
        Anchor = anchor;
        _textView = textView;
        _viewModel = new TemplateFlyoutViewModel(
            options,
            selectedIndex,
            option => OptionSelected?.Invoke(this, option),
            Remove);
        _flyout = CreateFlyout();
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
        if (!e.Handled && !_flyout.IsOpen && e.Source is Control control)
        {
            ShowFlyoutAt(control, e.GetPosition(control));
            (_flyout.Content as Control)?.FindDescendantOfType<TextBox>()?.Focus();
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
        CaptureFlyoutPosition(placementTarget, position);
        _flyout.ShowAt(placementTarget);
    }

    internal void CaptureFlyoutPosition(Control placementTarget, Point position)
    {
        var topLevel = TopLevel.GetTopLevel(placementTarget);
        var topLevelPosition = topLevel is null
            ? position
            : placementTarget.TranslatePoint(position, topLevel) ?? position;
        _flyoutAnchorRectangle = new Rect(topLevelPosition, ClickAnchorSize);
        _flyout.Placement = PlacementMode.Custom;
        _flyout.CustomPopupPlacementCallback = placement =>
        {
            placement.AnchorRectangle = _flyoutAnchorRectangle;
            placement.Anchor = PopupAnchor.TopLeft;
            placement.Gravity = PopupGravity.BottomRight;
            placement.ConstraintAdjustment = PopupPositionerConstraintAdjustment.None;
        };
    }

    private Flyout CreateFlyout()
    {
        var view = new TemplateFlyoutView { DataContext = _viewModel };
        return new Flyout { Content = view };
    }

    internal void Remove()
    {
        _flyout.Hide();
        Removed?.Invoke(this);
    }
}
