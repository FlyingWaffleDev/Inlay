using System.Text.Json;
using System.Text.Json.Serialization;
using Inlay.Models;

namespace Inlay;

internal sealed class JsonTemplateDocumentService : ITemplateDocumentService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public async Task<TemplateDocument> LoadAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var document = await JsonSerializer.DeserializeAsync<TemplateDocument>(
            stream,
            Options,
            cancellationToken).ConfigureAwait(false) ?? throw new InvalidDataException("The file does not contain a template document.");

        if (document.FormatVersion != 1)
        {
            throw new InvalidDataException(
                $"Document format version {document.FormatVersion} is not supported.");
        }

        if (document.Content is null)
        {
            throw new InvalidDataException("The document is missing its content collection.");
        }

        if (document.LineLength is null)
        {
            throw new InvalidDataException("The document has null line length settings.");
        }

        if (document.LineLength.SoftLimit < LineLengthSettings.MinimumLimit)
        {
            throw new InvalidDataException(
                $"The soft line length limit must be at least {LineLengthSettings.MinimumLimit}.");
        }

        if (document.LineLength.HardLimit < document.LineLength.SoftLimit)
        {
            throw new InvalidDataException(
                "The hard line length limit cannot be lower than the soft limit.");
        }

        if (document.LineLength.HardLimit > LineLengthSettings.MaximumLimit)
        {
            throw new InvalidDataException(
                $"The hard line length limit cannot exceed {LineLengthSettings.MaximumLimit}.");
        }

        foreach (var part in document.Content)
        {
            if (!Enum.IsDefined(part.Type))
            {
                throw new InvalidDataException("A document part has an invalid type.");
            }

            if (part.Type == DocumentPartKind.Text && part.Text is null)
            {
                throw new InvalidDataException("A text part is missing its text value.");
            }

            if (part.Type == DocumentPartKind.Template)
            {
                var count = part.Options?.Count ?? 0;
                var selectedIndex = part.SelectedIndex ?? -1;
                if (selectedIndex < -1 || selectedIndex >= count)
                {
                    throw new InvalidDataException("A template has an invalid selectedIndex value.");
                }

                if (part.Options?.Exists(string.IsNullOrEmpty) == true)
                {
                    throw new InvalidDataException("A template choice cannot be empty.");
                }
            }
        }

        return document;
    }

    public async Task SaveAsync(
        Stream stream,
        TemplateDocument document,
        CancellationToken cancellationToken = default)
    {
        // Serialize first so a failure here cannot truncate the existing file.
        using var buffer = new MemoryStream();
        await JsonSerializer.SerializeAsync(buffer, document, Options, cancellationToken)
            .ConfigureAwait(false);

        if (stream.CanSeek)
        {
            stream.Position = 0;
            stream.SetLength(0);
        }

        buffer.Position = 0;
        await buffer.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
