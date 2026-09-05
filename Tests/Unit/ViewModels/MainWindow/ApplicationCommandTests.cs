using Xunit;

namespace Inlay.Tests;

public sealed class ApplicationCommandTests
{
    [Fact]
    public void CommandsReachTheApplicationService()
    {
        using var context = MainWindowViewModelTestContext.Create();

        TestCommand.Execute(context.ViewModel.NewWindowCommand.Execute());
        TestCommand.Execute(context.ViewModel.ExitCommand.Execute());

        Assert.Equal(1, context.Application.OpenNewWindowCount);
        Assert.Equal(1, context.Application.ExitCount);
    }

    [Fact]
    public async Task CancellingFontDialogKeepsTheCurrentFont()
    {
        using var context = MainWindowViewModelTestContext.Create();
        var initialFont = context.ViewModel.EditorFontFamily;

        await MainWindowViewModelTestContext.ObserveAsync(
            context.ViewModel.FontCommand.Execute(),
            TestContext.Current.CancellationToken);

        Assert.Equal(initialFont, context.ViewModel.EditorFontFamily);
    }
}
