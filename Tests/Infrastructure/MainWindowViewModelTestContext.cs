using Inlay.Models;
using Inlay.ViewModels;
using System.Text;
using Avalonia.Media;

namespace Inlay.Tests;

internal sealed record MainWindowViewModelTestContext(
    MainWindowViewModel ViewModel,
    FakeStorageService Storage,
    FakeInteractionService Interaction,
    FakeApplicationService Application,
    FakeEditorAdapter Editor)
{
    public static MainWindowViewModelTestContext Create()
    {
        var storage = new FakeStorageService();
        var interaction = new FakeInteractionService();
        var application = new FakeApplicationService();
        var viewModel = new MainWindowViewModel(
            new JsonTemplateDocumentService(),
            storage,
            interaction,
            application);
        var editor = new FakeEditorAdapter();
        viewModel.Editor!.Attach(editor);
        return new MainWindowViewModelTestContext(
            viewModel, storage, interaction, application, editor);
    }

    public static Task ObserveAsync<T>(
        IObservable<T> command,
        CancellationToken cancellationToken) =>
        TestCommand.ExecuteAsync(command, cancellationToken);

}

internal sealed class FakeApplicationService : IApplicationService
{
    public int OpenNewWindowCount { get; private set; }
    public int ExitCount { get; private set; }

    public void OpenNewWindow() => OpenNewWindowCount++;
    public Task ExitAsync()
    {
        ExitCount++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeEditorAdapter : ITemplateEditorAdapter
{
    public TemplateDocument Document { get; set; } = new();
    public int LoadCount { get; private set; }
    public int InsertCount { get; private set; }
    public int UndoCount { get; private set; }
    public int RedoCount { get; private set; }
    public int CutCount { get; private set; }
    public int CopyCount { get; private set; }
    public int PasteCount { get; private set; }
    public int DeleteCount { get; private set; }
    public int SelectAllCount { get; private set; }
    public int FindCount { get; private set; }
    public int ReplaceCount { get; private set; }

    public TemplateDocument ExportDocument() => Document;

    public void LoadDocument(TemplateDocument document)
    {
        Document = document;
        LoadCount++;
    }

    public void InsertTemplate() => InsertCount++;
    public void Undo() => UndoCount++;
    public void Redo() => RedoCount++;
    public void Cut() => CutCount++;
    public void Copy() => CopyCount++;
    public void Paste() => PasteCount++;
    public void Delete() => DeleteCount++;
    public void SelectAll() => SelectAllCount++;
    public void Find() => FindCount++;
    public void Replace() => ReplaceCount++;
}

internal sealed class FakeStorageService : IDocumentStorageService
{
    public IDocumentFile? OpenFile { get; set; }
    public MemoryDocumentFile SaveFile { get; set; } = new("document.itd");
    public string? SuggestedName { get; private set; }
    public int SaveAsCount { get; private set; }
    public bool CancelSaveAs { get; set; }

    public Task<IDocumentFile?> OpenAsync() => Task.FromResult(OpenFile);

    public Task<IDocumentFile?> SaveAsAsync(string suggestedName)
    {
        SaveAsCount++;
        SuggestedName = suggestedName;
        return Task.FromResult<IDocumentFile?>(CancelSaveAs ? null : SaveFile);
    }
}

internal sealed class MemoryDocumentFile(string name, string? identity = null) : IDocumentFile
{
    private DateTime _lastWriteTimeUtc;

    public string Name { get; } = name;
    public string Identity { get; } = identity ?? name;
    public MemoryStream Contents { get; } = new();
    public int ReadCount { get; private set; }
    public Exception? OpenReadException { get; set; }
    public Exception? OpenWriteException { get; set; }
    public bool HasVersion { get; set; } = true;

    public void SetDocumentText(string text)
    {
        var json = $$"""
            {"formatVersion":1,"content":[{"type":"text","text":"{{text}}"}]}
            """;
        SetRawContents(json);
    }

    public void SetRawContents(string contents)
    {
        Contents.SetLength(0);
        Contents.Write(Encoding.UTF8.GetBytes(contents));
        Contents.Position = 0;
        _lastWriteTimeUtc = _lastWriteTimeUtc.AddTicks(1);
    }

    public DocumentFileVersion? GetVersion() =>
        HasVersion
            ? new DocumentFileVersion(true, Contents.Length, _lastWriteTimeUtc)
            : null;

    public Task<Stream> OpenReadAsync()
    {
        if (OpenReadException is not null)
        {
            return Task.FromException<Stream>(OpenReadException);
        }

        ReadCount++;
        Contents.Position = 0;
        return Task.FromResult<Stream>(new NonClosingStream(Contents));
    }

    public Task<Stream> OpenWriteAsync()
    {
        if (OpenWriteException is not null)
        {
            return Task.FromException<Stream>(OpenWriteException);
        }

        Contents.Position = 0;
        return Task.FromResult<Stream>(new NonClosingStream(Contents));
    }
}

internal sealed class NonClosingStream(Stream inner) : Stream
{
    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => inner.CanSeek;
    public override bool CanWrite => inner.CanWrite;
    public override long Length => inner.Length;
    public override long Position { get => inner.Position; set => inner.Position = value; }
    public override void Flush() => inner.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) =>
        inner.FlushAsync(cancellationToken);
    public override int Read(byte[] buffer, int offset, int count) =>
        inner.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
    public override void SetLength(long value) => inner.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) =>
        inner.Write(buffer, offset, count);
    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default) =>
        inner.WriteAsync(buffer, cancellationToken);
}

internal sealed class FakeInteractionService : IUserInteractionService
{
    public UnsavedChoice UnsavedChoice { get; set; } = UnsavedChoice.Cancel;
    public Queue<UnsavedChoice> UnsavedChoices { get; } = new();
    public int UnsavedConfirmationCount { get; private set; }
    public List<(string Title, string Message)> Messages { get; } = [];
    public FontFamily? SelectedFont { get; set; }
    public FontFamily? CurrentFont { get; private set; }

    public Task<UnsavedChoice> ConfirmUnsavedChangesAsync()
    {
        UnsavedConfirmationCount++;
        return Task.FromResult(
            UnsavedChoices.TryDequeue(out var choice) ? choice : UnsavedChoice);
    }

    public Task ShowMessageAsync(string title, string message)
    {
        Messages.Add((title, message));
        return Task.CompletedTask;
    }
    public Task<FontFamily?> SelectFontAsync(FontFamily currentFontFamily)
    {
        CurrentFont = currentFontFamily;
        return Task.FromResult(SelectedFont);
    }
    public Task ShowTemplateHelpAsync() => Task.CompletedTask;
    public Task ShowAboutAsync() => Task.CompletedTask;
}
