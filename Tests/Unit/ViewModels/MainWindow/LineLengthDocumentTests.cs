using Xunit;

namespace Inlay.Tests;

public sealed class LineLengthDocumentTests
{
    [Fact]
    public void EachDocumentKeepsItsOwnLineLengthSettings()
    {
        var context = MainWindowViewModelTestContext.Create();
        var first = context.ViewModel.SelectedDocument!;
        first.Editor.ShowLineLengthIndicators = true;
        first.Editor.EnforceHardLineLengthLimit = true;
        first.Editor.SoftLineLengthLimit = 72;
        first.Editor.HardLineLengthLimit = 96;

        context.ViewModel.AddNewDocument();

        Assert.False(context.ViewModel.Editor.ShowLineLengthIndicators);
        Assert.False(context.ViewModel.Editor.EnforceHardLineLengthLimit);
        Assert.Equal(80, context.ViewModel.Editor.SoftLineLengthLimit);
        Assert.Equal(120, context.ViewModel.Editor.HardLineLengthLimit);

        context.ViewModel.SelectedDocument = first;
        Assert.True(context.ViewModel.Editor.ShowLineLengthIndicators);
        Assert.True(context.ViewModel.Editor.EnforceHardLineLengthLimit);
        Assert.Equal(72, context.ViewModel.Editor.SoftLineLengthLimit);
        Assert.Equal(96, context.ViewModel.Editor.HardLineLengthLimit);
    }
}
