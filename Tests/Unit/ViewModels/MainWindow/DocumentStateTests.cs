using Inlay.Models;
using Xunit;

namespace Inlay.Tests;

public sealed class DocumentStateTests
{
    [Fact]
    public void EditingUpdatesDirtyStateTitleAndTabHeader()
    {
        using var context = MainWindowViewModelTestContext.Create();

        context.ViewModel.Editor!.ReportContentChanged();

        Assert.True(context.ViewModel.IsDirty);
        Assert.Equal("Untitled * - Inlay", context.ViewModel.Title);
        Assert.Equal("Untitled *", context.ViewModel.SelectedDocument!.Header);
    }

    [Fact]
    public void UnsavedTabHeaderUsesAndUpdatesATruncatedFirstLine()
    {
        using var context = MainWindowViewModelTestContext.Create();
        context.Editor.Document = new TemplateDocument
        {
            Content = [DocumentPart.PlainText("  123456789012345678901234567890\nSecond line")]
        };

        context.ViewModel.Editor!.ReportContentChanged();

        Assert.Equal(
            "Untitled (123456789012345678901234…) *",
            context.ViewModel.SelectedDocument!.Header);

        context.Editor.Document = new TemplateDocument
        {
            Content = [DocumentPart.PlainText("Updated first line\nSecond line")]
        };
        context.ViewModel.Editor.ReportContentChanged();

        Assert.Equal("Untitled (Updated first line) *", context.ViewModel.SelectedDocument.Header);
    }

    [Theory]
    [InlineData(-100, 8)]
    [InlineData(100, 40)]
    public void ZoomStopsAtItsLimits(int steps, double expectedSize)
    {
        using var context = MainWindowViewModelTestContext.Create();

        context.ViewModel.AdjustEditorZoom(steps);

        Assert.Equal(expectedSize, context.ViewModel.EditorFontSize);
    }
}
