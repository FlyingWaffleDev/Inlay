using Avalonia.Media;

namespace Inlay;

internal interface IDocumentFile
{
    string Name { get; }
    string Identity { get; }
    DocumentFileVersion? GetVersion();
    Task<Stream> OpenReadAsync();
    Task<Stream> OpenWriteAsync();
}

internal readonly record struct DocumentFileVersion(bool Exists, long Length, DateTime LastWriteTimeUtc);

internal interface IDocumentStorageService
{
    Task<IDocumentFile?> OpenAsync();
    Task<IDocumentFile?> SaveAsAsync(string suggestedName);
}

internal interface IUserInteractionService
{
    Task<UnsavedChoice> ConfirmUnsavedChangesAsync();
    Task<FontFamily?> SelectFontAsync(FontFamily currentFontFamily);
    Task ShowMessageAsync(string title, string message);
    Task ShowTemplateHelpAsync();
    Task ShowAboutAsync();
}
