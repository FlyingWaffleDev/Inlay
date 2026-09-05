using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using Inlay.Models;
using Xunit;

namespace Inlay.Tests;

public sealed class ExternalChangeTests
{
    [Fact]
    public async Task ExternalChangesDisableEditCommands()
    {
        using var context = await OpenDocument();
        var viewModel = context.Test.ViewModel;
        var editingCommands = new (string Name, ICommand Command)[]
        {
            ("InsertTemplate", viewModel.InsertTemplateCommand),
            ("Undo", viewModel.UndoCommand),
            ("Redo", viewModel.RedoCommand),
            ("Cut", viewModel.CutCommand),
            ("Paste", viewModel.PasteCommand),
            ("Delete", viewModel.DeleteCommand),
            ("ToggleLineLengthIndicators", viewModel.ToggleLineLengthIndicatorsCommand),
            ("ToggleHardLineLengthLimit", viewModel.ToggleHardLineLengthLimitCommand)
        };

        Assert.All(editingCommands, entry => Assert.True(entry.Command.CanExecute(null), entry.Name));

        context.File.SetDocumentText("Changed elsewhere");
        viewModel.CheckSelectedDocumentForExternalChanges();

        Assert.False(viewModel.SelectedDocument!.CanEdit);
        Assert.All(editingCommands, entry => Assert.False(entry.Command.CanExecute(null), entry.Name));

        Assert.True(((ICommand)viewModel.CopyCommand).CanExecute(null));
        Assert.True(((ICommand)viewModel.SaveCommand).CanExecute(null));
        Assert.True(((ICommand)viewModel.ReloadExternalChangesCommand)
            .CanExecute(viewModel.SelectedDocument));
        Assert.True(((ICommand)viewModel.IgnoreExternalChangesCommand)
            .CanExecute(viewModel.SelectedDocument));

        await MainWindowViewModelTestContext.ObserveAsync(
            viewModel.IgnoreExternalChangesCommand.Execute(viewModel.SelectedDocument),
            TestContext.Current.CancellationToken);

        Assert.All(editingCommands, entry => Assert.True(entry.Command.CanExecute(null), entry.Name));
    }

    [Fact]
    public async Task FocusCheckShowsInlineExternalChangeChoiceAndMarksDocumentDirty()
    {
        using var context = await OpenDocument();
        context.File.SetDocumentText("Changed elsewhere");

        Assert.False(context.Test.ViewModel.SelectedDocument!.IsDirty);

        context.Test.ViewModel.CheckSelectedDocumentForExternalChanges();

        Assert.True(context.Test.ViewModel.SelectedDocument.IsDirty);
        Assert.True(context.Test.ViewModel.SelectedDocument.HasExternalChanges);
        Assert.Equal("opened.itd *", context.Test.ViewModel.SelectedDocument.Header);

        await MainWindowViewModelTestContext.ObserveAsync(
            context.Test.ViewModel.IgnoreExternalChangesCommand.Execute(
                context.Test.ViewModel.SelectedDocument),
            TestContext.Current.CancellationToken);

        Assert.True(context.Test.ViewModel.SelectedDocument.IsDirty);
        Assert.False(context.Test.ViewModel.SelectedDocument.HasExternalChanges);
    }

    [Fact]
    public async Task ReloadingAnExternalChangeReplacesContentAndClearsDirtyState()
    {
        using var context = await OpenDocument();
        var editor = new FakeEditorAdapter();
        context.Test.ViewModel.SelectedDocument!.Editor.Attach(editor);
        context.File.SetDocumentText("Changed elsewhere");
        context.Test.ViewModel.CheckSelectedDocumentForExternalChanges();

        await MainWindowViewModelTestContext.ObserveAsync(
            context.Test.ViewModel.ReloadExternalChangesCommand.Execute(
                context.Test.ViewModel.SelectedDocument),
            TestContext.Current.CancellationToken);

        Assert.False(context.Test.ViewModel.SelectedDocument.IsDirty);
        Assert.Equal("Changed elsewhere", Assert.Single(editor.Document.Content).Text);
    }

    [Fact]
    public async Task SelectingATabChecksThatFileForExternalChanges()
    {
        using var context = await OpenDocument();
        var fileTab = context.Test.ViewModel.SelectedDocument!;
        context.Test.ViewModel.AddNewDocument();
        context.File.SetDocumentText("Changed elsewhere");

        Assert.False(fileTab.IsDirty);

        context.Test.ViewModel.SelectedDocument = fileTab;

        Assert.True(fileTab.IsDirty);
        Assert.True(fileTab.HasExternalChanges);
        Assert.False(fileTab.CanEdit);
    }

    [Fact]
    public async Task ReloadFailurePreservesTheExternalChangeWarning()
    {
        using var context = await OpenDocument();
        context.File.SetRawContents("{not valid json");
        context.Test.ViewModel.CheckSelectedDocumentForExternalChanges();

        await MainWindowViewModelTestContext.ObserveAsync(
            context.Test.ViewModel.ReloadExternalChangesCommand.Execute(
                context.Test.ViewModel.SelectedDocument),
            TestContext.Current.CancellationToken);

        Assert.True(context.Test.ViewModel.SelectedDocument!.IsDirty);
        Assert.True(context.Test.ViewModel.SelectedDocument.HasExternalChanges);
        Assert.False(context.Test.ViewModel.SelectedDocument.CanEdit);
        var message = Assert.Single(context.Test.Interaction.Messages);
        Assert.Equal("Could not reload document", message.Title);
        Assert.NotEmpty(message.Message);
    }

    [Fact]
    public async Task ExternalChangesMessageShowsChangedInAnotherWindowWhenSavedByAnotherTab()
    {
        using var context1 = await OpenDocument();
        var vm1 = context1.Test.ViewModel;

        using var context2 = MainWindowViewModelTestContext.Create();
        var vm2 = context2.ViewModel;

        var copiedTab = vm2.CopyDocument(vm1.SelectedDocument!);
        Assert.NotNull(copiedTab);

        vm1.SelectedDocument!.Editor.LoadDocument(new TemplateDocument
        {
            Content = [DocumentPart.PlainText("Updated text from window 1")]
        });
        await MainWindowViewModelTestContext.ObserveAsync(
            vm1.SaveCommand.Execute(),
            TestContext.Current.CancellationToken);

        vm2.CheckSelectedDocumentForExternalChanges();

        Assert.True(vm2.SelectedDocument!.HasExternalChanges);
        Assert.True(vm2.SelectedDocument.ChangedInAnotherWindow);
        Assert.StartsWith("opened.itd was changed in another window.", vm2.SelectedDocument.ExternalChangesMessage, StringComparison.Ordinal);
        Assert.Equal("opened.itd was changed in another window. Reload it or ignore the changes?", vm2.SelectedDocument.ExternalChangesMessage);

        using var context3 = MainWindowViewModelTestContext.Create();
        var copiedWarning = context3.ViewModel.CopyDocument(vm2.SelectedDocument);
        Assert.True(copiedWarning.HasExternalChanges);
        Assert.True(copiedWarning.ChangedInAnotherWindow);
        Assert.False(copiedWarning.CanEdit);

        await MainWindowViewModelTestContext.ObserveAsync(
            vm2.IgnoreExternalChangesCommand.Execute(vm2.SelectedDocument),
            TestContext.Current.CancellationToken);
        Assert.False(vm2.SelectedDocument.HasExternalChanges);
        Assert.False(vm2.SelectedDocument.ChangedInAnotherWindow);
    }

    [Fact]
    public async Task ExternalChangesMessageShowsChangedOutsideInlayWhenSavedExternally()
    {
        using var context = await OpenDocument();
        context.File.SetDocumentText("Direct external change");
        context.Test.ViewModel.CheckSelectedDocumentForExternalChanges();

        Assert.True(context.Test.ViewModel.SelectedDocument!.HasExternalChanges);
        Assert.False(context.Test.ViewModel.SelectedDocument.ChangedInAnotherWindow);
        Assert.StartsWith("opened.itd changed outside Inlay.", context.Test.ViewModel.SelectedDocument.ExternalChangesMessage, StringComparison.Ordinal);
        Assert.Equal("opened.itd changed outside Inlay. Reload it or ignore the changes?", context.Test.ViewModel.SelectedDocument.ExternalChangesMessage);
    }

    [SuppressMessage("Reliability", "CA2000", Justification = "The caller owns the returned object and disposes it.")]
    private static async Task<OpenedDocumentContext> OpenDocument()
    {
        var test = MainWindowViewModelTestContext.Create();
        var file = new MemoryDocumentFile("opened.itd");
        file.SetDocumentText("Original");
        test.Storage.OpenFile = file;

        await MainWindowViewModelTestContext.ObserveAsync(
            test.ViewModel.OpenCommand.Execute(),
            TestContext.Current.CancellationToken);

        return new OpenedDocumentContext(test, file);
    }

    private sealed record OpenedDocumentContext(
        MainWindowViewModelTestContext Test,
        MemoryDocumentFile File) : IDisposable
    {
        public void Dispose() => Test.Dispose();
    }
}
