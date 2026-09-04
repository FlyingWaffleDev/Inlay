using System.Windows.Input;
using Xunit;

namespace Inlay.Tests;

public sealed class ExternalChangeTests
{
    [Fact]
    public async Task ExternalChangesDisableEditCommands()
    {
        var context = await OpenDocument();
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
        var context = await OpenDocument();
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
        var context = await OpenDocument();
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
        var context = await OpenDocument();
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
        var context = await OpenDocument();
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
        MemoryDocumentFile File);
}
