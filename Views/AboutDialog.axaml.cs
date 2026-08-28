using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Svg.Skia;
using Inlay.ViewModels;

namespace Inlay;

internal sealed partial class AboutDialog : Window
{
    internal string LogoPath { get; private set; } = string.Empty;

    public AboutDialog()
    {
        InitializeComponent();
        DataContext = new CloseDialogViewModel(Close);
        UpdateLogo();
        ActualThemeVariantChanged += (_, _) => UpdateLogo();
    }

    private void UpdateLogo()
    {
        var iconName = ActualThemeVariant == ThemeVariant.Dark
            ? "inlay-icon-dark.svg"
            : "inlay-icon-light.svg";
        LogoPath = $"avares://Inlay/Assets/{iconName}";
        Logo.Source = new SvgImage { Source = SvgSource.Load(LogoPath, null) };
    }
}
