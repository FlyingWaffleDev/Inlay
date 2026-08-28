using Avalonia.Controls;
using Inlay.ViewModels;

namespace Inlay;

internal sealed partial class MessageDialog : Window
{
    public MessageDialog(string title, string message)
    {
        InitializeComponent();
        DataContext = new MessageDialogViewModel(title, message, Close);
    }
}
