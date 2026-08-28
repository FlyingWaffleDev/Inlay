using System.Collections.ObjectModel;
using Inlay.ViewModels;
using Xunit;

namespace Inlay.Tests;

public sealed class TemplateFlyoutViewModelTests
{
    [Fact]
    public void AddChoiceTrimsInputAndRejectsDuplicates()
    {
        var options = new ObservableCollection<string>(["One"]);
        var viewModel = new TemplateFlyoutViewModel(options, 0, _ => { }, () => { });

        viewModel.NewChoice = "  Two  ";
        viewModel.AddChoice();
        viewModel.NewChoice = "Two";
        viewModel.AddChoice();

        Assert.Equal(["One", "Two"], options);
    }

    [Fact]
    public void RemovingChoiceBeforeSelectionKeepsSameSelectedText()
    {
        var selected = string.Empty;
        var options = new ObservableCollection<string>(["One", "Two", "Three"]);
        var viewModel = new TemplateFlyoutViewModel(options, 2, value => selected = value, () => { });

        viewModel.RemoveChoice("One");

        Assert.Equal(1, viewModel.SelectedIndex);
        Assert.Equal("Three", selected);
    }

    [Fact]
    public void RemovingTheSelectedChoiceSelectsThePlaceholder()
    {
        var selected = string.Empty;
        var options = new ObservableCollection<string>(["One", "Two"]);
        var viewModel = new TemplateFlyoutViewModel(
            options,
            1,
            value => selected = value,
            () => { });

        viewModel.RemoveChoice("Two");

        Assert.Equal(-1, viewModel.SelectedIndex);
        Assert.Equal(TemplateTextElement.PlaceholderText, selected);
        Assert.Equal(["One"], options);
    }

    [Fact]
    public void RemoveTemplateCommandInvokesTheRemovalCallback()
    {
        var removeCount = 0;
        var viewModel = new TemplateFlyoutViewModel(
            [],
            -1,
            _ => { },
            () => removeCount++);

        TestCommand.Execute(viewModel.RemoveTemplateCommand.Execute());

        Assert.Equal(1, removeCount);
    }
}
