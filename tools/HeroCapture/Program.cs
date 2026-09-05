using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using Inlay;
using Inlay.Models;
using Inlay.ViewModels;
using ReactiveUI.Avalonia;

var theme = args.FirstOrDefault()?.Equals("dark", StringComparison.OrdinalIgnoreCase) == true
    ? ThemeVariant.Dark
    : ThemeVariant.Light;
var examplePath = Path.Combine(AppContext.BaseDirectory, "product-launch.itd");
var exampleDocument = await LoadExampleDocumentAsync(examplePath).ConfigureAwait(false);

Build().StartWithClassicDesktopLifetime(args);

AppBuilder Build() => AppBuilder
    .Configure(() => new CaptureApp(theme, examplePath, exampleDocument))
    .UsePlatformDetect()
    .UseReactiveUI(_ => { });

static async Task<TemplateDocument> LoadExampleDocumentAsync(string path)
{
    using var stream = File.OpenRead(path);
    return await new JsonTemplateDocumentService().LoadAsync(stream).ConfigureAwait(false);
}

internal sealed class CaptureApp(
    ThemeVariant theme,
    string examplePath,
    TemplateDocument exampleDocument) : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = theme;
        Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://Inlay"))
        {
            Source = new Uri("avares://Inlay/Styles/ThemeResources.axaml")
        });
        Styles.Add(new FluentTheme());
        Styles.Add(new StyleInclude(new Uri("avares://Inlay"))
        {
            Source = new Uri("avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml")
        });
        Styles.Add(new StyleInclude(new Uri("avares://Inlay"))
        {
            Source = new Uri("avares://Inlay/Styles/InlayStyles.axaml")
        });
    }

    [SuppressMessage("Reliability", "CA2000",
        Justification = "The window owns the view model and disposes it when it closes.")]
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        var viewModel = new MainWindowViewModel(
            new NullDocumentService(), new NullStorageService(), new NullInteractionService(),
            new NullApplicationService());
        var exampleFile = new ExampleFile(examplePath);
        viewModel.SelectedDocument!.Editor.LoadDocument(exampleDocument);
        viewModel.SelectedDocument!.MarkSaved(exampleFile);

        var window = new MainWindow(viewModel)
        {
            Width = 1040,
            Height = 680,
            Position = new PixelPoint(90, 70),
            WindowStartupLocation = WindowStartupLocation.Manual,
            CanResize = false
        };
        window.Opened += (_, _) => PrepareCapture(window);
        desktop.MainWindow = window;
        base.OnFrameworkInitializationCompleted();
    }

    private void PrepareCapture(MainWindow window)
    {
        SetDecorationTheme(window);
        DispatcherTimer.RunOnce(() =>
        {
            OpenFlyout(window);
            DispatcherTimer.RunOnce(
                () => WriteCaptureInfo(window),
                TimeSpan.FromMilliseconds(250));
        }, TimeSpan.FromMilliseconds(250));
    }

    private static void WriteCaptureInfo(Window window)
    {
        var path = Environment.GetEnvironmentVariable("INLAY_HERO_CAPTURE_INFO");
        var handle = window.TryGetPlatformHandle();
        if (string.IsNullOrWhiteSpace(path) || handle?.HandleDescriptor != "XID")
        {
            return;
        }

        File.WriteAllText(
            path,
            $"0x{handle.Handle:x} {window.Position.X} {window.Position.Y}{Environment.NewLine}");
    }

    private void SetDecorationTheme(Window window)
    {
        var handle = window.TryGetPlatformHandle();
        if (handle?.HandleDescriptor != "XID" || !OperatingSystem.IsLinux())
        {
            return;
        }

        var colorScheme = theme == ThemeVariant.Dark
            ? "/usr/share/color-schemes/BreezeDark.colors"
            : "/usr/share/color-schemes/BreezeLight.colors";
        if (!File.Exists(colorScheme))
        {
            return;
        }

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "xprop",
            UseShellExecute = false,
            ArgumentList =
            {
                "-id", $"0x{handle.Handle:x}",
                "-f", "_KDE_NET_WM_COLOR_SCHEME", "8s",
                "-set", "_KDE_NET_WM_COLOR_SCHEME", colorScheme
            }
        });
        process?.WaitForExit();
    }

    private static void OpenFlyout(MainWindow window)
    {
        var editor = window.GetVisualDescendants().OfType<Inlay.Controls.TemplateTextEditor>().Single();
        var textView = editor.TextArea.TextView;
        textView.EnsureVisualLines();
        var template = textView.VisualLines
            .SelectMany(line => line.Elements)
            .OfType<TemplateTextElement>()
            .ElementAt(1);

        var templateLength = template.SelectedIndex >= 0
            ? template.Options[template.SelectedIndex].Length
            : TemplateTextElement.PlaceholderText.Length;
        var start = textView.GetVisualPosition(
            new TextViewPosition(editor.Document.GetLocation(template.Anchor.Offset)),
            VisualYPosition.LineMiddle);
        var end = textView.GetVisualPosition(
            new TextViewPosition(editor.Document.GetLocation(template.Anchor.Offset + templateLength)),
            VisualYPosition.LineMiddle);
        var midpoint = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);
        var editorPosition = textView.TranslatePoint(midpoint, editor) ?? midpoint;
        template.ShowFlyoutAt(editor, editorPosition);
    }
}

internal sealed class ExampleFile(string path) : IDocumentFile
{
    public string Name => Path.GetFileName(path);
    public string Identity => Path.GetFullPath(path);
    public DocumentFileVersion? GetVersion()
    {
        var info = new FileInfo(path);
        return info.Exists
            ? new DocumentFileVersion(true, info.Length, info.LastWriteTimeUtc)
            : new DocumentFileVersion(false, 0, default);
    }
    public Task<Stream> OpenReadAsync() => Task.FromResult<Stream>(File.OpenRead(path));
    public Task<Stream> OpenWriteAsync() => Task.FromResult<Stream>(File.OpenWrite(path));
}

internal sealed class NullDocumentService : ITemplateDocumentService
{
    public Task<TemplateDocument> LoadAsync(Stream stream, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task SaveAsync(Stream stream, TemplateDocument document, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class NullStorageService : IDocumentStorageService
{
    public Task<IDocumentFile?> OpenAsync() => Task.FromResult<IDocumentFile?>(null);
    public Task<IDocumentFile?> SaveAsAsync(string suggestedName) => Task.FromResult<IDocumentFile?>(null);
}

internal sealed class NullInteractionService : IUserInteractionService
{
    public Task<UnsavedChoice> ConfirmUnsavedChangesAsync() => Task.FromResult(UnsavedChoice.Cancel);
    public Task<FontFamily?> SelectFontAsync(FontFamily currentFontFamily) =>
        Task.FromResult<FontFamily?>(null);
    public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
    public Task ShowTemplateHelpAsync() => Task.CompletedTask;
    public Task ShowAboutAsync() => Task.CompletedTask;
}

internal sealed class NullApplicationService : IApplicationService
{
    public void OpenNewWindow() { }
    public Task ExitAsync() => Task.CompletedTask;
}
