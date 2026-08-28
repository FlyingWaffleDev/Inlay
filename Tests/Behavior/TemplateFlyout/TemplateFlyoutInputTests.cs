using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Inlay.ViewModels;
using System.Collections.ObjectModel;
using Xunit;

namespace Inlay.Tests;

public sealed class TemplateFlyoutInputTests
{
    [AvaloniaFact]
    public void AddButtonReturnsFocusToTheChoiceInput()
    {
        var options = new ObservableCollection<string>();
        var viewModel = new TemplateFlyoutViewModel(options, -1, _ => { }, () => { });
        var view = new TemplateFlyoutView { DataContext = viewModel };
        var window = new Window { Content = view };
        window.Show();
        window.UpdateLayout();

        try
        {
            var input = view.FindControl<TextBox>("NewChoiceInput")!;
            var addButton = view.FindControl<Button>("AddChoiceButton")!;
            viewModel.NewChoice = "Added choice";
            addButton.Focus();

            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.None, null);
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(["Added choice"], options);
            Assert.True(input.IsFocused);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void EnterInTheChoiceInputAddsTheChoice()
    {
        var options = new ObservableCollection<string>();
        var viewModel = new TemplateFlyoutViewModel(options, -1, _ => { }, () => { });
        var view = new TemplateFlyoutView { DataContext = viewModel };
        var window = new Window { Content = view };
        window.Show();
        window.UpdateLayout();

        try
        {
            var input = view.FindControl<TextBox>("NewChoiceInput")!;
            viewModel.NewChoice = "Keyboard choice";
            input.Focus();

            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.None, null);
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.None, null);

            Assert.Equal(["Keyboard choice"], options);
            Assert.Empty(viewModel.NewChoice);
            Assert.True(input.IsFocused);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void RemoveButtonRemovesItsChoiceAndHandlesTheClick()
    {
        var options = new ObservableCollection<string> { "Keep", "Remove" };
        var viewModel = new TemplateFlyoutViewModel(options, 0, _ => { }, () => { });
        var view = new TemplateFlyoutView { DataContext = viewModel };
        var window = new Window { Content = view };
        window.Show();
        window.UpdateLayout();

        try
        {
            var removeButton = view.GetVisualDescendants()
                .OfType<Button>()
                .Single(button => Equals(button.DataContext, "Remove"));
            var click = new RoutedEventArgs(Button.ClickEvent);

            removeButton.RaiseEvent(click);

            Assert.True(click.Handled);
            Assert.Equal(["Keep"], options);
            Assert.Equal(0, viewModel.SelectedIndex);
        }
        finally
        {
            window.Close();
        }
    }
}
