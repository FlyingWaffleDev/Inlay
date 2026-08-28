using System.Text;
using Inlay.Models;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Inlay.ViewModels;

internal sealed partial class DocumentTabViewModel : ReactiveObject, IDisposable
{
    private const int UntitledPreviewMaxLength = 24;
    private readonly Func<DocumentTabViewModel, Task> _closeAsync;
    private readonly int _untitledOrdinal;
    private DocumentFileVersion? _knownFileVersion;
    private bool _isApplyingDocument;

    public DocumentTabViewModel(
        TemplateDocument document,
        Func<DocumentTabViewModel, Task> closeAsync,
        IDocumentFile? file = null,
        int untitledOrdinal = 1)
    {
        _closeAsync = closeAsync;
        _untitledOrdinal = untitledOrdinal;
        File = file;
        Editor.ContentChanged += MarkDirty;
        ApplyDocument(document);
        RefreshFileVersion();
    }

    [Reactive(SetModifier = AccessModifier.Private)]
    private string _header = "Untitled";

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _isDirty;

    private bool _hasExternalChanges;

    public TemplateEditorViewModel Editor { get; } = new();

    public IDocumentFile? File { get; private set; }

    internal int UntitledOrdinal => _untitledOrdinal;

    public string FileName => File?.Name ?? "Untitled";

    public string ExternalChangesMessage => $"{FileName} changed outside Inlay. Reload it or ignore the changes?";

    public bool IsEmptyUntitled =>
        File is null && !IsDirty && Editor.ExportDocument().Content.Count == 0;

    public bool HasExternalChanges
    {
        get => _hasExternalChanges;
        private set
        {
            if (_hasExternalChanges == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _hasExternalChanges, value);
            this.RaisePropertyChanged(nameof(CanEdit));
        }
    }

    public bool CanEdit => !HasExternalChanges;

    public void MarkSaved(IDocumentFile file)
    {
        File = file;
        IsDirty = false;
        HasExternalChanges = false;
        UpdateHeader();
        this.RaisePropertyChanged(nameof(ExternalChangesMessage));
        RefreshFileVersion();
    }

    public void CheckForExternalChanges()
    {
        var version = File?.GetVersion();
        if (version is null || _knownFileVersion is null)
        {
            _knownFileVersion = version;
            return;
        }

        if (version == _knownFileVersion)
        {
            return;
        }

        _knownFileVersion = version;
        MarkDirty();
        HasExternalChanges = true;
    }

    public void IgnoreExternalChanges() => HasExternalChanges = false;

    public void Reload(TemplateDocument document)
    {
        ApplyDocument(document);
        HasExternalChanges = false;
        RefreshFileVersion();
    }

    public void Dispose()
    {
        Editor.ContentChanged -= MarkDirty;
    }

    [ReactiveCommand]
    private async Task CloseAsync() => await _closeAsync(this);

    private void ApplyDocument(TemplateDocument document)
    {
        _isApplyingDocument = true;
        try
        {
            Editor.LoadDocument(document);
            IsDirty = false;
            UpdateHeader();
        }
        finally
        {
            _isApplyingDocument = false;
        }
    }

    private void MarkDirty()
    {
        if (_isApplyingDocument)
        {
            return;
        }

        if (!IsDirty)
        {
            IsDirty = true;
        }

        UpdateHeader();
    }

    private void UpdateHeader()
    {
        var name = File is null ? UntitledName() : FileName;
        Header = $"{name}{(IsDirty ? " *" : string.Empty)}";
    }

    private string UntitledName()
    {
        var firstLine = FirstLine(Editor.ExportDocument());
        if (firstLine.Length > 0)
        {
            var preview = firstLine.Length <= UntitledPreviewMaxLength
                ? firstLine
                : $"{firstLine[..UntitledPreviewMaxLength].TrimEnd()}…";
            return $"Untitled ({preview})";
        }

        return _untitledOrdinal > 1 ? $"Untitled ({_untitledOrdinal})" : "Untitled";
    }

    private static string FirstLine(TemplateDocument document)
    {
        var text = new StringBuilder(UntitledPreviewMaxLength + 1);
        foreach (var part in document.Content)
        {
            var partText = part.Type switch
            {
                DocumentPartKind.Text => part.Text ?? string.Empty,
                _ when part.SelectedIndex is int selectedIndex &&
                       part.Options is { } options &&
                       selectedIndex >= 0 && selectedIndex < options.Count => options[selectedIndex],
                _ => "_____"
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
                if (text.Length > UntitledPreviewMaxLength)
                {
                    return text.ToString();
                }
            }
        }

        return text.ToString().TrimEnd();
    }

    private void RefreshFileVersion() => _knownFileVersion = File?.GetVersion();
}
