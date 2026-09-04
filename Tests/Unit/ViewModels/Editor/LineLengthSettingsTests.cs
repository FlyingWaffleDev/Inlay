using Inlay.Models;
using Inlay.ViewModels;
using Xunit;

namespace Inlay.Tests;

public sealed class LineLengthSettingsTests
{
    [Fact]
    public void SoftLimitRaisesHardLimitToKeepTheRangeValid()
    {
        var editor = new TemplateEditorViewModel();
        var observedRanges = new List<(int Soft, int Hard)>();
        editor.PropertyChanged += (_, _) => observedRanges.Add(
            (editor.SoftLineLengthLimit, editor.HardLineLengthLimit));

        editor.SoftLineLengthLimit = 140;

        Assert.Equal(140, editor.SoftLineLengthLimit);
        Assert.Equal(140, editor.HardLineLengthLimit);
        Assert.All(observedRanges, range => Assert.True(range.Hard >= range.Soft));
    }

    [Fact]
    public void HardLimitCannotDropBelowSoftLimit()
    {
        var editor = new TemplateEditorViewModel();

        editor.HardLineLengthLimit = 40;

        Assert.Equal(80, editor.HardLineLengthLimit);
    }

    [Fact]
    public void LoweringAnEqualHardLimitMovesTheSoftLimitWithIt()
    {
        var editor = new TemplateEditorViewModel { HardLineLengthLimit = 80 };

        editor.HardLineLengthLimit = 79;

        Assert.Equal(79, editor.SoftLineLengthLimit);
        Assert.Equal(79, editor.HardLineLengthLimit);
    }

    [Fact]
    public void LimitsAreClampedToTheRangeTheDocumentFormatAllows()
    {
        var editor = new TemplateEditorViewModel();

        editor.HardLineLengthLimit = LineLengthSettings.MaximumLimit + 1_000;
        Assert.Equal(LineLengthSettings.MaximumLimit, editor.HardLineLengthLimit);

        editor.SoftLineLengthLimit = LineLengthSettings.MaximumLimit + 1_000;
        Assert.Equal(LineLengthSettings.MaximumLimit, editor.SoftLineLengthLimit);

        editor.SoftLineLengthLimit = -5;
        Assert.Equal(LineLengthSettings.MinimumLimit, editor.SoftLineLengthLimit);

        var exported = editor.ExportDocument().LineLength;
        Assert.InRange(
            exported.HardLimit,
            LineLengthSettings.MinimumLimit,
            LineLengthSettings.MaximumLimit);
        Assert.InRange(
            exported.SoftLimit,
            LineLengthSettings.MinimumLimit,
            exported.HardLimit);
    }
}
