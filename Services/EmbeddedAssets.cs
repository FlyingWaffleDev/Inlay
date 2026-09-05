using Avalonia.Platform;

namespace Inlay;

internal static class EmbeddedAssets
{
    public static Uri Uri(string name) => new($"avares://Inlay/Assets/{name}");

    // The toolbar's Svg controls each load on their own thread as the first window
    // is built. Reading one asset here builds this assembly's resource table once,
    // on the UI thread, before those loads start racing for it.
    public static void WarmUp()
    {
        using var stream = AssetLoader.Open(Uri("new-document.svg"));
    }
}
