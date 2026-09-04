using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Media;
using Inlay.Models;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Inlay.ViewModels;

internal sealed partial class MainWindowViewModel : ReactiveObject
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

    [ReactiveCommand]
    private void InsertTemplate() => Editor?.InsertTemplate();

    [ReactiveCommand]
    private void Undo() => Editor?.Undo();

    [ReactiveCommand]
    private void Redo() => Editor?.Redo();

    [ReactiveCommand]
    private void Cut() => Editor.Cut();

    [ReactiveCommand]
    private void Copy() => Editor.Copy();

    [ReactiveCommand]
    private void Paste() => Editor.Paste();

    [ReactiveCommand]
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

    [ReactiveCommand]
    private void ToggleLineLengthIndicators() =>
        Editor.ShowLineLengthIndicators = !Editor.ShowLineLengthIndicators;

    [ReactiveCommand]
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
        var untitledOrdinal = file is null
            ? resetUntitledOrdinal
                ? 1
                : Documents
                    .Where(documentTab => documentTab.File is null)
                    .Select(documentTab => documentTab.UntitledOrdinal)
                    .DefaultIfEmpty(0)
                    .Max() + 1
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
        var replacement = new DocumentTabViewModel(document, CloseDocumentAsync, file);
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
        Documents.FirstOrDefault(document =>
            document.File is not null && FileIdentityComparer.Equals(document.File.Identity, identity));

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

    private static StringComparer FileIdentityComparer { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

}
