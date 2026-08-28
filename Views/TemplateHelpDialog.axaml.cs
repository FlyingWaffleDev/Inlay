using Avalonia.Controls;
using Inlay.ViewModels;

namespace Inlay;

internal sealed partial class TemplateHelpDialog : Window
{
    public TemplateHelpDialog()
    {
        InitializeComponent();
        DataContext = new CloseDialogViewModel(Close);
    }
}
