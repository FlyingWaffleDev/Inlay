using System.Text;
using Inlay.Models;
using Xunit;

namespace Inlay.Tests;

public sealed class JsonTemplateDocumentServiceTests
{
    [Fact]
    public async Task SaveAndLoadPreservesTemplateChoices()
    {
        var service = new JsonTemplateDocumentService();
        var source = new TemplateDocument
        {
            LineLength = new LineLengthSettings
            {
                Show = true,
                Enforce = true,
                SoftLimit = 72,
                HardLimit = 96
            },
            Content =
            [
                DocumentPart.PlainText("Dear "),
                DocumentPart.Template(["Ada", "Grace"], 1),
                DocumentPart.PlainText(",\nWelcome.")
            ]
        };
        using var stream = new MemoryStream();

        await service.SaveAsync(stream, source, TestContext.Current.CancellationToken);
        stream.Position = 0;
        var loaded = await service.LoadAsync(stream, TestContext.Current.CancellationToken);

        Assert.Equal(3, loaded.Content.Count);
        Assert.Equal(DocumentPartKind.Template, loaded.Content[1].Type);
        Assert.Equal(["Ada", "Grace"], loaded.Content[1].Options);
        Assert.Equal(1, loaded.Content[1].SelectedIndex);
        Assert.True(loaded.LineLength.Show);
        Assert.True(loaded.LineLength.Enforce);
        Assert.Equal(72, loaded.LineLength.SoftLimit);
        Assert.Equal(96, loaded.LineLength.HardLimit);
    }

    [Fact]
    public async Task LoadUsesDefaultLineLengthsForOlderDocuments()
    {
        const string json = """
            { "formatVersion": 1, "content": [] }
            """;

        var loaded = await LoadJsonAsync(json);

        Assert.False(loaded.LineLength.Show);
        Assert.False(loaded.LineLength.Enforce);
        Assert.Equal(80, loaded.LineLength.SoftLimit);
        Assert.Equal(120, loaded.LineLength.HardLimit);
    }

    [Theory]
    [InlineData(0, 120)]
    [InlineData(80, 79)]
    [InlineData(80, 501)]
    public async Task LoadRejectsInvalidLineLengths(int softLimit, int hardLimit)
    {
        var json = $$"""
            {
              "formatVersion": 1,
              "lineLength": {
                "softLimit": {{softLimit}},
                "hardLimit": {{hardLimit}}
              },
              "content": []
            }
            """;

        await Assert.ThrowsAsync<InvalidDataException>(() => LoadJsonAsync(json));
    }

    [Fact]
    public async Task LoadRejectsNullLineLengthSettings()
    {
        const string json = """
            { "formatVersion": 1, "lineLength": null, "content": [] }
            """;

        var error = await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadJsonAsync(json));

        Assert.Contains("null line length", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(1)]
    public async Task LoadRejectsAnOutOfRangeSelection(int selectedIndex)
    {
        var json = $$"""
            {
              "formatVersion": 1,
              "content": [
                { "type": "template", "options": ["one"], "selectedIndex": {{selectedIndex}} }
              ]
            }
            """;

        await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadJsonAsync(json));
    }

    [Fact]
    public async Task LoadRejectsAnEmptySelectedChoice()
    {
        const string json = """
            {
              "formatVersion": 1,
              "content": [
                { "type": "template", "options": [""], "selectedIndex": 0 }
              ]
            }
            """;

        var error = await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadJsonAsync(json));

        Assert.Contains("cannot be empty", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadRejectsEmptyAndNullUnselectedChoices()
    {
        const string json = """
            {
              "formatVersion": 1,
              "content": [
                { "type": "template", "options": ["one", "", null], "selectedIndex": 0 }
              ]
            }
            """;

        var error = await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadJsonAsync(json));

        Assert.Contains("cannot be empty", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadRejectsAnUnsupportedFormatVersion()
    {
        const string json = """
            { "formatVersion": 2, "content": [] }
            """;

        var error = await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadJsonAsync(json));

        Assert.Contains("version 2", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadRejectsATextPartWithoutText()
    {
        const string json = """
            {
              "formatVersion": 1,
              "content": [{ "type": "text" }]
            }
            """;

        var error = await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadJsonAsync(json));

        Assert.Contains("missing its text value", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadRejectsANullDocument()
    {
        var error = await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadJsonAsync("null"));

        Assert.Contains("does not contain", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadRejectsANullContentCollection()
    {
        const string json = """
            { "formatVersion": 1, "content": null }
            """;

        var error = await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadJsonAsync(json));

        Assert.Contains("content collection", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadRejectsAnInvalidPartType()
    {
        const string json = """
            { "formatVersion": 1, "content": [{ "type": 99 }] }
            """;

        var error = await Assert.ThrowsAsync<InvalidDataException>(
            () => LoadJsonAsync(json));

        Assert.Contains("invalid type", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveTruncatesExistingSeekableContent()
    {
        var service = new JsonTemplateDocumentService();
        var oldContents = Encoding.UTF8.GetBytes(new string('x', 4096));
        using var stream = new MemoryStream();
        await stream.WriteAsync(oldContents, TestContext.Current.CancellationToken);
        stream.Position = 0;

        await service.SaveAsync(
            stream,
            new TemplateDocument { Content = [DocumentPart.PlainText("short")] },
            TestContext.Current.CancellationToken);

        Assert.True(stream.Length < oldContents.Length);
        stream.Position = 0;
        var loaded = await service.LoadAsync(stream, TestContext.Current.CancellationToken);
        Assert.Equal("short", Assert.Single(loaded.Content).Text);
    }

    [Fact]
    public async Task SaveRewindsASeekableStreamBeforeWriting()
    {
        var service = new JsonTemplateDocumentService();
        using var stream = new MemoryStream();
        await stream.WriteAsync(
            Encoding.UTF8.GetBytes(new string('x', 128)),
            TestContext.Current.CancellationToken);
        stream.Position = 47;

        await service.SaveAsync(
            stream,
            new TemplateDocument { Content = [DocumentPart.PlainText("saved")] },
            TestContext.Current.CancellationToken);

        Assert.Equal((byte)'{', stream.GetBuffer()[0]);
        stream.Position = 0;
        var loaded = await service.LoadAsync(stream, TestContext.Current.CancellationToken);
        Assert.Equal("saved", Assert.Single(loaded.Content).Text);
    }

    private static async Task<TemplateDocument> LoadJsonAsync(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return await new JsonTemplateDocumentService().LoadAsync(
            stream,
            TestContext.Current.CancellationToken);
    }
}
