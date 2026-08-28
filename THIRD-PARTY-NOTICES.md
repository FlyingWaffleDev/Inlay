# Third-party notices

Inlay uses the NuGet packages listed below. This inventory was taken from the
resolved `net10.0` dependency graphs for `Inlay.csproj` and
`Tests/Inlay.Tests.csproj` on 2026-08-28. Versions include transitive
dependencies, not only packages referenced directly by the project files.

The license identifiers come from each package's NuGet manifest. The ANGLE
entry uses the license file included in its package because its manifest names
that file instead of an SPDX expression. Preserve any license or notice files
shipped inside a package when redistributing its binaries.

## Application dependencies

| Package | Version | License |
| --- | --- | --- |
| `Avalonia`, `Avalonia.Desktop`, `Avalonia.FreeDesktop`, `Avalonia.FreeDesktop.AtSpi`, `Avalonia.HarfBuzz`, `Avalonia.Native`, `Avalonia.Remote.Protocol`, `Avalonia.Skia`, `Avalonia.Themes.Fluent`, `Avalonia.Win32`, `Avalonia.X11` | 12.1.1 | [MIT](https://licenses.nuget.org/MIT) |
| `Avalonia.AvaloniaEdit` | 12.0.0 | [MIT](https://licenses.nuget.org/MIT) |
| `Avalonia.Angle.Windows.Natives` | 2.1.27548.20260419 | [BSD 3-Clause, package copy](https://github.com/AvaloniaUI/angle/blob/1c89805903c1482166356d3b950d474973180e61/LICENSE) |
| `ExCSS` | 4.3.1 | [MIT](https://licenses.nuget.org/MIT) |
| `HarfBuzzSharp`, `HarfBuzzSharp.NativeAssets.Linux`, `HarfBuzzSharp.NativeAssets.macOS`, `HarfBuzzSharp.NativeAssets.Win32` | 14.2.0 | [MIT](https://licenses.nuget.org/MIT) |
| `HarfBuzzSharp.NativeAssets.WebAssembly` | 8.3.1.3 | [MIT](https://licenses.nuget.org/MIT) |
| `MicroCom.Runtime` | 0.11.6 | [MIT](https://licenses.nuget.org/MIT) |
| `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.DependencyInjection.Abstractions` | 10.0.11 | [MIT](https://licenses.nuget.org/MIT) |
| `ReactiveUI`, `ReactiveUI.Core` | 24.1.0 | [MIT](https://licenses.nuget.org/MIT) |
| `ReactiveUI.Avalonia` | 12.1.1 | [MIT](https://licenses.nuget.org/MIT) |
| `ReactiveUI.Disposables`, `ReactiveUI.Primitives`, `ReactiveUI.Primitives.Avalonia`, `ReactiveUI.Primitives.Core` | 7.1.0 | [MIT](https://licenses.nuget.org/MIT) |
| `ShimSkiaSharp`, `Svg.Animation`, `Svg.Model`, `Svg.SceneGraph`, `Svg.Skia` | 5.2.1 | [MIT](https://licenses.nuget.org/MIT) |
| `Svg.Custom` | 5.2.1 | [Microsoft Public License](https://licenses.nuget.org/MS-PL) |
| `Svg.Controls.Skia.Avalonia` | 12.0.0.15 | [MIT](https://licenses.nuget.org/MIT) |
| `SkiaSharp`, `SkiaSharp.NativeAssets.Linux`, `SkiaSharp.NativeAssets.macOS`, `SkiaSharp.NativeAssets.Win32` | 4.148.0 | [MIT](https://licenses.nuget.org/MIT) |
| `SkiaSharp.NativeAssets.WebAssembly` | 3.119.4 | [MIT](https://licenses.nuget.org/MIT) |
| `Splat`, `Splat.Builder`, `Splat.Core`, `Splat.Logging` | 20.2.0 | [MIT](https://licenses.nuget.org/MIT) |
| `Tmds.DBus.Protocol` | 0.94.1 | [MIT](https://licenses.nuget.org/MIT) |

## Build and debug dependencies

These packages support compilation or Debug builds. They are not runtime
libraries in a normal Release distribution.

| Package | Version | License |
| --- | --- | --- |
| `Avalonia.BuildServices` | 11.3.2 | [MIT](https://licenses.nuget.org/MIT) |
| `AvaloniaUI.DiagnosticsSupport` | 2.2.3 | The package manifest declares no license. [Avalonia's FAQ](https://docs.avaloniaui.net/tools/faq#can-everybody-build-project-referencing-avaloniauidiagnosticssupport-even-without-a-license) says this bridge requires no license on its own and may be referenced by public projects. |
| `Microsoft.Extensions.Logging.Abstractions` | 8.0.0 | [MIT](https://licenses.nuget.org/MIT) |
| `Microsoft.IO.RecyclableMemoryStream` | 3.0.1 | [MIT](https://licenses.nuget.org/MIT) |
| `ReactiveUI.SourceGenerators`, `ReactiveUI.SourceGenerators.Analyzers.CodeFixes` | 3.2.0 | [MIT](https://licenses.nuget.org/MIT) |

## Test dependencies

These packages occur only in the test project's resolved dependency graph.

| Package | Version | License |
| --- | --- | --- |
| `Avalonia.Fonts.Inter`, `Avalonia.Headless`, `Avalonia.Headless.XUnit` | 12.1.1 | [MIT](https://licenses.nuget.org/MIT) |
| `Microsoft.ApplicationInsights` | 2.23.0 | [MIT](https://licenses.nuget.org/MIT) |
| `Microsoft.Bcl.AsyncInterfaces` | 6.0.0 | [MIT](https://licenses.nuget.org/MIT) |
| `Microsoft.CodeCoverage`, `Microsoft.NET.Test.Sdk`, `Microsoft.TestPlatform.ObjectModel`, `Microsoft.TestPlatform.TestHost` | 18.9.0 | [MIT](https://licenses.nuget.org/MIT) |
| `Microsoft.Testing.Extensions.Telemetry`, `Microsoft.Testing.Extensions.TrxReport.Abstractions`, `Microsoft.Testing.Platform`, `Microsoft.Testing.Platform.MSBuild` | 1.9.1 | [MIT](https://licenses.nuget.org/MIT) |
| `Microsoft.Win32.Registry` | 5.0.0 | [MIT](https://licenses.nuget.org/MIT) |
| `xunit.analyzers` | 1.27.0 | [Apache License 2.0](https://licenses.nuget.org/Apache-2.0) |
| `xunit.runner.visualstudio` | 4.0.0 | [Apache License 2.0](https://licenses.nuget.org/Apache-2.0) |
| `xunit.v3`, `xunit.v3.assert`, `xunit.v3.common`, `xunit.v3.core.mtp-v1`, `xunit.v3.extensibility.core`, `xunit.v3.mtp-v1`, `xunit.v3.runner.common`, `xunit.v3.runner.inproc.console` | 3.2.2 | [Apache License 2.0](https://licenses.nuget.org/Apache-2.0) |

The GNU GPLv3 license for Inlay does not replace the licenses above. Each
third-party component remains available under its own terms.
