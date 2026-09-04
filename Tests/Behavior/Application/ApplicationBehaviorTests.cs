using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Inlay.ViewModels;
using Xunit;

namespace Inlay.Tests;

public sealed class ApplicationBehaviorTests
{
    [AvaloniaFact]
    public void NewWindowOpensAnotherWindowInTheSameApplication()
    {
        var owner = new Window();
        var newWindow = new Window();
        var service = new AvaloniaApplicationService(owner, () => newWindow);

        service.OpenNewWindow();
        try
        {
            Assert.True(newWindow.IsVisible);
            Assert.False(owner.IsVisible);
        }
        finally
        {
            newWindow.Close();
        }
    }

    [AvaloniaFact]
    public async Task ExitClosesEveryOpenWindow()
    {
        var owner = new Window();
        var otherWindow = new Window();
        var service = new AvaloniaApplicationService(
            owner,
            () => new Window(),
            () => [owner, otherWindow]);
        owner.Show();
        otherWindow.Show();

        try
        {
            await service.ExitAsync();

            Assert.False(owner.IsVisible);
            Assert.False(otherWindow.IsVisible);
        }
        finally
        {
            owner.Close();
            otherWindow.Close();
        }
    }

    [AvaloniaFact]
    public async Task CancelingAnUnsavedPromptKeepsEveryWindowOpen()
    {
        var firstInteraction = new FakeInteractionService
        {
            UnsavedChoice = UnsavedChoice.Discard
        };
        var secondInteraction = new FakeInteractionService
        {
            UnsavedChoice = UnsavedChoice.Cancel
        };
        var firstWindow = CreateWindow(firstInteraction);
        var secondWindow = CreateWindow(secondInteraction);
        firstWindow.Show();
        secondWindow.Show();
        var firstViewModel = Assert.IsType<MainWindowViewModel>(firstWindow.DataContext);
        var secondViewModel = Assert.IsType<MainWindowViewModel>(secondWindow.DataContext);
        firstViewModel.Editor.ReportContentChanged();
        secondViewModel.Editor.ReportContentChanged();
        var service = new AvaloniaApplicationService(
            firstWindow,
            () => new Window(),
            () => [firstWindow, secondWindow]);

        try
        {
            await service.ExitAsync();

            Assert.True(firstWindow.IsVisible);
            Assert.True(secondWindow.IsVisible);
            Assert.Equal(1, firstInteraction.UnsavedConfirmationCount);
            Assert.Equal(1, secondInteraction.UnsavedConfirmationCount);
        }
        finally
        {
            firstWindow.ApproveApplicationExit();
            secondWindow.ApproveApplicationExit();
            firstWindow.Close();
            secondWindow.Close();
        }
    }

    [AvaloniaFact]
    public void NewWindowsHaveIndependentDocumentState()
    {
        var factory = new MainWindowFactory(new JsonTemplateDocumentService());
        var firstWindow = factory.Create();
        var secondWindow = factory.Create();
        try
        {
            var first = Assert.IsType<MainWindowViewModel>(firstWindow.DataContext);
            var second = Assert.IsType<MainWindowViewModel>(secondWindow.DataContext);

            TestCommand.Execute(first.NewCommand.Execute());

            Assert.NotSame(first, second);
            Assert.Equal(2, first.Documents.Count);
            Assert.Single(second.Documents);
        }
        finally
        {
            firstWindow.Close();
            secondWindow.Close();
        }
    }

    private static MainWindow CreateWindow(FakeInteractionService interaction) =>
        new(new MainWindowViewModel(
            new JsonTemplateDocumentService(),
            new FakeStorageService(),
            interaction,
            new FakeApplicationService()));
}
