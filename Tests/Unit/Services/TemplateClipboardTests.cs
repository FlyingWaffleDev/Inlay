using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Inlay.Models;
using Xunit;

namespace Inlay.Tests;

public sealed class TemplateClipboardTests
{
    [Fact]
    public void SerializeAndDeserializePreservesTemplatePayload()
    {
        var source = new TemplateClipboardPayload
        {
            FormatVersion = 1,
            Content =
            [
                DocumentPart.PlainText("Hello "),
                DocumentPart.Template(["World", "Universe"], 1),
                DocumentPart.PlainText("!")
            ]
        };

        var json = TemplateClipboard.Serialize(source);
        var loaded = TemplateClipboard.Deserialize(json);

        Assert.NotNull(loaded);
        Assert.Equal(1, loaded.FormatVersion);
        Assert.Equal(3, loaded.Content.Count);
        Assert.Equal("Hello ", loaded.Content[0].Text);
        Assert.Equal(DocumentPartKind.Template, loaded.Content[1].Type);
        Assert.Equal(["World", "Universe"], loaded.Content[1].Options);
        Assert.Equal(1, loaded.Content[1].SelectedIndex);
        Assert.Equal("!", loaded.Content[2].Text);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{ "formatVersion": 2, "content": [] }""")]
    [InlineData("""{ "formatVersion": 1, "content": null }""")]
    [InlineData("""{ "formatVersion": 1, "content": [{ "type": "InvalidType" }] }""")]
    [InlineData("""{ "formatVersion": 1, "content": [{ "type": "text", "text": null }] }""")]
    [InlineData("""{ "formatVersion": 1, "content": [{ "type": "template", "options": ["A"], "selectedIndex": 5 }] }""")]
    [InlineData("""{ "formatVersion": 1, "content": [{ "type": "template", "options": ["A"], "selectedIndex": -2 }] }""")]
    [InlineData("""{ "formatVersion": 1, "content": [{ "type": "template", "options": [""], "selectedIndex": 0 }] }""")]
    [InlineData("""{ "formatVersion": 1, "content": [{ "type": "template", "options": [null], "selectedIndex": 0 }] }""")]
    [InlineData("""{ "formatVersion": 1, "content": [null] }""")]
    public void DeserializeReturnsNullForMalformedData(string json)
    {
        Assert.Null(TemplateClipboard.Deserialize(json));
    }

    [Fact]
    public async Task TryGetPayloadAsyncRejectsMalformedInProcessData()
    {
        using var transfer = TemplateClipboard.CreateDataTransfer(
            "Fallback",
            new TemplateClipboardPayload
            {
                Content = [DocumentPart.Template([string.Empty], 0)]
            });

        var result = await TemplateClipboard.TryGetPayloadAsync(transfer);

        Assert.Null(result);
    }

    [AvaloniaFact]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Avalonia clipboard takes ownership of outgoing DataTransfer.")]
    public async Task TryGetPayloadAsyncPrefersInProcessFormat()
    {
        var window = new Window();
        window.Show();
        try
        {
            var payload = new TemplateClipboardPayload
            {
                FormatVersion = 1,
                Content = [DocumentPart.Template(["Option1", "Option2"], 0)]
            };

            var transfer = TemplateClipboard.CreateDataTransfer("Option1", payload);
            await window.Clipboard!.SetDataAsync(transfer);

            using var retrieved = await window.Clipboard.TryGetDataAsync();
            Assert.NotNull(retrieved);
            Assert.True(retrieved.Contains(DataFormat.Text));
            Assert.True(retrieved.Contains(TemplateClipboardPayload.Format));
            Assert.True(retrieved.Contains(TemplateClipboardPayload.InProcessFormat));

            var result = await TemplateClipboard.TryGetPayloadAsync(retrieved);
            Assert.NotNull(result);
            Assert.Single(result.Content);
            Assert.Equal(["Option1", "Option2"], result.Content[0].Options);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Avalonia clipboard takes ownership of outgoing DataTransfer.")]
    public async Task TryGetPayloadAsyncFallsBackToStringFormat()
    {
        var window = new Window();
        window.Show();
        try
        {
            var payload = new TemplateClipboardPayload
            {
                FormatVersion = 1,
                Content = [DocumentPart.Template(["Alpha", "Beta"], 1)]
            };

            var json = TemplateClipboard.Serialize(payload);
            var transfer = new DataTransfer();
            var item = new DataTransferItem();
            item.Set(DataFormat.Text, "Beta");
            item.Set(TemplateClipboardPayload.Format, json);
            transfer.Add(item);

            await window.Clipboard!.SetDataAsync(transfer);

            using var retrieved = await window.Clipboard.TryGetDataAsync();
            Assert.NotNull(retrieved);

            var result = await TemplateClipboard.TryGetPayloadAsync(retrieved);
            Assert.NotNull(result);
            Assert.Single(result.Content);
            Assert.Equal(["Alpha", "Beta"], result.Content[0].Options);
            Assert.Equal(1, result.Content[0].SelectedIndex);
        }
        finally
        {
            window.Close();
        }
    }
}
