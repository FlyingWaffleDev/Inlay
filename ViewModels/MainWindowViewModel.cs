using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Media;
using Inlay.Models;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Inlay.ViewModels;

internal sealed partial class MainWindowViewModel : ReactiveObject, IDisposable
{
    private const double DefaultEditorFontSize = 15;
    private static readonly FontFamily DefaultEditorFontFamily =
        new("Cascadia Mono,Consolas,DejaVu Sans Mono,monospace");
    private const double MinimumEditorFontSize = 8;
    private const double MaximumEditorFontSize = 40;
    private readonly ITemplateDocumentService _documentService;
    private readonly IDocumentStorageService _storageService;
    private readonly IUserInteractionService _interactionService;
    private readonly IApplicationService _applicationService;
    private readonly TemplateEditorViewModel _emptyEditor = new();
    private readonly IObservable<bool> _canEditDocument;
    private DocumentTabViewModel? _selectedDocument;

    [Reactive(SetModifier = AccessModifier.Private)]
    private string _title = "Untitled - Inlay";

    [Reactive]
    private bool _isStatusBarVisible = true;

    [Reactive]
    private bool _isWordWrapEnabled = true;

    [Reactive]
    private bool _isCurrentLineHighlightEnabled = true;

    [Reactive(SetModifier = AccessModifier.Private)]
    private double _editorFontSize = DefaultEditorFontSize;

    [Reactive(SetModifier = AccessModifier.Private)]
    private FontFamily _editorFontFamily = DefaultEditorFontFamily;

    public MainWindowViewModel(
        ITemplateDocumentService documentService,
        IDocumentStorageService storageService,
        IUserInteractionService interactionService,
        IApplicationService applicationService)
    {
        _documentService = documentService;
        _storageService = storageService;
        _interactionService = interactionService;
        _applicationService = applicationService;
        _canEditDocument = this.WhenAnyValue(viewModel => viewModel.SelectedDocument!.CanEdit);

        AddNewDocument();
    }

    public ObservableCollection<DocumentTabViewModel> Documents { get; } = [];

    public DocumentTabViewModel? SelectedDocument
    {
        get => _selectedDocument;
        set
        {
            if (ReferenceEquals(_selectedDocument, value))
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _selectedDocument, value);
            this.RaisePropertyChanged(nameof(Editor));
            UpdateTitle();
            value?.CheckForExternalChanges();
        }
    }

    public TemplateEditorViewModel Editor => SelectedDocument?.Editor ?? _emptyEditor;

    public bool IsDirty => Documents.Any(document => document.IsDirty);

    public void AddNewDocument() => AddDocument(new TemplateDocument());

    public void ReorderDocument(DocumentTabViewModel document, int targetSlot)
    {
        var sourceIndex = Documents.IndexOf(document);
        if (sourceIndex < 0)
        {
            return;
        }

        var newIndex = DragReorder.SlotToIndex(targetSlot, sourceIndex, Documents.Count);
        if (newIndex != sourceIndex)
        {
            Documents.Move(sourceIndex, newIndex);
        }

        SelectedDocument = document;
    }

    public bool ContainsDocument(DocumentTabViewModel document) =>
        FindMatchingDocument(document.DocumentId, document.File) is not null;

    public DocumentTabViewModel CopyDocument(
        TemplateDocument document,
        IDocumentFile? file,
        bool isDirty,
        int targetIndex = -1,
        Guid? documentId = null,
        DocumentFileState? fileState = null)
    {
        var targetDocId = documentId ?? Guid.NewGuid();
        var existingDocument = FindMatchingDocument(targetDocId, file);
        if (existingDocument is not null)
        {
            SelectedDocument = existingDocument;
            existingDocument.CheckForExternalChanges();
            return existingDocument;
        }

        var shouldReplacePristineUntitled = Documents.Count == 1 && Documents[0].IsEmptyUntitled();
        var untitledOrdinal = file is null && !shouldReplacePristineUntitled
            ? NextUntitledOrdinal()
            : 1;

        var tab = new DocumentTabViewModel(
            document,
            CloseDocumentAsync,
            file,
            untitledOrdinal,
            targetDocId,
            fileState,
            isDirty);
        if (fileState is not null)
        {
            tab.CheckForExternalChanges();
        }

        tab.PropertyChanged += OnDocumentPropertyChanged;

        if (shouldReplacePristineUntitled)
        {
            var oldTab = Documents[0];
            Documents[0] = tab;
            oldTab.PropertyChanged -= OnDocumentPropertyChanged;
            oldTab.Dispose();
        }
        else if (targetIndex >= 0 && targetIndex <= Documents.Count)
        {
            Documents.Insert(targetIndex, tab);
        }
        else
        {
            Documents.Add(tab);
        }

        SelectedDocument = tab;
        NotifyDocumentStateChanged();
        return tab;
    }

    public DocumentTabViewModel CopyDocument(DocumentTabViewModel sourceTab, int targetIndex = -1) =>
        CopyDocument(
            sourceTab.Editor.ExportDocument(),
            sourceTab.File,
            sourceTab.IsDirty,
            targetIndex,
            sourceTab.DocumentId,
            sourceTab.CaptureFileState());

    public void AdjustEditorZoom(int steps)
    {
        EditorFontSize = Math.Clamp(
            EditorFontSize + steps,
            MinimumEditorFontSize,
            MaximumEditorFontSize);
    }

    public void CheckSelectedDocumentForExternalChanges() =>
        SelectedDocument?.CheckForExternalChanges();

    public async Task CloseDocumentAsync(DocumentTabViewModel? document)
    {
        if (document is null || !Documents.Contains(document) || !await CanCloseDocumentAsync(document))
        {
            return;
        }

        var index = Documents.IndexOf(document);
        if (ReferenceEquals(SelectedDocument, document))
        {
            if (Documents.Count > 1)
            {
                SelectedDocument = Documents[index == Documents.Count - 1 ? index - 1 : index + 1];
            }
            else
            {
                AddDocument(new TemplateDocument(), resetUntitledOrdinal: true);
            }
        }

        Documents.Remove(document);
        document.PropertyChanged -= OnDocumentPropertyChanged;
        document.Dispose();

        NotifyDocumentStateChanged();
    }

    public async Task<bool> CanCloseAsync()
    {
        foreach (var document in Documents.ToList())
        {
            if (!document.IsDirty)
            {
                continue;
            }

            SelectedDocument = document;
            if (!await CanCloseDocumentAsync(document))
            {
                return false;
            }
        }

        return true;
    }

    public void Dispose()
    {
        foreach (var document in Documents)
        {
            document.PropertyChanged -= OnDocumentPropertyChanged;
            document.Dispose();
        }
    }

    [ReactiveCommand]
    private void New() => AddNewDocument();

    [ReactiveCommand]
    private void NewWindow() => _applicationService.OpenNewWindow();

    [ReactiveCommand]
    private async Task ExitAsync() => await _applicationService.ExitAsync();

    [ReactiveCommand]
    private async Task OpenAsync()
    {
        var file = await _storageService.OpenAsync();
        if (file is null)
        {
            return;
        }

        try
        {
            var openDocument = FindOpenDocument(file.Identity);
            if (openDocument is not null)
            {
                SelectedDocument = openDocument;
                CheckSelectedDocumentForExternalChanges();
                return;
            }

            await using var stream = await file.OpenReadAsync();
            var document = await _documentService.LoadAsync(stream);
            if (Documents.Count == 1 && Documents[0].IsEmptyUntitled())
            {
                ReplaceDocument(Documents[0], document, file);
            }
            else
            {
                AddDocument(document, file);
            }
        }
        catch (Exception exception)
        {
            await _interactionService.ShowMessageAsync("Could not open document", exception.Message);
        }
    }

    [ReactiveCommand]
    private async Task SaveAsync()
    {
        if (SelectedDocument is not null)
        {
            await SaveDocumentAsync(SelectedDocument, false);
        }
    }

    [ReactiveCommand]
    private async Task SaveAsAsync()
    {
        if (SelectedDocument is not null)
        {
            await SaveDocumentAsync(SelectedDocument, true);
        }
    }

    [ReactiveCommand]
    private async Task CloseTabAsync(DocumentTabViewModel? document) => await CloseDocumentAsync(document);

    [ReactiveCommand(CanExecute = nameof(_canEditDocument))]
    private void InsertTemplate() => Editor.InsertTemplate();

    [ReactiveCommand(CanExecute = nameof(_canEditDocument))]
    private void Undo() => Editor.Undo();

    [ReactiveCommand(CanExecute = nameof(_canEditDocument))]
    private void Redo() => Editor.Redo();

    [ReactiveCommand(CanExecute = nameof(_canEditDocument))]
    private async Task CutAsync() => await Editor.CutAsync();

    [ReactiveCommand]
    private async Task CopyAsync() => await Editor.CopyAsync();

    [ReactiveCommand(CanExecute = nameof(_canEditDocument))]
    private async Task PasteAsync() => await Editor.PasteAsync();

    [ReactiveCommand(CanExecute = nameof(_canEditDocument))]
    private void Delete() => Editor.Delete();

    [ReactiveCommand]
    private void SelectAll() => Editor.SelectAll();

    [ReactiveCommand]
    private void Find() => Editor.Find();

    [ReactiveCommand]
    private void Replace() => Editor.Replace();

    [ReactiveCommand]
    private async Task FontAsync()
    {
        var selectedFont = await _interactionService.SelectFontAsync(EditorFontFamily);
        if (selectedFont is not null)
        {
            EditorFontFamily = selectedFont;
        }
    }

    [ReactiveCommand]
    private void ZoomIn() => AdjustEditorZoom(1);

    [ReactiveCommand]
    private void ZoomOut() => AdjustEditorZoom(-1);

    [ReactiveCommand]
    private void RestoreDefaultZoom() => EditorFontSize = DefaultEditorFontSize;

    [ReactiveCommand]
    private void ToggleStatusBar() => IsStatusBarVisible = !IsStatusBarVisible;

    [ReactiveCommand]
    private void ToggleWordWrap() => IsWordWrapEnabled = !IsWordWrapEnabled;

    [ReactiveCommand]
    private void ToggleCurrentLineHighlight() =>
        IsCurrentLineHighlightEnabled = !IsCurrentLineHighlightEnabled;

    [ReactiveCommand(CanExecute = nameof(_canEditDocument))]
    private void ToggleLineLengthIndicators() =>
        Editor.ShowLineLengthIndicators = !Editor.ShowLineLengthIndicators;

    [ReactiveCommand(CanExecute = nameof(_canEditDocument))]
    private void ToggleHardLineLengthLimit() =>
        Editor.EnforceHardLineLengthLimit = !Editor.EnforceHardLineLengthLimit;

    [ReactiveCommand]
    private async Task TemplateHelpAsync() => await _interactionService.ShowTemplateHelpAsync();

    [ReactiveCommand]
    private async Task AboutAsync() => await _interactionService.ShowAboutAsync();

    [ReactiveCommand]
    private async Task ReloadExternalChangesAsync(DocumentTabViewModel? document)
    {
        if (document is null || !Documents.Contains(document) ||
            !document.HasExternalChanges || document.File is null)
        {
            return;
        }

        try
        {
            await using var stream = await document.File.OpenReadAsync();
            var reloadedDocument = await _documentService.LoadAsync(stream);
            document.Reload(reloadedDocument);
        }
        catch (Exception exception)
        {
            await _interactionService.ShowMessageAsync("Could not reload document", exception.Message);
        }
    }

    [ReactiveCommand]
    private void IgnoreExternalChanges(DocumentTabViewModel? document)
    {
        if (document is not null && Documents.Contains(document))
        {
            document.IgnoreExternalChanges();
        }
    }

    private void AddDocument(
        TemplateDocument document,
        IDocumentFile? file = null,
        bool resetUntitledOrdinal = false)
    {
        var untitledOrdinal = file is null && !resetUntitledOrdinal
            ? NextUntitledOrdinal()
            : 1;
        var tab = new DocumentTabViewModel(
            document,
            CloseDocumentAsync,
            file,
            untitledOrdinal);
        tab.PropertyChanged += OnDocumentPropertyChanged;
        Documents.Add(tab);
        SelectedDocument = tab;
        NotifyDocumentStateChanged();
    }

    private void ReplaceDocument(
        DocumentTabViewModel existingTab,
        TemplateDocument document,
        IDocumentFile file)
    {
        var replacement = new DocumentTabViewModel(
            document,
            CloseDocumentAsync,
            file,
            documentId: existingTab.DocumentId);
        replacement.PropertyChanged += OnDocumentPropertyChanged;

        var index = Documents.IndexOf(existingTab);
        Documents[index] = replacement;
        SelectedDocument = replacement;

        existingTab.PropertyChanged -= OnDocumentPropertyChanged;
        existingTab.Dispose();
        NotifyDocumentStateChanged();
    }

    private async Task<bool> SaveDocumentAsync(DocumentTabViewModel document, bool chooseFile)
    {
        var file = document.File;
        if (chooseFile || file is null)
        {
            file = await _storageService.SaveAsAsync(file?.Name ?? "document.itd");
        }

        if (file is null)
        {
            return false;
        }

        try
        {
            await using (var stream = await file.OpenWriteAsync())
            {
                await _documentService.SaveAsync(stream, document.Editor.ExportDocument());
            }

            document.MarkSaved(file);
            return true;
        }
        catch (Exception exception)
        {
            await _interactionService.ShowMessageAsync("Could not save document", exception.Message);
            return false;
        }
    }

    private async Task<bool> CanCloseDocumentAsync(DocumentTabViewModel document)
    {
        if (!document.IsDirty)
        {
            return true;
        }

        SelectedDocument = document;
        return await _interactionService.ConfirmUnsavedChangesAsync() switch
        {
            UnsavedChoice.Discard => true,
            UnsavedChoice.Save => await SaveDocumentAsync(document, false),
            _ => false
        };
    }

    private DocumentTabViewModel? FindOpenDocument(string identity) =>
        Documents.FirstOrDefault(document => DocumentFileIdentity.Matches(document.File, identity));

    private DocumentTabViewModel? FindMatchingDocument(Guid documentId, IDocumentFile? file) =>
        Documents.FirstOrDefault(document =>
            document.DocumentId == documentId ||
            (file is not null && DocumentFileIdentity.Matches(document.File, file.Identity)));

    private int NextUntitledOrdinal() =>
        Documents
            .Where(document => document.File is null)
            .Select(document => document.UntitledOrdinal)
            .DefaultIfEmpty(0)
            .Max() + 1;

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DocumentTabViewModel.IsDirty) or nameof(DocumentTabViewModel.Header))
        {
            NotifyDocumentStateChanged();
        }
    }

    private void NotifyDocumentStateChanged()
    {
        this.RaisePropertyChanged(nameof(IsDirty));
        UpdateTitle();
    }

    private void UpdateTitle()
    {
        var name = SelectedDocument?.Header ?? "Inlay";
        Title = $"{name} - Inlay";
    }

}
