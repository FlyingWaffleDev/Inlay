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
    public void ReorderingAChoiceUsesDropSlotsAndKeepsTheSelectedChoice()
    {
        var selected = string.Empty;
        var options = new ObservableCollection<string>(["One", "Two", "Three"]);
        var viewModel = new TemplateFlyoutViewModel(
            options,
            1,
            value => selected = value,
            () => { });

        viewModel.ReorderChoice("One", 3);

        Assert.Equal(["Two", "Three", "One"], options);
        Assert.Equal("Two", viewModel.SelectedChoice);
        Assert.Equal(0, viewModel.SelectedIndex);
        Assert.Equal("Two", selected);
    }

    [Fact]
    public void ReorderingToAnAdjacentSlotLeavesTheOrderAlone()
    {
        var options = new ObservableCollection<string>(["One", "Two", "Three"]);
        var viewModel = new TemplateFlyoutViewModel(options, 1, _ => { }, () => { });

        viewModel.ReorderChoice("Two", 2);

        Assert.Equal(["One", "Two", "Three"], options);
        Assert.Equal("Two", viewModel.SelectedChoice);
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

    [Fact]
    public void DisconnectStopsObservingTheDocumentOptions()
    {
        var selections = 0;
        var options = new ObservableCollection<string>(["One", "Two"]);
        var viewModel = new TemplateFlyoutViewModel(
            options,
            1,
            _ => selections++,
            () => { });

        viewModel.Disconnect();
        options.RemoveAt(1);

        Assert.Equal(0, selections);
    }
}
