using System.Runtime.CompilerServices;
using ReactiveUI.Builder;

namespace Inlay.Tests;

internal static class ReactiveUiTestInitializer
{
    [ModuleInitializer]
    internal static void Initialize() =>
        RxAppBuilder.CreateReactiveUIBuilder()
            .WithCoreServices()
            .BuildApp();
}
