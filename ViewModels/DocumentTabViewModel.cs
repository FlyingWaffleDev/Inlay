using System.Threading;
using Inlay.Models;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Inlay.ViewModels;

internal readonly record struct DocumentFileState(
    DocumentFileVersion? KnownVersion,
    bool HasExternalChanges,
    bool ChangedInAnotherWindow);

internal sealed partial class DocumentTabViewModel : ReactiveObject, IDisposable
{
    private const int UntitledPreviewMaxLength = 24;
    private static readonly Lock LiveTabsLock = new();
    private static readonly List<DocumentTabViewModel> LiveTabs = [];
    private readonly Func<DocumentTabViewModel, Task> _closeAsync;
    private readonly int _untitledOrdinal;
    private DocumentFileVersion? _knownFileVersion;
    private bool _isApplyingDocument;

    public DocumentTabViewModel(
        TemplateDocument document,
        Func<DocumentTabViewModel, Task> closeAsync,
        IDocumentFile? file = null,
        int untitledOrdinal = 1,
        Guid? documentId = null,
        DocumentFileState? fileState = null,
        bool isDirty = false)
    {
        _closeAsync = closeAsync;
        _untitledOrdinal = untitledOrdinal;
        DocumentId = documentId ?? Guid.NewGuid();
        File = file;
        Editor.ContentChanged += MarkDirty;
        ApplyDocument(document);
        if (fileState is { } state)
        {
            _knownFileVersion = state.KnownVersion;
            HasExternalChanges = state.HasExternalChanges;
            ChangedInAnotherWindow = state.ChangedInAnotherWindow;
        }
        else
        {
            RefreshFileVersion();
        }

        if (isDirty)
        {
            MarkDirty();
        }

        // Registering publishes this tab to other threads, so it stays last: every
        // field another tab could read must already be written. Dispose unregisters,
        // and a tab that is never disposed stays here for the life of the process.
        lock (LiveTabsLock)
        {
            LiveTabs.Add(this);
        }
    }

    public Guid DocumentId { get; }

    [Reactive(SetModifier = AccessModifier.Private)]
    private string _header = "Untitled";

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _isDirty;

    private bool _hasExternalChanges;
    private bool _changedInAnotherWindow;

    public TemplateEditorViewModel Editor { get; } = new();

    public IDocumentFile? File { get; private set; }

    internal int UntitledOrdinal => _untitledOrdinal;

    public string FileName => File?.Name ?? "Untitled";

    public bool ChangedInAnotherWindow
    {
        get => _changedInAnotherWindow;
        private set
        {
            if (_changedInAnotherWindow == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _changedInAnotherWindow, value);
            this.RaisePropertyChanged(nameof(ExternalChangesMessage));
        }
    }

    public string ExternalChangesMessage =>
        ChangedInAnotherWindow
            ? $"{FileName} was changed in another window. Reload it or ignore the changes?"
            : $"{FileName} changed outside Inlay. Reload it or ignore the changes?";

    public bool IsEmptyUntitled() =>
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

    internal DocumentFileState CaptureFileState() =>
        new(_knownFileVersion, HasExternalChanges, ChangedInAnotherWindow);

    public void MarkSaved(IDocumentFile file)
    {
        File = file;
        IsDirty = false;
        HasExternalChanges = false;
        ChangedInAnotherWindow = false;
        UpdateHeader();
        RefreshFileVersion();
        this.RaisePropertyChanged(nameof(ExternalChangesMessage));
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
        ChangedInAnotherWindow = WasWrittenByAnotherTab(version.Value);
        HasExternalChanges = true;
    }

    public void IgnoreExternalChanges()
    {
        HasExternalChanges = false;
        ChangedInAnotherWindow = false;
    }

    public void Reload(TemplateDocument document)
    {
        ApplyDocument(document);
        HasExternalChanges = false;
        ChangedInAnotherWindow = false;
        RefreshFileVersion();
    }

    public void Dispose()
    {
        Editor.ContentChanged -= MarkDirty;
        lock (LiveTabsLock)
        {
            LiveTabs.Remove(this);
        }
    }

    internal static bool IsTracked(DocumentTabViewModel tab)
    {
        lock (LiveTabsLock)
        {
            return LiveTabs.Contains(tab);
        }
    }

    // A version that some other open tab already knows about was written by Inlay itself,
    // not by an external editor.
    private bool WasWrittenByAnotherTab(DocumentFileVersion version)
    {
        if (File is null)
        {
            return false;
        }

        lock (LiveTabsLock)
        {
            return LiveTabs.Exists(other =>
                !ReferenceEquals(other, this) &&
                other._knownFileVersion == version &&
                DocumentFileIdentity.Matches(other.File, File.Identity));
        }
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
        var firstLine = Editor.PreviewText;
        if (firstLine.Length > 0)
        {
            var preview = firstLine.Length <= UntitledPreviewMaxLength
                ? firstLine
                : $"{firstLine[..UntitledPreviewMaxLength].TrimEnd()}…";
            return $"Untitled ({preview})";
        }

        return _untitledOrdinal > 1 ? $"Untitled ({_untitledOrdinal})" : "Untitled";
    }

    private void RefreshFileVersion() => _knownFileVersion = File?.GetVersion();
}
