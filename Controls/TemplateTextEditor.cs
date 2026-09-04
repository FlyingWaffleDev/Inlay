using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
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
    private int _wordCount;
    private int _replacedWordStarts;
    private string _previewText = string.Empty;

    protected override Type StyleKeyOverride => typeof(TextEditor);

    public TemplateTextEditor()
    {
        Options.HighlightCurrentLine = true;
        Options.AllowScrollBelowDocument = false;
        Options.ShowColumnRulers = false;
        _lineLengthRenderer = new LineLengthIndicatorRenderer(TextArea.TextView);
        UpdateLineLengthIndicators();
        UpdateWordWrapping();
        SubscribeToDocument(Document);
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
        var marginWidth = TextArea.LeftMargins.Sum(margin => margin.DesiredSize.Width);
        var availableWidth = TextArea.Bounds.Width - marginWidth;
        WordWrap = ViewportWordWrap || EnforceHardLineLengthLimit;
        HorizontalScrollBarVisibility = !EnforceHardLineLengthLimit && !ViewportWordWrap
            ? ScrollBarVisibility.Auto
            : ScrollBarVisibility.Disabled;
        TextArea.Width = EnforceHardLineLengthLimit
            ? hardLimitWidth + marginWidth
            : double.NaN;
        TextArea.HorizontalAlignment = EnforceHardLineLengthLimit
            ? Avalonia.Layout.HorizontalAlignment.Left
            : Avalonia.Layout.HorizontalAlignment.Stretch;
        TextArea.TextView.HorizontalAlignment = EnforceHardLineLengthLimit
            ? Avalonia.Layout.HorizontalAlignment.Left
            : Avalonia.Layout.HorizontalAlignment.Stretch;
        TextArea.TextView.MinWidth = EnforceHardLineLengthLimit
            ? hardLimitWidth
            : ShowLineLengthIndicators
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

    public ITemplateEditorSession CaptureSession()
    {
        EnsureGenerator();
        var session = new NativeTemplateEditorSession(
            this,
            Document,
            _templateGenerator!.DetachDocument(),
            CaretOffset,
            SelectionStart,
            SelectionLength,
            _wordCount);
        ActivateDocument(
            new TextDocument(),
            TemplateTextElementGenerator.CreateEmptyState(),
            wordCount: 0);
        return session;
    }

    public void RestoreSession(ITemplateEditorSession session)
    {
        EnsureGenerator();
        if (session is not NativeTemplateEditorSession nativeSession ||
            !ReferenceEquals(nativeSession.Owner, this))
        {
            LoadDocument(session.ExportDocument());
            return;
        }

        _templateGenerator!.DetachDocument();
        ActivateDocument(
            nativeSession.Document,
            nativeSession.GeneratorState,
            nativeSession.WordCount);

        var selectionStart = Math.Clamp(
            nativeSession.SelectionStart,
            0,
            Document.TextLength);
        var selectionLength = Math.Clamp(
            nativeSession.SelectionLength,
            0,
            Document.TextLength - selectionStart);
        Select(selectionStart, selectionLength);
        CaretOffset = Math.Clamp(nativeSession.CaretOffset, 0, Document.TextLength);
    }

    public void LoadDocument(TemplateDocument document)
    {
        EnsureGenerator();
        _isApplyingDocument = true;
        try
        {
            _templateGenerator!.LoadDocument(document);
            Document.UndoStack.ClearAll();
            CaretOffset = 0;
            Select(0, 0);
            RefreshPreviewText();
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
        if (oldState is not null)
        {
            Dispatcher.UIThread.Post(() => TextArea.Focus(), DispatcherPriority.Input);
        }
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

    private void ActivateDocument(
        TextDocument document,
        TemplateTextElementGenerator.State generatorState,
        int wordCount)
    {
        UnsubscribeFromDocument(Document);
        Document = document;
        _wordCount = wordCount;
        RefreshPreviewText();
        SubscribeToDocument(Document);
        _templateGenerator!.AttachDocument(generatorState);
    }

    private void SubscribeToDocument(TextDocument document)
    {
        document.Changing += OnDocumentChanging;
        document.Changed += OnDocumentChanged;
        document.TextChanged += OnDocumentTextChanged;
    }

    private void UnsubscribeFromDocument(TextDocument document)
    {
        document.Changing -= OnDocumentChanging;
        document.Changed -= OnDocumentChanged;
        document.TextChanged -= OnDocumentTextChanged;
    }

    private void OnDocumentChanging(object? sender, DocumentChangeEventArgs e) =>
        _replacedWordStarts = CountWordStarts(Document, e.Offset, e.RemovalLength);

    private void OnDocumentChanged(object? sender, DocumentChangeEventArgs e)
    {
        _wordCount += CountWordStarts(Document, e.Offset, e.InsertionLength) -
                      _replacedWordStarts;
        RefreshPreviewText();
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
        UpdateSnapshot();
        if (!_isApplyingDocument)
        {
            EditorState?.ReportContentChanged(previewIsCurrent: true);
        }
    }

    private void OnTemplatesChanged()
    {
        UpdateSnapshot();
        if (!_isApplyingDocument)
        {
            EditorState?.ReportContentChanged(previewIsCurrent: true);
        }
    }

    private void UpdateSnapshot()
    {
        EditorState?.UpdateSnapshot(new EditorSnapshot(
            Document.TextLength,
            _wordCount,
            TextArea.Caret.Line,
            TextArea.Caret.Column,
            CanUndo,
            CanRedo,
            CanCut,
            CanCopy,
            CanPaste,
            CanDelete,
            CanSelectAll,
            _previewText));
    }

    private static int CountWordStarts(TextDocument document, int offset, int changedLength)
    {
        // A replacement can only alter word starts within its span and at the first
        // unchanged character after it.
        var count = 0;
        var endOffset = Math.Min(document.TextLength, offset + changedLength + 1);
        for (var index = offset; index < endOffset; index++)
        {
            if (char.IsLetterOrDigit(document.GetCharAt(index)) &&
                (index == 0 || !char.IsLetterOrDigit(document.GetCharAt(index - 1))))
            {
                count++;
            }
        }

        return count;
    }

    private void RefreshPreviewText()
    {
        const int maximumLength = 25;
        if (Document.TextLength == 0)
        {
            _previewText = string.Empty;
            return;
        }

        var firstLine = Document.Lines[0];
        Span<char> preview = stackalloc char[maximumLength];
        var previewLength = 0;
        for (var index = firstLine.Offset;
             index < firstLine.EndOffset && previewLength < maximumLength;
             index++)
        {
            var character = Document.GetCharAt(index);
            if (previewLength == 0 && char.IsWhiteSpace(character))
            {
                continue;
            }

            preview[previewLength++] = character;
        }

        while (previewLength > 0 && char.IsWhiteSpace(preview[previewLength - 1]))
        {
            previewLength--;
        }

        _previewText = new string(preview[..previewLength]);
    }

    private sealed class NativeTemplateEditorSession(
        TemplateTextEditor owner,
        TextDocument document,
        TemplateTextElementGenerator.State generatorState,
        int caretOffset,
        int selectionStart,
        int selectionLength,
        int wordCount) : ITemplateEditorSession
    {
        internal TemplateTextEditor Owner { get; } = owner;
        internal TextDocument Document { get; } = document;
        internal TemplateTextElementGenerator.State GeneratorState { get; } = generatorState;
        internal int CaretOffset { get; } = caretOffset;
        internal int SelectionStart { get; } = selectionStart;
        internal int SelectionLength { get; } = selectionLength;
        internal int WordCount { get; } = wordCount;

        public TemplateDocument ExportDocument() => GeneratorState.ExportDocument(Document);
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
