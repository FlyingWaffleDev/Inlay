using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Inlay.Models;

namespace Inlay;

internal sealed class TemplateTextElementGenerator : VisualLineElementGenerator
{
    private readonly TextEditor _editor;
    private readonly List<TemplateInfo> _templates = [];
    private bool _isReplayingOptionChange;

    public event Action? TemplatesChanged;

    public TemplateTextElementGenerator(TextEditor editor)
    {
        _editor = editor;
        _editor.AddHandler(InputElement.KeyDownEvent, OnEditorKeyDown, RoutingStrategies.Tunnel);
        _editor.TextArea.Caret.PositionChanged += (_, _) => UpdateAnchorMovementTypes();
    }

    public void AddTemplate(int offset, IEnumerable<string> options, int selectedIndex = -1)
    {
        var optionList = options.ToList();
        var selectedText = SelectedText(optionList, selectedIndex);

        RunUndoGroup(() =>
        {
            _editor.Document.Insert(offset, selectedText);
            var template = CreateTemplate(offset, optionList, selectedIndex);
            _editor.Document.UndoStack.Push(new TemplateMembershipOperation(this, template, true));
            AddTemplateMetadata(template);
        });

        NotifyChanged();
    }

    public TemplateDocument ExportDocument()
    {
        var content = new List<DocumentPart>();
        var cursor = 0;

        foreach (var template in _templates.OrderBy(item => item.Anchor.Offset))
        {
            var offset = template.Anchor.Offset;
            if (offset < cursor || offset + template.Length > _editor.Document.TextLength)
            {
                continue;
            }

            AddTextPart(content, _editor.Document.GetText(cursor, offset - cursor));
            content.Add(DocumentPart.Template(template.Options, template.SelectedIndex));
            cursor = offset + template.Length;
        }

        AddTextPart(content, _editor.Document.GetText(cursor, _editor.Document.TextLength - cursor));
        return new TemplateDocument { Content = content };
    }

    public void LoadDocument(TemplateDocument document)
    {
        _templates.Clear();
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

        var element = new TemplateTextElement(
            CurrentContext.VisualLine,
            info.Length,
            info.Anchor,
            info.Options,
            info.SelectedIndex,
            _editor.TextArea.TextView);
        RegisterElement(info, element);
        element.Removed += RemoveTemplate;
        element.OptionSelected += SelectOption;
        return element;
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

    private void RemoveTemplate(TemplateTextElement element)
    {
        var info = _templates.FirstOrDefault(template => template.Anchor == element.Anchor);
        if (info is null)
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
            _editor.Document.Remove(offset, info.Length);
        });

        _editor.CaretOffset = Math.Clamp(newCaretOffset, 0, _editor.Document.TextLength);
        NotifyChanged();
    }

    private void SelectOption(TemplateTextElement element, string option)
    {
        if (_isReplayingOptionChange)
        {
            return;
        }

        var info = _templates.FirstOrDefault(template => template.Anchor == element.Anchor);
        if (info is null || info.Anchor.Offset < 0)
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
            _editor.Document.Replace(info.Anchor.Offset, info.Length, option);
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
                _editor.Document.Replace(info.Anchor.Offset, info.Length, newText);
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

        _editor.Document.Replace(info.Anchor.Offset, info.Length, text);
        ApplySelectionMetadata(info, text, selectedIndex);
        NotifyChanged();
    }

    private void ApplySelectionMetadata(TemplateInfo info, string text, int selectedIndex)
    {
        info.SelectedText = text;
        info.Length = text.Length;
        info.SelectedIndex = selectedIndex;
        SynchronizeElements(info);
        _editor.TextArea.TextView.Redraw();
    }

    private static void RegisterElement(TemplateInfo info, TemplateTextElement element)
    {
        RemoveDeadElements(info);
        info.Elements.Add(new WeakReference<TemplateTextElement>(element));
    }

    private static void SynchronizeElements(TemplateInfo info)
    {
        for (var index = info.Elements.Count - 1; index >= 0; index--)
        {
            if (info.Elements[index].TryGetTarget(out var element))
            {
                element.SynchronizeSelectedIndex(info.SelectedIndex);
            }
            else
            {
                info.Elements.RemoveAt(index);
            }
        }
    }

    private static void RemoveDeadElements(TemplateInfo info)
    {
        for (var index = info.Elements.Count - 1; index >= 0; index--)
        {
            if (!info.Elements[index].TryGetTarget(out _))
            {
                info.Elements.RemoveAt(index);
            }
        }
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
        CloseElementFlyouts(template);
        template.DetachedOffset = template.Anchor.Offset;
        _templates.Remove(template);
        if (redraw)
        {
            _editor.TextArea.TextView.Redraw();
        }
    }

    private static void CloseElementFlyouts(TemplateInfo info)
    {
        for (var index = info.Elements.Count - 1; index >= 0; index--)
        {
            if (info.Elements[index].TryGetTarget(out var element))
            {
                element.CloseFlyout();
            }
            else
            {
                info.Elements.RemoveAt(index);
            }
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

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
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

    private sealed class TemplateInfo
    {
        public required TextAnchor Anchor { get; set; }
        public required int DetachedOffset { get; set; }
        public required ObservableCollection<string> Options { get; init; }
        public required string SelectedText { get; set; }
        public required int Length { get; set; }
        public required int SelectedIndex { get; set; }
        public List<WeakReference<TemplateTextElement>> Elements { get; } = [];
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
