using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using Inlay.Models;
using Inlay.ViewModels;

namespace Inlay.Controls;

internal sealed class TemplateTextEditor : TextEditor, ITemplateEditorAdapter
{
    public static readonly StyledProperty<TemplateEditorViewModel?> EditorStateProperty =
        AvaloniaProperty.Register<TemplateTextEditor, TemplateEditorViewModel?>(nameof(EditorState));

    public static readonly StyledProperty<bool> HighlightCurrentLineProperty =
        AvaloniaProperty.Register<TemplateTextEditor, bool>(nameof(HighlightCurrentLine), true);

    public static readonly StyledProperty<bool> ShowLineLengthIndicatorsProperty =
        AvaloniaProperty.Register<TemplateTextEditor, bool>(nameof(ShowLineLengthIndicators));

    public static readonly StyledProperty<bool> EnforceHardLineLengthLimitProperty =
        AvaloniaProperty.Register<TemplateTextEditor, bool>(nameof(EnforceHardLineLengthLimit));

    public static readonly StyledProperty<bool> ViewportWordWrapProperty =
        AvaloniaProperty.Register<TemplateTextEditor, bool>(nameof(ViewportWordWrap), true);

    public static readonly StyledProperty<int> SoftLineLengthLimitProperty =
        AvaloniaProperty.Register<TemplateTextEditor, int>(nameof(SoftLineLengthLimit), 80);

    public static readonly StyledProperty<int> HardLineLengthLimitProperty =
        AvaloniaProperty.Register<TemplateTextEditor, int>(nameof(HardLineLengthLimit), 120);

    public static readonly StyledProperty<IBrush> SoftLineLengthIndicatorBrushProperty =
        AvaloniaProperty.Register<TemplateTextEditor, IBrush>(
            nameof(SoftLineLengthIndicatorBrush),
            new SolidColorBrush(Color.FromArgb(40, 245, 166, 35)));

    public static readonly StyledProperty<IBrush> HardLineLengthIndicatorBrushProperty =
        AvaloniaProperty.Register<TemplateTextEditor, IBrush>(
            nameof(HardLineLengthIndicatorBrush),
            new SolidColorBrush(Color.FromArgb(48, 229, 72, 77)));

    private readonly LineLengthIndicatorRenderer _lineLengthRenderer;
    private TemplateTextElementGenerator? _templateGenerator;
    private bool _isApplyingDocument;

    protected override Type StyleKeyOverride => typeof(TextEditor);

    public TemplateTextEditor()
    {
        Options.HighlightCurrentLine = true;
        Options.AllowScrollBelowDocument = false;
        Options.ShowColumnRulers = false;
        _lineLengthRenderer = new LineLengthIndicatorRenderer(TextArea.TextView);
        UpdateLineLengthIndicators();
        UpdateWordWrapping();
        Document.TextChanged += OnDocumentTextChanged;
        TextArea.TextView.PropertyChanged += OnTextViewPropertyChanged;
        TextArea.SizeChanged += (_, _) => UpdateWordWrapping();
        TextArea.Caret.PositionChanged += (_, _) => UpdateSnapshot();
        TextArea.SelectionChanged += (_, _) => UpdateSnapshot();
    }

    public TemplateEditorViewModel? EditorState
    {
        get => GetValue(EditorStateProperty);
        set => SetValue(EditorStateProperty, value);
    }

    public bool HighlightCurrentLine
    {
        get => GetValue(HighlightCurrentLineProperty);
        set => SetValue(HighlightCurrentLineProperty, value);
    }

    public bool ShowLineLengthIndicators
    {
        get => GetValue(ShowLineLengthIndicatorsProperty);
        set => SetValue(ShowLineLengthIndicatorsProperty, value);
    }

    public bool EnforceHardLineLengthLimit
    {
        get => GetValue(EnforceHardLineLengthLimitProperty);
        set => SetValue(EnforceHardLineLengthLimitProperty, value);
    }

    public bool ViewportWordWrap
    {
        get => GetValue(ViewportWordWrapProperty);
        set => SetValue(ViewportWordWrapProperty, value);
    }

    public int SoftLineLengthLimit
    {
        get => GetValue(SoftLineLengthLimitProperty);
        set => SetValue(SoftLineLengthLimitProperty, value);
    }

    public int HardLineLengthLimit
    {
        get => GetValue(HardLineLengthLimitProperty);
        set => SetValue(HardLineLengthLimitProperty, value);
    }

    public IBrush SoftLineLengthIndicatorBrush
    {
        get => GetValue(SoftLineLengthIndicatorBrushProperty);
        set => SetValue(SoftLineLengthIndicatorBrushProperty, value);
    }

    public IBrush HardLineLengthIndicatorBrush
    {
        get => GetValue(HardLineLengthIndicatorBrushProperty);
        set => SetValue(HardLineLengthIndicatorBrushProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == EditorStateProperty)
        {
            AttachState(change.OldValue as TemplateEditorViewModel, change.NewValue as TemplateEditorViewModel);
        }
        else if (change.Property == HighlightCurrentLineProperty)
        {
            Options.HighlightCurrentLine = change.GetNewValue<bool>();
        }
        else if (change.Property == ShowLineLengthIndicatorsProperty)
        {
            UpdateLineLengthIndicators();
            UpdateWordWrapping();
        }
        else if (change.Property == EnforceHardLineLengthLimitProperty ||
                 change.Property == ViewportWordWrapProperty)
        {
            UpdateWordWrapping();
        }
        else if (change.Property == SoftLineLengthLimitProperty)
        {
            var softLimit = Math.Max(1, change.GetNewValue<int>());
            if (softLimit != SoftLineLengthLimit)
            {
                SetCurrentValue(SoftLineLengthLimitProperty, softLimit);
                return;
            }

            if (HardLineLengthLimit < softLimit)
            {
                SetCurrentValue(HardLineLengthLimitProperty, softLimit);
            }

            UpdateLineLengthIndicators();
        }
        else if (change.Property == HardLineLengthLimitProperty)
        {
            var hardLimit = Math.Max(1, change.GetNewValue<int>());
            if (hardLimit < SoftLineLengthLimit)
            {
                if (change.GetOldValue<int>() == SoftLineLengthLimit)
                {
                    SetCurrentValue(SoftLineLengthLimitProperty, hardLimit);
                }
                else
                {
                    SetCurrentValue(HardLineLengthLimitProperty, SoftLineLengthLimit);
                    return;
                }
            }

            UpdateLineLengthIndicators();
            UpdateWordWrapping();
        }
        else if (change.Property == SoftLineLengthIndicatorBrushProperty ||
                 change.Property == HardLineLengthIndicatorBrushProperty)
        {
            UpdateLineLengthIndicators();
        }
    }

    private void UpdateLineLengthIndicators()
    {
        _lineLengthRenderer.Update(
            ShowLineLengthIndicators,
            SoftLineLengthLimit,
            HardLineLengthLimit,
            SoftLineLengthIndicatorBrush,
            HardLineLengthIndicatorBrush);
    }

    private void UpdateWordWrapping()
    {
        var hardLimitWidth = HardLineLengthLimit * TextArea.TextView.WideSpaceWidth;
        var availableWidth = TextArea.Bounds.Width -
            TextArea.LeftMargins.Sum(margin => margin.DesiredSize.Width);
        WordWrap = ViewportWordWrap || EnforceHardLineLengthLimit;
        TextArea.TextView.HorizontalAlignment = EnforceHardLineLengthLimit
            ? Avalonia.Layout.HorizontalAlignment.Left
            : Avalonia.Layout.HorizontalAlignment.Stretch;
        TextArea.TextView.MinWidth = ShowLineLengthIndicators
            ? Math.Min(hardLimitWidth, Math.Max(0, availableWidth))
            : 0;
        TextArea.TextView.MaxWidth = EnforceHardLineLengthLimit
            ? hardLimitWidth
            : double.PositiveInfinity;
        TextArea.TextView.Redraw();
    }

    private void OnTextViewPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == FontSizeProperty || e.Property == FontFamilyProperty)
        {
            UpdateWordWrapping();
        }
    }

    public TemplateDocument ExportDocument() =>
        _templateGenerator?.ExportDocument() ?? new TemplateDocument();

    public void LoadDocument(TemplateDocument document)
    {
        EnsureGenerator();
        _isApplyingDocument = true;
        try
        {
            _templateGenerator!.LoadDocument(document);
            Document.UndoStack.ClearAll();
            CaretOffset = 0;
        }
        finally
        {
            _isApplyingDocument = false;
        }

        UpdateSnapshot();
    }

    public void InsertTemplate()
    {
        EnsureGenerator();
        var offset = SelectionLength > 0
            ? Math.Min(SelectionStart, SelectionStart + SelectionLength)
            : CaretOffset;
        Document.UndoStack.StartUndoGroup();
        try
        {
            if (SelectionLength > 0)
            {
                Document.Remove(offset, SelectionLength);
            }

            _templateGenerator!.AddTemplate(offset, []);
        }
        finally
        {
            Document.UndoStack.EndUndoGroup();
        }

        CaretOffset = offset + TemplateTextElement.PlaceholderText.Length;
        Focus();
        UpdateSnapshot();
    }

    public new void Undo()
    {
        if (CanUndo)
        {
            base.Undo();
            UpdateSnapshot();
        }
    }

    public new void Redo()
    {
        if (CanRedo)
        {
            base.Redo();
            UpdateSnapshot();
        }
    }

    public void Find() => ToggleSearchPanel(isReplaceMode: false);

    public void Replace() => ToggleSearchPanel(isReplaceMode: true);

    private void AttachState(TemplateEditorViewModel? oldState, TemplateEditorViewModel? newState)
    {
        oldState?.Detach(this);
        if (newState is null)
        {
            return;
        }

        EnsureGenerator();
        newState.Attach(this);
        UpdateSnapshot();
    }

    private void EnsureGenerator()
    {
        if (_templateGenerator is not null)
        {
            return;
        }

        _templateGenerator = new TemplateTextElementGenerator(this);
        TextArea.TextView.ElementGenerators.Add(_templateGenerator);
        _templateGenerator.TemplatesChanged += OnTemplatesChanged;
    }

    private void ToggleSearchPanel(bool isReplaceMode)
    {
        var panel = SearchPanel;
        if (panel is null)
        {
            return;
        }

        if (panel.IsOpened && panel.IsReplaceMode == isReplaceMode)
        {
            panel.Close();
            return;
        }

        panel.IsReplaceMode = isReplaceMode;
        panel.Open();
        if (!TextArea.Selection.IsEmpty && !TextArea.Selection.IsMultiline)
        {
            panel.SearchPattern = TextArea.Selection.GetText();
        }

        Dispatcher.UIThread.Post(panel.Reactivate, DispatcherPriority.Input);
    }

    private void OnDocumentTextChanged(object? sender, EventArgs e)
    {
        if (!_isApplyingDocument)
        {
            EditorState?.ReportContentChanged();
        }

        UpdateSnapshot();
    }

    private void OnTemplatesChanged()
    {
        if (!_isApplyingDocument)
        {
            EditorState?.ReportContentChanged();
        }

        UpdateSnapshot();
    }

    private void UpdateSnapshot()
    {
        var text = Document.Text;
        EditorState?.UpdateSnapshot(new EditorSnapshot(
            text.Length,
            CountWords(text),
            TextArea.Caret.Line,
            TextArea.Caret.Column,
            CanUndo,
            CanRedo,
            CanCut,
            CanCopy,
            CanPaste,
            CanDelete,
            CanSelectAll));
    }

    private static int CountWords(string text)
    {
        var count = 0;
        var insideWord = false;
        foreach (var character in text)
        {
            if (char.IsLetterOrDigit(character))
            {
                if (!insideWord)
                {
                    count++;
                    insideWord = true;
                }
            }
            else
            {
                insideWord = false;
            }
        }

        return count;
    }
}

internal sealed class LineLengthIndicatorRenderer : IBackgroundRenderer
{
    private readonly TextView _textView;
    private bool _isEnabled;
    private int _softLimit;
    private int _hardLimit;
    private IBrush? _softBrush;
    private IBrush? _hardBrush;

    internal bool IsEnabled => _isEnabled;
    internal int SoftLimit => _softLimit;
    internal int HardLimit => _hardLimit;

    public LineLengthIndicatorRenderer(TextView textView)
    {
        _textView = textView;
        _textView.BackgroundRenderers.Add(this);
    }

    public KnownLayer Layer => KnownLayer.Background;

    public void Update(
        bool isEnabled,
        int softLimit,
        int hardLimit,
        IBrush softBrush,
        IBrush hardBrush)
    {
        _isEnabled = isEnabled;
        _softLimit = softLimit;
        _hardLimit = hardLimit;
        _softBrush = softBrush;
        _hardBrush = hardBrush;
        _textView.InvalidateLayer(Layer);
    }

    public void Draw(TextView view, DrawingContext drawingContext)
    {
        if (!_isEnabled)
        {
            return;
        }

        if (_softLimit != _hardLimit)
        {
            drawingContext.DrawRectangle(_softBrush, null, GetColumnBounds(view, _softLimit));
        }

        drawingContext.DrawRectangle(_hardBrush, null, GetColumnBounds(view, _hardLimit));
    }

    internal static Rect GetColumnBounds(TextView view, int column)
    {
        var characterWidth = view.WideSpaceWidth;
        var x = characterWidth * (column - 1) - view.ScrollOffset.X;
        var height = Math.Max(view.DocumentHeight, view.Bounds.Height);
        return new Rect(x, 0, characterWidth, height);
    }
}
