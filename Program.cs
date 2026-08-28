using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia;

namespace Inlay;

public static class Program
{
    private static readonly Lazy<ServiceProvider> PreviewServices = new(CreatePreviewServiceProvider);

    [STAThread]
    public static void Main(string[] args)
    {
        using var provider = CreateServiceProvider();
        BuildAvaloniaApp(provider).StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => BuildAvaloniaApp(PreviewServices.Value);

    private static AppBuilder BuildAvaloniaApp(IServiceProvider services) =>
        AppBuilder.Configure(() => new App(services))
            .UsePlatformDetect()
            .UseReactiveUI(_ => { })
            .LogToTrace();

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITemplateDocumentService, JsonTemplateDocumentService>();
        services.AddSingleton<MainWindowFactory>();
        return services.BuildServiceProvider();
    }

    private static ServiceProvider CreatePreviewServiceProvider()
    {
        var provider = CreateServiceProvider();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => provider.Dispose();
        return provider;
    }
}
