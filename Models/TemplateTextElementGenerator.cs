using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Inlay.Models;
using Inlay.ViewModels;

namespace Inlay;

internal sealed class TemplateTextElementGenerator : VisualLineElementGenerator
{
    private static readonly Size ClickAnchorSize = new(1, 1);
    private readonly TextEditor _editor;
    private List<TemplateInfo> _templates = [];
    private TextDocument? _attachedDocument;
    private Flyout? _flyout;
    private TemplateFlyoutView? _flyoutView;
    private TemplateFlyoutViewModel? _flyoutViewModel;
    private TemplateInfo? _flyoutTemplate;
    private Rect _flyoutAnchorRectangle;
    private bool _isReplayingOptionChange;
    private bool _isChangingTemplateText;

    public event Action? TemplatesChanged;

    internal bool HasCreatedFlyout => _flyout is not null;

    public TemplateTextElementGenerator(TextEditor editor)
    {
        _editor = editor;
        _attachedDocument = editor.Document;
        _attachedDocument.Changing += OnDocumentChanging;
        _editor.AddHandler(InputElement.KeyDownEvent, OnEditorKeyDown, RoutingStrategies.Tunnel);
        _editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
    }

    internal State DetachDocument()
    {
        _attachedDocument?.Changing -= OnDocumentChanging;
        _attachedDocument = null;

        CloseFlyout();
        var state = new State(_templates);
        _templates = [];
        return state;
    }

    internal static State CreateEmptyState() => new([]);

    internal void AttachDocument(State state)
    {
        if (_attachedDocument is not null)
        {
            throw new InvalidOperationException("The template generator is already attached to a document.");
        }

        _templates = state.Templates;
        _attachedDocument = _editor.Document;
        _attachedDocument.Changing += OnDocumentChanging;
        _editor.TextArea.TextView.Redraw();
    }

    public void AddTemplate(int offset, IEnumerable<string> options, int selectedIndex = -1)
    {
        var optionList = options.ToList();
        var selectedText = SelectedText(optionList, selectedIndex);

        RunUndoGroup(() =>
        {
            ChangeDocument(() => _editor.Document.Insert(offset, selectedText));
            var template = CreateTemplate(offset, optionList, selectedIndex);
            _editor.Document.UndoStack.Push(new TemplateMembershipOperation(this, template, true));
            AddTemplateMetadata(template);
        });

        NotifyChanged();
    }

    public TemplateDocument ExportDocument() => ExportDocument(_editor.Document, _templates);

    public void LoadDocument(TemplateDocument document)
    {
        CloseFlyout();
        _templates.Clear();
        ChangeDocument(() =>
        {
            _editor.Document.Text = string.Empty;

            foreach (var part in document.Content)
            {
                if (part.Type == DocumentPartKind.Text)
                {
                    _editor.Document.Insert(_editor.Document.TextLength, part.Text ?? string.Empty);
                    continue;
                }

                var options = part.Options ?? [];
                var selectedIndex = part.SelectedIndex ?? -1;
                var offset = _editor.Document.TextLength;
                _editor.Document.Insert(offset, SelectedText(options, selectedIndex));
                AddTemplateMetadata(CreateTemplate(offset, options, selectedIndex));
            }
        });

        _editor.TextArea.TextView.Redraw();
    }

    public override int GetFirstInterestedOffset(int startOffset) =>
        _templates.FirstOrDefault(template => template.Anchor.Offset >= startOffset)?.Anchor.Offset ?? -1;

    public override VisualLineElement? ConstructElement(int offset)
    {
        var info = _templates.FirstOrDefault(template => template.Anchor.Offset == offset);
        if (info is null)
        {
            return null;
        }

        return new TemplateTextElement(
            CurrentContext.VisualLine,
            info.Length,
            info.Anchor,
            _editor.TextArea.TextView,
            this);
    }

    private TemplateInfo CreateTemplate(int offset, IEnumerable<string> options, int selectedIndex)
    {
        var optionList = options.ToList();
        var text = SelectedText(optionList, selectedIndex);
        var observableOptions = new ObservableCollection<string>(optionList);
        var info = new TemplateInfo
        {
            Anchor = CreateAnchor(offset),
            DetachedOffset = offset,
            Options = observableOptions,
            SelectedText = text,
            Length = text.Length,
            SelectedIndex = selectedIndex
        };
        observableOptions.CollectionChanged += (_, e) => OnOptionsChanged(info, e);
        return info;
    }

    internal void RemoveTemplate(TemplateTextElement element)
    {
        var info = _templates.FirstOrDefault(template => template.Anchor == element.Anchor);
        if (info is null)
        {
            return;
        }

        RemoveTemplate(info);
    }

    private void RemoveTemplate(TemplateInfo info)
    {
        if (!_templates.Contains(info))
        {
            return;
        }

        var offset = info.Anchor.Offset;
        var oldCaretOffset = _editor.CaretOffset;
        var newCaretOffset = oldCaretOffset switch
        {
            var value when value <= offset => value,
            var value when value >= offset + info.Length => value - info.Length,
            _ => offset
        };

        if (oldCaretOffset >= offset)
        {
            _editor.CaretOffset = offset;
        }

        RunUndoGroup(() =>
        {
            RemoveTemplateMetadata(info, redraw: false);
            _editor.Document.UndoStack.Push(new TemplateMembershipOperation(this, info, false));
            ChangeDocument(() => _editor.Document.Remove(offset, info.Length));
        });

        _editor.CaretOffset = Math.Clamp(newCaretOffset, 0, _editor.Document.TextLength);
        NotifyChanged();
    }

    private void SelectOption(TemplateInfo info, string option)
    {
        if (_isReplayingOptionChange)
        {
            return;
        }

        if (!_templates.Contains(info) || info.Anchor.Offset < 0)
        {
            return;
        }

        var oldText = info.SelectedText;
        var oldIndex = info.SelectedIndex;
        var newIndex = info.Options.IndexOf(option);
        if (oldText == option && oldIndex == newIndex)
        {
            return;
        }

        RunUndoGroup(() =>
        {
            ChangeDocument(() =>
                _editor.Document.Replace(info.Anchor.Offset, info.Length, option));
            _editor.Document.UndoStack.Push(
                new TemplateSelectionOperation(this, info, oldText, oldIndex, option, newIndex));
            ApplySelectionMetadata(info, option, newIndex);
        });

        _editor.CaretOffset = Math.Clamp(_editor.CaretOffset, 0, _editor.Document.TextLength);
        NotifyChanged();
    }

    private void OnOptionsChanged(TemplateInfo info, NotifyCollectionChangedEventArgs e)
    {
        if (_isReplayingOptionChange)
        {
            return;
        }

        if (e.Action == NotifyCollectionChangedAction.Add &&
            e.NewStartingIndex >= 0 &&
            e.NewItems?.Count == 1 &&
            e.NewItems[0] is string addedOption)
        {
            RecordOptionAddition(info, addedOption, e.NewStartingIndex);
            return;
        }

        if (e.Action == NotifyCollectionChangedAction.Remove &&
            e.OldStartingIndex >= 0 &&
            e.OldItems?.Count == 1 &&
            e.OldItems[0] is string removedOption)
        {
            RecordOptionRemoval(info, removedOption, e.OldStartingIndex);
            return;
        }

        if (info.SelectedIndex >= info.Options.Count)
        {
            SelectByMetadata(info, TemplateTextElement.PlaceholderText, -1);
        }
        else
        {
            NotifyChanged();
        }
    }

    private void RecordOptionAddition(TemplateInfo info, string option, int addedIndex)
    {
        var oldIndex = info.SelectedIndex;
        var newIndex = oldIndex >= addedIndex && oldIndex >= 0 ? oldIndex + 1 : oldIndex;

        RunUndoGroup(() =>
        {
            _editor.Document.UndoStack.Push(new TemplateOptionCollectionOperation(
                this,
                info,
                option,
                addedIndex,
                info.SelectedText,
                oldIndex,
                info.SelectedText,
                newIndex,
                addOnRedo: true));
            ApplySelectionMetadata(info, info.SelectedText, newIndex);
        });

        NotifyChanged();
    }

    private void RecordOptionRemoval(TemplateInfo info, string option, int removedIndex)
    {
        var oldText = info.SelectedText;
        var oldIndex = info.SelectedIndex;
        var removedSelection = removedIndex == oldIndex;
        var newText = removedSelection ? TemplateTextElement.PlaceholderText : oldText;
        var newIndex = removedSelection
            ? -1
            : oldIndex > removedIndex ? oldIndex - 1 : oldIndex;

        RunUndoGroup(() =>
        {
            if (oldText != newText)
            {
                ChangeDocument(() =>
                    _editor.Document.Replace(info.Anchor.Offset, info.Length, newText));
            }

            _editor.Document.UndoStack.Push(new TemplateOptionCollectionOperation(
                this,
                info,
                option,
                removedIndex,
                oldText,
                oldIndex,
                newText,
                newIndex,
                addOnRedo: false));
            ApplySelectionMetadata(info, newText, newIndex);
        });

        ClampCaretOffset();
        NotifyChanged();
    }

    private void SelectByMetadata(TemplateInfo info, string text, int selectedIndex)
    {
        if (info.Anchor.Offset < 0)
        {
            return;
        }

        ChangeDocument(() => _editor.Document.Replace(info.Anchor.Offset, info.Length, text));
        ApplySelectionMetadata(info, text, selectedIndex);
        NotifyChanged();
    }

    private void ApplySelectionMetadata(TemplateInfo info, string text, int selectedIndex)
    {
        info.SelectedText = text;
        info.Length = text.Length;
        info.SelectedIndex = selectedIndex;
        if (ReferenceEquals(_flyoutTemplate, info))
        {
            _flyoutViewModel?.SynchronizeSelectedIndex(info.SelectedIndex);
            _flyoutViewModel?.SynchronizeOptions();
        }

        _editor.TextArea.TextView.Redraw();
    }

    private void AddTemplateMetadata(TemplateInfo template, bool recreateAnchor = false)
    {
        if (!_templates.Contains(template))
        {
            if (recreateAnchor)
            {
                template.Anchor = CreateAnchor(
                    Math.Clamp(template.DetachedOffset, 0, _editor.Document.TextLength));
            }

            _templates.Add(template);
            _templates.Sort((left, right) => left.Anchor.Offset.CompareTo(right.Anchor.Offset));
            _editor.TextArea.TextView.Redraw();
        }
    }

    private void RemoveTemplateMetadata(TemplateInfo template, bool redraw = true)
    {
        if (ReferenceEquals(_flyoutTemplate, template))
        {
            CloseFlyout();
        }

        template.DetachedOffset = template.Anchor.Offset;
        _templates.Remove(template);
        if (redraw)
        {
            _editor.TextArea.TextView.Redraw();
        }
    }

    internal ObservableCollection<string> GetOptions(TemplateTextElement element) =>
        FindTemplate(element).Options;

    internal int GetSelectedIndex(TemplateTextElement element) =>
        FindTemplate(element).SelectedIndex;

    internal Control GetFlyoutContent(TemplateTextElement element)
    {
        ActivateFlyout(element);
        return (Control)_flyoutView!.Content!;
    }

    internal Rect FlyoutAnchorRectangle => _flyoutAnchorRectangle;

    internal PlacementMode? FlyoutPlacement => _flyout?.Placement;

    internal bool IsFlyoutOpen(TemplateTextElement element)
    {
        var template = TryFindTemplate(element);
        return template is not null &&
               ReferenceEquals(_flyoutTemplate, template) &&
               _flyout?.IsOpen == true;
    }

    internal void ShowFlyoutAt(
        TemplateTextElement element,
        Control placementTarget,
        Point position)
    {
        CaptureFlyoutPosition(element, placementTarget, position);
        _flyout!.ShowAt(placementTarget);
    }

    internal void CaptureFlyoutPosition(
        TemplateTextElement element,
        Control placementTarget,
        Point position)
    {
        ActivateFlyout(element);
        var topLevel = TopLevel.GetTopLevel(placementTarget);
        var topLevelPosition = topLevel is null
            ? position
            : placementTarget.TranslatePoint(position, topLevel) ?? position;
        _flyoutAnchorRectangle = new Rect(topLevelPosition, ClickAnchorSize);
        _flyout!.Placement = PlacementMode.Custom;
        _flyout.CustomPopupPlacementCallback = placement =>
        {
            placement.AnchorRectangle = _flyoutAnchorRectangle;
            placement.Anchor = PopupAnchor.TopLeft;
            placement.Gravity = PopupGravity.BottomRight;
            placement.ConstraintAdjustment = PopupPositionerConstraintAdjustment.None;
        };
    }

    internal void CloseFlyout(TemplateTextElement element)
    {
        var template = TryFindTemplate(element);
        if (template is not null && ReferenceEquals(_flyoutTemplate, template))
        {
            CloseFlyout();
        }
    }

    private void ActivateFlyout(TemplateTextElement element)
    {
        var info = FindTemplate(element);
        if (ReferenceEquals(_flyoutTemplate, info) && _flyoutViewModel is not null)
        {
            return;
        }

        CloseFlyout();
        _flyoutTemplate = info;
        _flyoutViewModel = new TemplateFlyoutViewModel(
            info.Options,
            info.SelectedIndex,
            option => SelectOption(info, option),
            () => RemoveTemplate(info));
        _flyoutView ??= new TemplateFlyoutView();
        _flyoutView.DataContext = _flyoutViewModel;
        _flyout ??= CreateFlyout(_flyoutView);
    }

    private static Flyout CreateFlyout(TemplateFlyoutView view) =>
        new() { Content = view };

    private void CloseFlyout()
    {
        if (_flyout?.IsOpen == true)
        {
            _flyout.Hide();
        }

        ReleaseFlyoutViewModel();
    }

    private void ReleaseFlyoutViewModel()
    {
        _flyoutViewModel?.Disconnect();
        _flyoutViewModel = null;
        _flyoutTemplate = null;
        if (_flyoutView is not null)
        {
            _flyoutView.DataContext = null;
        }
    }

    private TemplateInfo FindTemplate(TemplateTextElement element) =>
        TryFindTemplate(element) ??
        throw new InvalidOperationException("The template element is no longer part of this document.");

    private TemplateInfo? TryFindTemplate(TemplateTextElement element) =>
        _templates.FirstOrDefault(template => template.Anchor == element.Anchor);

    private void OnDocumentChanging(object? sender, DocumentChangeEventArgs e)
    {
        if (_isChangingTemplateText || !_editor.Document.UndoStack.AcceptChanges)
        {
            return;
        }

        var removalEnd = e.Offset + e.RemovalLength;
        var affectedTemplates = _templates.Where(template =>
        {
            var templateStart = template.Anchor.Offset;
            var templateEnd = templateStart + template.Length;
            var removesTemplateText = e.RemovalLength > 0 &&
                                      e.Offset < templateEnd &&
                                      removalEnd > templateStart;
            var insertsInsideTemplate = e.InsertionLength > 0 &&
                                        e.Offset > templateStart &&
                                        e.Offset < templateEnd;
            return removesTemplateText || insertsInsideTemplate;
        }).ToList();

        foreach (var template in affectedTemplates)
        {
            // The document change is added to the same undo group after this callback.
            // Undo therefore restores the text before restoring this metadata.
            RemoveTemplateMetadata(template, redraw: false);
            _editor.Document.UndoStack.Push(
                new TemplateMembershipOperation(this, template, addOnRedo: false));
        }
    }

    private void UpdateAnchorMovementTypes()
    {
        var caret = _editor.TextArea.Caret;
        if (_editor.Document.IsInUpdate ||
            caret.Offset < 0 ||
            caret.Offset > _editor.Document.TextLength)
        {
            return;
        }

        var template = _templates.FirstOrDefault(item => item.Anchor.Offset == caret.Offset);
        if (template is null)
        {
            return;
        }

        template.Anchor.MovementType = caret.VisualColumn >= template.Anchor.Column
            ? AnchorMovementType.BeforeInsertion
            : AnchorMovementType.AfterInsertion;
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e) =>
        UpdateAnchorMovementTypes();

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_editor.TextArea.Selection.IsEmpty)
        {
            return;
        }

        var caret = _editor.TextArea.Caret;
        foreach (var template in _templates.Where(item =>
                     item.Anchor.Offset == caret.Offset || item.Anchor.Offset + item.Length == caret.Offset))
        {
            var visuallyBefore = caret.VisualColumn < template.Anchor.Column;
            var visuallyAfter = caret.VisualColumn > template.Anchor.Column;
            if ((e.Key == Key.Back && !visuallyBefore) ||
                (e.Key == Key.Delete && !visuallyAfter))
            {
                e.Handled = true;
                return;
            }
        }
    }

    private void NotifyChanged()
    {
        _editor.TextArea.TextView.Redraw();
        TemplatesChanged?.Invoke();
    }

    private void ClampCaretOffset()
    {
        if (_editor.CaretOffset < 0 || _editor.CaretOffset > _editor.Document.TextLength)
        {
            _editor.CaretOffset = Math.Clamp(_editor.CaretOffset, 0, _editor.Document.TextLength);
        }
    }

    private void ClampCaretOffset(int maximumOffset)
    {
        if (_editor.CaretOffset < 0 || _editor.CaretOffset > maximumOffset)
        {
            _editor.CaretOffset = Math.Clamp(_editor.CaretOffset, 0, maximumOffset);
        }
    }

    private void ReplayOptionChange(Action action)
    {
        _isReplayingOptionChange = true;
        try
        {
            action();
        }
        finally
        {
            _isReplayingOptionChange = false;
        }
    }

    private void RunUndoGroup(Action action)
    {
        _editor.Document.UndoStack.StartUndoGroup();
        try
        {
            action();
        }
        finally
        {
            _editor.Document.UndoStack.EndUndoGroup();
        }
    }

    private void ChangeDocument(Action action)
    {
        var wasChangingTemplateText = _isChangingTemplateText;
        _isChangingTemplateText = true;
        try
        {
            action();
        }
        finally
        {
            _isChangingTemplateText = wasChangingTemplateText;
        }
    }

    private TextAnchor CreateAnchor(int offset)
    {
        var anchor = _editor.Document.CreateAnchor(offset);
        anchor.SurviveDeletion = true;
        return anchor;
    }

    private static string SelectedText(List<string> options, int selectedIndex) =>
        selectedIndex >= 0 && selectedIndex < options.Count
            ? options[selectedIndex]
            : TemplateTextElement.PlaceholderText;

    private static void AddTextPart(List<DocumentPart> content, string text)
    {
        if (text.Length > 0)
        {
            content.Add(DocumentPart.PlainText(text));
        }
    }

    private static TemplateDocument ExportDocument(
        TextDocument document,
        IEnumerable<TemplateInfo> templates)
    {
        var content = new List<DocumentPart>();
        var cursor = 0;

        foreach (var template in templates.OrderBy(item => item.Anchor.Offset))
        {
            var offset = template.Anchor.Offset;
            if (offset < cursor || offset + template.Length > document.TextLength)
            {
                continue;
            }

            AddTextPart(content, document.GetText(cursor, offset - cursor));
            content.Add(DocumentPart.Template(template.Options, template.SelectedIndex));
            cursor = offset + template.Length;
        }

        AddTextPart(content, document.GetText(cursor, document.TextLength - cursor));
        return new TemplateDocument { Content = content };
    }

    internal sealed class State
    {
        private readonly List<TemplateInfo> _templates;

        internal State(List<TemplateInfo> templates)
        {
            _templates = templates;
        }

        internal List<TemplateInfo> Templates => _templates;

        internal TemplateDocument ExportDocument(TextDocument document) =>
            TemplateTextElementGenerator.ExportDocument(document, _templates);
    }

    internal sealed class TemplateInfo
    {
        public required TextAnchor Anchor { get; set; }
        public required int DetachedOffset { get; set; }
        public required ObservableCollection<string> Options { get; init; }
        public required string SelectedText { get; set; }
        public required int Length { get; set; }
        public required int SelectedIndex { get; set; }
    }

    private sealed class TemplateMembershipOperation(
        TemplateTextElementGenerator generator,
        TemplateInfo template,
        bool addOnRedo) : IUndoableOperation
    {
        public void Undo()
        {
            if (addOnRedo)
            {
                generator.RemoveTemplateMetadata(template);
            }
            else
            {
                generator.AddTemplateMetadata(template, recreateAnchor: true);
            }

            generator.ClampCaretOffset();
            generator.TemplatesChanged?.Invoke();
        }

        public void Redo()
        {
            if (addOnRedo)
            {
                generator.AddTemplateMetadata(template, recreateAnchor: true);
            }
            else
            {
                generator.RemoveTemplateMetadata(template);
            }

            generator.ClampCaretOffset();
            generator.TemplatesChanged?.Invoke();
        }
    }

    private sealed class TemplateSelectionOperation(
        TemplateTextElementGenerator generator,
        TemplateInfo template,
        string oldText,
        int oldIndex,
        string newText,
        int newIndex) : IUndoableOperation
    {
        public void Undo()
        {
            generator.ApplySelectionMetadata(template, oldText, oldIndex);
            generator.ClampCaretOffset();
            generator.TemplatesChanged?.Invoke();
        }

        public void Redo()
        {
            generator.ApplySelectionMetadata(template, newText, newIndex);
            generator.ClampCaretOffset();
            generator.TemplatesChanged?.Invoke();
        }
    }

    private sealed class TemplateOptionCollectionOperation(
        TemplateTextElementGenerator generator,
        TemplateInfo template,
        string option,
        int optionIndex,
        string oldText,
        int oldIndex,
        string newText,
        int newIndex,
        bool addOnRedo) : IUndoableOperation
    {
        public void Undo()
        {
            ReplayCollectionChange(add: !addOnRedo);
            generator.ApplySelectionMetadata(template, oldText, oldIndex);
            var restoredDocumentLength = generator._editor.Document.TextLength -
                                         newText.Length + oldText.Length;
            generator.ClampCaretOffset(restoredDocumentLength);
            generator.TemplatesChanged?.Invoke();
        }

        public void Redo()
        {
            ReplayCollectionChange(addOnRedo);
            generator.ApplySelectionMetadata(template, newText, newIndex);
            generator.ClampCaretOffset();
            generator.TemplatesChanged?.Invoke();
        }

        private void ReplayCollectionChange(bool add)
        {
            generator.ReplayOptionChange(() =>
            {
                if (add)
                {
                    template.Options.Insert(Math.Min(optionIndex, template.Options.Count), option);
                    return;
                }

                var currentIndex = optionIndex < template.Options.Count &&
                                   template.Options[optionIndex] == option
                    ? optionIndex
                    : template.Options.IndexOf(option);
                if (currentIndex >= 0)
                {
                    template.Options.RemoveAt(currentIndex);
                }
            });
        }
    }
}
