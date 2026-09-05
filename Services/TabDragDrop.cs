using Avalonia.Input;
using Inlay.ViewModels;

namespace Inlay;

internal sealed class TabDragPayload
{
    public static readonly DataFormat<TabDragPayload> Format =
        DataFormat.CreateInProcessFormat<TabDragPayload>("application/x-inlay-tab");

    public required MainWindow SourceWindow { get; init; }
    public required DocumentTabViewModel SourceTab { get; init; }
}
