using Avalonia.Input;

namespace Inlay;

internal sealed class TemplateChoiceDragPayload
{
    public static readonly DataFormat<TemplateChoiceDragPayload> Format =
        DataFormat.CreateInProcessFormat<TemplateChoiceDragPayload>(
            "application/x-inlay-template-choice");

    public required TemplateFlyoutView SourceView { get; init; }
    public required string SourceChoice { get; init; }
}
