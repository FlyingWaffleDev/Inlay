using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Inlay.Models;

namespace Inlay;

internal interface IEditorClipboard
{
    Task SetDataAsync(IAsyncDataTransfer dataTransfer);
    Task<IAsyncDataTransfer?> TryGetDataAsync();
}

internal sealed class TopLevelEditorClipboard(Func<IClipboard?> clipboardProvider) : IEditorClipboard
{
    public async Task SetDataAsync(IAsyncDataTransfer dataTransfer)
    {
        var clipboard = clipboardProvider();
        if (clipboard is null)
        {
            throw new InvalidOperationException("Clipboard is unavailable.");
        }

        await clipboard.SetDataAsync(dataTransfer).ConfigureAwait(true);
    }

    public async Task<IAsyncDataTransfer?> TryGetDataAsync()
    {
        var clipboard = clipboardProvider();
        if (clipboard is null)
        {
            return null;
        }

        return await clipboard.TryGetDataAsync().ConfigureAwait(true);
    }
}

internal sealed class TemplateClipboardPayload
{
    public const string FormatIdentifier = "inlay.template-document-fragment";

    public static readonly DataFormat<string> Format =
        DataFormat.CreateStringApplicationFormat(FormatIdentifier);

    public static readonly DataFormat<TemplateClipboardPayload> InProcessFormat =
        DataFormat.CreateInProcessFormat<TemplateClipboardPayload>(FormatIdentifier);

    public int FormatVersion { get; init; } = 1;

    public List<DocumentPart> Content { get; init; } = [];
}

internal static class TemplateClipboard
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Serialize(TemplateClipboardPayload payload) =>
        JsonSerializer.Serialize(payload, SerializerOptions);

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Clipboard data may be invalid or external content.")]
    public static TemplateClipboardPayload? Deserialize(string json)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<TemplateClipboardPayload>(json, SerializerOptions);
            return IsValid(payload) ? payload : null;
        }
        catch
        {
            return null;
        }
    }

    public static DataTransfer CreateDataTransfer(string plainText, TemplateClipboardPayload payload)
    {
        var transfer = new DataTransfer();
        var item = new DataTransferItem();
        item.Set(DataFormat.Text, plainText);
        item.Set(TemplateClipboardPayload.Format, Serialize(payload));
        item.Set(TemplateClipboardPayload.InProcessFormat, payload);
        transfer.Add(item);
        return transfer;
    }

    public static DataTransfer CreateTextDataTransfer(string plainText)
    {
        var transfer = new DataTransfer();
        var item = new DataTransferItem();
        item.Set(DataFormat.Text, plainText);
        transfer.Add(item);
        return transfer;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Clipboard operations can fail unexpectedly on native platforms.")]
    public static async Task<TemplateClipboardPayload?> TryGetPayloadAsync(IAsyncDataTransfer data)
    {
        try
        {
            if (data.Contains(TemplateClipboardPayload.InProcessFormat))
            {
                var inProc = await data.TryGetValueAsync(TemplateClipboardPayload.InProcessFormat)
                    .ConfigureAwait(true);
                if (IsValid(inProc))
                {
                    return inProc;
                }
            }

            if (data.Contains(TemplateClipboardPayload.Format))
            {
                var json = await data.TryGetValueAsync(TemplateClipboardPayload.Format)
                    .ConfigureAwait(true);
                if (!string.IsNullOrEmpty(json))
                {
                    return Deserialize(json);
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static bool IsValid(TemplateClipboardPayload? payload) =>
        payload is { FormatVersion: 1 } &&
        DocumentPartValidation.FindError(payload.Content) is null;
}
