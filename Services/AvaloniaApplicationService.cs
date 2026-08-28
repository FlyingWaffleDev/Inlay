using Avalonia.Controls;

namespace Inlay;

internal sealed class AvaloniaApplicationService(
    Window owner,
    Func<Window> createWindow) : IApplicationService
{
    public void OpenNewWindow()
    {
        createWindow().Show();
    }

    public void Exit() => owner.Close();
}
