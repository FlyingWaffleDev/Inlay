using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace Inlay;

internal sealed class AvaloniaDocumentStorageService(Window owner) : IDocumentStorageService
{
    private static readonly FilePickerFileType TemplateDocumentFileType = new("Template document")
    {
        Patterns = ["*.itd"],
        AppleUniformTypeIdentifiers = ["public.json"],
        MimeTypes = ["application/vnd.inlay+json"]
    };

    public async Task<IDocumentFile?> OpenAsync()
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Inlay template document",
            AllowMultiple = false,
            FileTypeFilter = [TemplateDocumentFileType]
        });
        return files.Count > 0 ? new AvaloniaDocumentFile(files[0]) : null;
    }

    public async Task<IDocumentFile?> SaveAsAsync(string suggestedName)
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Inlay template document",
            SuggestedFileName = suggestedName,
            DefaultExtension = "itd",
            FileTypeChoices = [TemplateDocumentFileType],
            ShowOverwritePrompt = true
        });
        return file is null ? null : new AvaloniaDocumentFile(file);
    }

    private sealed class AvaloniaDocumentFile(IStorageFile file) : IDocumentFile
    {
        public string Name => file.Name;
        public string Identity => file.Path.IsFile
            ? Path.GetFullPath(file.Path.LocalPath)
            : file.Path.AbsoluteUri;
        public DocumentFileVersion? GetVersion()
        {
            if (!file.Path.IsFile)
            {
                return null;
            }

            try
            {
                var info = new FileInfo(file.Path.LocalPath);
                return info.Exists
                    ? new DocumentFileVersion(true, info.Length, info.LastWriteTimeUtc)
                    : new DocumentFileVersion(false, 0, default);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }
        public async Task<Stream> OpenReadAsync() => await file.OpenReadAsync();
        public async Task<Stream> OpenWriteAsync() => await file.OpenWriteAsync();
    }
}

internal sealed class AvaloniaUserInteractionService(Window owner) : IUserInteractionService
{
    public async Task<UnsavedChoice> ConfirmUnsavedChangesAsync() =>
        await new UnsavedChangesDialog().ShowDialog<UnsavedChoice>(owner);

    public async Task<FontFamily?> SelectFontAsync(FontFamily currentFontFamily) =>
        await new FontDialog(
            SystemMonospaceFonts.GetAvailableFamiliesAsync(),
            currentFontFamily).ShowDialog<FontFamily?>(owner);

    public async Task ShowMessageAsync(string title, string message) =>
        await new MessageDialog(title, message).ShowDialog(owner);

    public async Task ShowTemplateHelpAsync() =>
        await new TemplateHelpDialog().ShowDialog(owner);

    public async Task ShowAboutAsync() =>
        await new AboutDialog().ShowDialog(owner);
}

internal static class SystemMonospaceFonts
{
    private static readonly Lazy<Task<IReadOnlyList<FontFamily>>> AvailableFamilies =
        new(() => Task.Run(FindAvailableFamilies));

    public static Task<IReadOnlyList<FontFamily>> GetAvailableFamiliesAsync() =>
        AvailableFamilies.Value;

    public static void WarmUp() => _ = AvailableFamilies.Value;

    private static IReadOnlyList<FontFamily> FindAvailableFamilies()
    {
        var fontManager = FontManager.Current;
        return fontManager.SystemFonts
            .Where(fontFamily =>
                fontManager.TryGetGlyphTypeface(new Typeface(fontFamily), out var glyphTypeface) &&
                glyphTypeface.Metrics.IsFixedPitch)
            .DistinctBy(fontFamily => fontFamily.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(fontFamily => fontFamily.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
