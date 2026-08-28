using System.Collections.ObjectModel;
using Avalonia.Media;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace Inlay.ViewModels;

internal sealed partial class UnsavedChangesDialogViewModel(Action<UnsavedChoice> close)
{
    [ReactiveCommand]
    private void Save() => close(UnsavedChoice.Save);

    [ReactiveCommand]
    private void Discard() => close(UnsavedChoice.Discard);

    [ReactiveCommand]
    private void Cancel() => close(UnsavedChoice.Cancel);
}

internal sealed partial class MessageDialogViewModel(string title, string message, Action close)
{
    public string Title { get; } = title;
    public string Message { get; } = message;

    [ReactiveCommand]
    private void Close() => close();
}

internal sealed partial class CloseDialogViewModel(Action close)
{
    [ReactiveCommand]
    private void Close() => close();
}

internal sealed record FontOption(FontFamily Family)
{
    public string Name => Family.Name;
}

internal sealed partial class FontDialogViewModel : ReactiveObject
{
    private readonly FontFamily _currentFontFamily;
    private readonly Action<FontFamily?> _close;

    public FontDialogViewModel(
        FontFamily currentFontFamily,
        Action<FontFamily?> close)
    {
        _currentFontFamily = currentFontFamily;
        _close = close;
    }

    public ObservableCollection<FontOption> Fonts { get; } = [];

    private FontOption? _selectedFont;

    public FontOption? SelectedFont
    {
        get => _selectedFont;
        set
        {
            if (ReferenceEquals(_selectedFont, value))
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _selectedFont, value);
            this.RaisePropertyChanged(nameof(PreviewFontFamily));
        }
    }

    [Reactive]
    private bool _isLoading = true;

    public bool HasFonts => Fonts.Count > 0;
    public bool HasStatusMessage => StatusMessage.Length > 0;
    public FontFamily PreviewFontFamily => SelectedFont?.Family ?? _currentFontFamily;

    private string _statusMessage = "Finding installed fonts...";

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            this.RaiseAndSetIfChanged(ref _statusMessage, value);
            this.RaisePropertyChanged(nameof(HasStatusMessage));
        }
    }

    public void LoadFonts(IReadOnlyList<FontFamily> fonts)
    {
        foreach (var font in fonts)
        {
            Fonts.Add(new FontOption(font));
        }

        SelectCurrentFont();
        IsLoading = false;
        this.RaisePropertyChanged(nameof(HasFonts));
        StatusMessage = HasFonts ? string.Empty : "No monospace fonts found.";
    }

    public void ReportLoadFailure()
    {
        IsLoading = false;
        StatusMessage = "Could not load installed fonts.";
        this.RaisePropertyChanged(nameof(HasFonts));
    }

    public void SelectCurrentFont() =>
        SelectedFont = FindCurrentFont(Fonts, _currentFontFamily) ??
            (Fonts.Count > 0 ? Fonts[0] : null);

    [ReactiveCommand]
    private void Confirm()
    {
        if (SelectedFont is not null)
        {
            _close(SelectedFont.Family);
        }
    }

    [ReactiveCommand]
    private void Cancel() => _close(null);

    private static FontOption? FindCurrentFont(
        IReadOnlyList<FontOption> fonts,
        FontFamily currentFontFamily)
    {
        foreach (var candidate in currentFontFamily.ToString().Split(','))
        {
            var match = fonts.FirstOrDefault(font => string.Equals(
                font.Name,
                candidate.Trim(),
                StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        if (FontManager.Current.TryGetGlyphTypeface(
                new Typeface(currentFontFamily),
                out var currentTypeface))
        {
            return fonts.FirstOrDefault(font =>
                string.Equals(
                    font.Name,
                    currentTypeface.FamilyName,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    font.Name,
                    currentTypeface.TypographicFamilyName,
                    StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }
}
