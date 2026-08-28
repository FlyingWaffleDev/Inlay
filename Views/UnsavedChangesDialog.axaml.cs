using Avalonia.Controls;
using Inlay.ViewModels;

namespace Inlay;

internal sealed partial class UnsavedChangesDialog : Window
{
    public UnsavedChangesDialog()
    {
        InitializeComponent();
        DataContext = new UnsavedChangesDialogViewModel(choice => Close(choice));
    }
}
