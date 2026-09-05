using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace Inlay;

internal sealed class App(IServiceProvider services) : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

#if DEBUG
        if (Debugger.IsAttached)
        {
            this.AttachDeveloperTools();
        }
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            EmbeddedAssets.WarmUp();
            desktop.MainWindow = services.GetRequiredService<MainWindowFactory>().Create();
            SystemMonospaceFonts.WarmUp();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
