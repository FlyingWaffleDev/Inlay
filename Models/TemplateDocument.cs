using System.Text.Json.Serialization;

namespace Inlay.Models;

internal sealed class TemplateDocument
{
    public int FormatVersion { get; init; } = 1;
    public LineLengthSettings LineLength { get; init; } = new();
    public List<DocumentPart> Content { get; init; } = [];
}

internal sealed class LineLengthSettings
{
    public const int DefaultSoftLimit = 80;
    public const int DefaultHardLimit = 120;
    public const int MinimumLimit = 1;
    public const int MaximumLimit = 500;

    public bool Show { get; init; }
    public bool Enforce { get; init; }
    public int SoftLimit { get; init; } = DefaultSoftLimit;
    public int HardLimit { get; init; } = DefaultHardLimit;
}

internal sealed class DocumentPart
{
    [JsonConverter(typeof(JsonStringEnumConverter<DocumentPartKind>))]
    public DocumentPartKind Type { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Options { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SelectedIndex { get; init; }

    public static DocumentPart PlainText(string text) =>
        new() { Type = DocumentPartKind.Text, Text = text };

    public static DocumentPart Template(IEnumerable<string> options, int selectedIndex) =>
        new()
        {
            Type = DocumentPartKind.Template,
            Options = options.ToList(),
            SelectedIndex = selectedIndex
        };
}

internal enum DocumentPartKind
{
    Text,
    Template
}
