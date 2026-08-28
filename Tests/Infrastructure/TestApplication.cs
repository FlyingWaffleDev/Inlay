using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(Inlay.Tests.TestApplication))]

namespace Inlay.Tests;

internal sealed class TestApplication : Application
{
    public override void Initialize()
    {
        Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://Inlay.Tests"))
        {
            Source = new Uri("avares://Inlay/Styles/ThemeResources.axaml")
        });
        Styles.Add(new FluentTheme());
        Styles.Add(new StyleInclude(new Uri("avares://Inlay.Tests"))
        {
            Source = new Uri("avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml")
        });
        Styles.Add(new StyleInclude(new Uri("avares://Inlay.Tests"))
        {
            Source = new Uri("avares://Inlay/Styles/InlayStyles.axaml")
        });
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApplication>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = true
            });
}
