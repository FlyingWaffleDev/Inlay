using Inlay.ViewModels;

namespace Inlay;

internal sealed class MainWindowFactory(ITemplateDocumentService documentService)
{
    public MainWindow Create()
    {
        var window = new MainWindow();
        window.DataContext = new MainWindowViewModel(
            documentService,
            new AvaloniaDocumentStorageService(window),
            new AvaloniaUserInteractionService(window),
            new AvaloniaApplicationService(window, Create));
        return window;
    }
}
