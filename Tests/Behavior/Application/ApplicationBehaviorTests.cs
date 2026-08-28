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
    public void ExitClosesOnlyTheOwningWindow()
    {
        var owner = new Window();
        var otherWindow = new Window();
        var service = new AvaloniaApplicationService(owner, () => new Window());
        owner.Show();
        otherWindow.Show();

        service.Exit();
        try
        {
            Assert.False(owner.IsVisible);
            Assert.True(otherWindow.IsVisible);
        }
        finally
        {
            otherWindow.Close();
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
}
