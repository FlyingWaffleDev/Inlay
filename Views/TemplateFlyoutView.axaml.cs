using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Inlay.ViewModels;

namespace Inlay;

internal sealed partial class TemplateFlyoutView : UserControl
{
    public TemplateFlyoutView()
    {
        InitializeComponent();
    }

    private void RemoveOptionClicked(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (DataContext is TemplateFlyoutViewModel viewModel &&
            sender is Button { DataContext: string choice })
        {
            viewModel.RemoveChoice(choice);
        }
    }

    private void NewChoiceKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is TemplateFlyoutViewModel viewModel &&
            viewModel.CanAddChoice)
        {
            viewModel.AddChoice();
            NewChoiceInput.Focus();
            e.Handled = true;
        }
    }

    private void AddChoiceClicked(object? sender, RoutedEventArgs e) =>
        Dispatcher.UIThread.Post(() => NewChoiceInput.Focus());
}
