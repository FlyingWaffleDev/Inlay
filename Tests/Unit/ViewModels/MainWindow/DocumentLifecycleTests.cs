using Xunit;

namespace Inlay.Tests;

public sealed class DocumentLifecycleTests
{
    [Fact]
    public async Task NewCreatesAndSelectsAnotherDocumentTab()
    {
        using var context = MainWindowViewModelTestContext.Create();
        var original = context.ViewModel.SelectedDocument;

        await MainWindowViewModelTestContext.ObserveAsync(
            context.ViewModel.NewCommand.Execute(),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, context.ViewModel.Documents.Count);
        Assert.NotSame(original, context.ViewModel.SelectedDocument);
        Assert.Equal("Untitled", context.ViewModel.Documents[0].Header);
        Assert.Equal("Untitled (2)", context.ViewModel.Documents[1].Header);
        Assert.Equal("Untitled (2) - Inlay", context.ViewModel.Title);
    }

    [Fact]
    public async Task UntitledNumbersRemainAssignedAndInteriorGapsAreNotReused()
    {
        using var context = MainWindowViewModelTestContext.Create();
        context.ViewModel.AddNewDocument();
        context.ViewModel.AddNewDocument();
        var third = context.ViewModel.SelectedDocument!;
        context.ViewModel.AddNewDocument();

        await context.ViewModel.CloseDocumentAsync(context.ViewModel.Documents[0]);
        await context.ViewModel.CloseDocumentAsync(third);

        Assert.Equal(
            ["Untitled (2)", "Untitled (4)"],
            context.ViewModel.Documents.Select(document => document.Header));

        context.ViewModel.AddNewDocument();

        Assert.Equal(
            ["Untitled (2)", "Untitled (4)", "Untitled (5)"],
            context.ViewModel.Documents.Select(document => document.Header));
    }

    [Fact]
    public async Task ClosingTheHighestUntitledNumberMakesItAvailableAgain()
    {
        using var context = MainWindowViewModelTestContext.Create();
        context.ViewModel.AddNewDocument();
        context.ViewModel.AddNewDocument();
        context.ViewModel.AddNewDocument();

        await context.ViewModel.CloseDocumentAsync(context.ViewModel.SelectedDocument);
        context.ViewModel.AddNewDocument();

        Assert.Equal("Untitled (4)", context.ViewModel.SelectedDocument!.Header);
    }

    [Fact]
    public async Task SavingTheHighestUntitledNumberMakesItAvailableAgain()
    {
        using var context = MainWindowViewModelTestContext.Create();
        context.ViewModel.AddNewDocument();
        context.Storage.SaveFile = new MemoryDocumentFile("named.itd");

        await MainWindowViewModelTestContext.ObserveAsync(
            context.ViewModel.SaveCommand.Execute(),
            TestContext.Current.CancellationToken);
        context.ViewModel.AddNewDocument();

        Assert.Equal("named.itd", context.ViewModel.Documents[1].Header);
        Assert.Equal("Untitled (2)", context.ViewModel.SelectedDocument!.Header);
    }
}
