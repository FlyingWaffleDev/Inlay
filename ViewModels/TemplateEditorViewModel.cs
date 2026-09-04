using Inlay.Models;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Inlay.ViewModels;

internal readonly record struct EditorSnapshot(
    int CharacterCount,
    int WordCount,
    int CaretLine,
    int CaretColumn,
    bool CanUndo,
    bool CanRedo,
    bool CanCut = false,
    bool CanCopy = false,
    bool CanPaste = false,
    bool CanDelete = false,
    bool CanSelectAll = false,
    string PreviewText = "");

internal interface ITemplateEditorSession
{
    TemplateDocument ExportDocument();
}

internal sealed class SerializedTemplateEditorSession(TemplateDocument document)
    : ITemplateEditorSession
{
    public TemplateDocument ExportDocument() => document;
}

internal interface ITemplateEditorAdapter
{
    TemplateDocument ExportDocument();
    ITemplateEditorSession CaptureSession();
    void RestoreSession(ITemplateEditorSession session);
    void LoadDocument(TemplateDocument document);
    void InsertTemplate();
    void Undo();
    void Redo();
    void Cut();
    void Copy();
    void Paste();
    void Delete();
    void SelectAll();
    void Find();
    void Replace();
}

internal sealed partial class TemplateEditorViewModel : ReactiveObject
{
    private ITemplateEditorAdapter? _adapter;
    private ITemplateEditorSession? _detachedSession;
    private bool _isApplyingDocument;
    private bool _showLineLengthIndicators;
    private bool _enforceHardLineLengthLimit;
    private int _softLineLengthLimit = LineLengthSettings.DefaultSoftLimit;
    private int _hardLineLengthLimit = LineLengthSettings.DefaultHardLimit;

    [Reactive(SetModifier = AccessModifier.Private)]
    private string _diagnostics = "Characters 0   Words 0   Line 1   Column 1";

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _canUndo;

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _canRedo;

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _canCut;

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _canCopy;

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _canPaste;

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _canDelete;

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _canSelectAll;

    [Reactive(SetModifier = AccessModifier.Private)]
    private string _previewText = string.Empty;

    public event Action? ContentChanged;

    public bool ShowLineLengthIndicators
    {
        get => _showLineLengthIndicators;
        set => SetDocumentSetting(
            ref _showLineLengthIndicators,
            value,
            nameof(ShowLineLengthIndicators));
    }

    public bool EnforceHardLineLengthLimit
    {
        get => _enforceHardLineLengthLimit;
        set => SetDocumentSetting(
            ref _enforceHardLineLengthLimit,
            value,
            nameof(EnforceHardLineLengthLimit));
    }

    public int SoftLineLengthLimit
    {
        get => _softLineLengthLimit;
        set
        {
            var limit = ClampLimit(value);
            if (_softLineLengthLimit == limit)
            {
                return;
            }

            if (_hardLineLengthLimit < limit)
            {
                _hardLineLengthLimit = limit;
                this.RaisePropertyChanged(nameof(HardLineLengthLimit));
            }

            _softLineLengthLimit = limit;
            this.RaisePropertyChanged();
            ReportSettingsChanged();
        }
    }

    public int HardLineLengthLimit
    {
        get => _hardLineLengthLimit;
        set
        {
            var limit = ClampLimit(value);
            if (limit < SoftLineLengthLimit)
            {
                if (_hardLineLengthLimit == _softLineLengthLimit)
                {
                    _softLineLengthLimit = limit;
                    this.RaisePropertyChanged(nameof(SoftLineLengthLimit));
                }
                else
                {
                    limit = SoftLineLengthLimit;
                }
            }

            if (_hardLineLengthLimit == limit)
            {
                return;
            }

            _hardLineLengthLimit = limit;
            this.RaisePropertyChanged();
            ReportSettingsChanged();
        }
    }

    public void Attach(ITemplateEditorAdapter adapter)
    {
        _adapter = adapter;
        if (_detachedSession is not null)
        {
            adapter.RestoreSession(_detachedSession);
            _detachedSession = null;
        }
    }

    public void Detach(ITemplateEditorAdapter adapter)
    {
        if (ReferenceEquals(_adapter, adapter))
        {
            _detachedSession = adapter.CaptureSession();
            _adapter = null;
        }
    }

    public void LoadDocument(TemplateDocument document)
    {
        ApplyLineLengthSettings(document.LineLength);
        PreviewText = FirstLine(document);
        if (_adapter is null)
        {
            _detachedSession = new SerializedTemplateEditorSession(document);
        }
        else
        {
            _adapter.LoadDocument(document);
        }
    }

    public TemplateDocument ExportDocument()
    {
        var document = _adapter?.ExportDocument() ??
                       _detachedSession?.ExportDocument() ??
                       new TemplateDocument();
        return WithCurrentLineLengthSettings(document);
    }

    private TemplateDocument WithCurrentLineLengthSettings(TemplateDocument document)
    {
        var lineLength = document.LineLength;
        if (lineLength.Show == ShowLineLengthIndicators &&
            lineLength.Enforce == EnforceHardLineLengthLimit &&
            lineLength.SoftLimit == SoftLineLengthLimit &&
            lineLength.HardLimit == HardLineLengthLimit)
        {
            return document;
        }

        return new TemplateDocument
        {
            FormatVersion = document.FormatVersion,
            LineLength = CurrentLineLengthSettings(),
            Content = document.Content
        };
    }

    public void InsertTemplate() => _adapter?.InsertTemplate();
    public void Undo() => _adapter?.Undo();
    public void Redo() => _adapter?.Redo();
    public void Cut() => _adapter?.Cut();
    public void Copy() => _adapter?.Copy();
    public void Paste() => _adapter?.Paste();
    public void Delete() => _adapter?.Delete();
    public void SelectAll() => _adapter?.SelectAll();
    public void Find() => _adapter?.Find();
    public void Replace() => _adapter?.Replace();
    public void ReportContentChanged(bool previewIsCurrent = false)
    {
        if (!previewIsCurrent && _adapter is not null)
        {
            PreviewText = FirstLine(_adapter.ExportDocument());
        }

        ContentChanged?.Invoke();
    }

    private void ApplyLineLengthSettings(LineLengthSettings settings)
    {
        _isApplyingDocument = true;
        try
        {
            ShowLineLengthIndicators = settings.Show;
            EnforceHardLineLengthLimit = settings.Enforce;
            SoftLineLengthLimit = settings.SoftLimit;
            HardLineLengthLimit = settings.HardLimit;
        }
        finally
        {
            _isApplyingDocument = false;
        }
    }

    private static int ClampLimit(int value) =>
        Math.Clamp(value, LineLengthSettings.MinimumLimit, LineLengthSettings.MaximumLimit);

    private LineLengthSettings CurrentLineLengthSettings() =>
        new()
        {
            Show = ShowLineLengthIndicators,
            Enforce = EnforceHardLineLengthLimit,
            SoftLimit = SoftLineLengthLimit,
            HardLimit = HardLineLengthLimit
        };

    private void SetDocumentSetting(ref bool field, bool value, string propertyName)
    {
        if (field == value)
        {
            return;
        }

        this.RaiseAndSetIfChanged(ref field, value, propertyName);
        ReportSettingsChanged();
    }

    private void ReportSettingsChanged()
    {
        if (!_isApplyingDocument)
        {
            ReportContentChanged(previewIsCurrent: true);
        }
    }

    public void UpdateSnapshot(EditorSnapshot snapshot)
    {
        Diagnostics =
            $"Characters {snapshot.CharacterCount}   Words {snapshot.WordCount}   " +
            $"Line {snapshot.CaretLine}   Column {snapshot.CaretColumn}";
        CanUndo = snapshot.CanUndo;
        CanRedo = snapshot.CanRedo;
        CanCut = snapshot.CanCut;
        CanCopy = snapshot.CanCopy;
        CanPaste = snapshot.CanPaste;
        CanDelete = snapshot.CanDelete;
        CanSelectAll = snapshot.CanSelectAll;
        PreviewText = snapshot.PreviewText;
    }

    private static string FirstLine(TemplateDocument document)
    {
        const int previewLength = 25;
        var text = new System.Text.StringBuilder(previewLength);
        foreach (var part in document.Content)
        {
            var partText = part.Type switch
            {
                DocumentPartKind.Text => part.Text ?? string.Empty,
                _ when part.SelectedIndex is int selectedIndex &&
                       part.Options is { } options &&
                       selectedIndex >= 0 && selectedIndex < options.Count => options[selectedIndex],
                _ => TemplateTextElement.PlaceholderText
            };

            foreach (var character in partText)
            {
                if (character is '\r' or '\n')
                {
                    return text.ToString().TrimEnd();
                }

                if (text.Length == 0 && char.IsWhiteSpace(character))
                {
                    continue;
                }

                text.Append(character);
                if (text.Length == previewLength)
                {
                    return text.ToString();
                }
            }
        }

        return text.ToString().TrimEnd();
    }
}
