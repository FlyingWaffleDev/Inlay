using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Inlay.ViewModels;

namespace Inlay;

internal sealed class AvaloniaApplicationService(
    Window owner,
    Func<Window> createWindow,
    Func<IReadOnlyList<Window>>? getOpenWindows = null) : IApplicationService
{
    public void OpenNewWindow()
    {
        createWindow().Show();
    }

    public async Task ExitAsync()
    {
        var windows = getOpenWindows?.Invoke().ToArray() ??
                      (Application.Current?.ApplicationLifetime is
                          IClassicDesktopStyleApplicationLifetime desktop
                              ? desktop.Windows.ToArray()
                              : [owner]);
        foreach (var window in windows.OfType<MainWindow>())
        {
            if (window.DataContext is MainWindowViewModel viewModel &&
                !await viewModel.CanCloseAsync().ConfigureAwait(true))
            {
                return;
            }
        }

        foreach (var window in windows.OfType<MainWindow>())
        {
            window.ApproveApplicationExit();
        }

        foreach (var window in windows)
        {
            window.Close();
        }
    }
}
