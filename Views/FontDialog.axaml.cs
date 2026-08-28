using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Inlay.ViewModels;

namespace Inlay;

internal sealed partial class FontDialog : Window
{
    private readonly Task<IReadOnlyList<FontFamily>> _fonts;
    private readonly FontDialogViewModel _viewModel;

    public FontDialog(
        Task<IReadOnlyList<FontFamily>> fonts,
        FontFamily currentFontFamily)
    {
        _fonts = fonts;
        InitializeComponent();
        _viewModel = new FontDialogViewModel(currentFontFamily, Close);
        DataContext = _viewModel;
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        try
        {
            _viewModel.LoadFonts(await _fonts.ConfigureAwait(true));
            Dispatcher.UIThread.Post(SelectCurrentFont, DispatcherPriority.Loaded);
        }
        catch (Exception)
        {
            _viewModel.ReportLoadFailure();
        }
    }

    private void SelectCurrentFont()
    {
        _viewModel.SelectCurrentFont();
        if (_viewModel.SelectedFont is not { } selectedFont)
        {
            return;
        }

        FontList.SelectedItem = selectedFont;
        FontList.ScrollIntoView(selectedFont);
    }
}
