using Avalonia;
using ImvixPro.Views;

namespace ImvixPro.Tests;

public sealed class WindowScreenBoundsHelperTests
{
    [Fact]
    public void CalculateConstrainedBounds_PreservesLargeScreenLayout()
    {
        var bounds = WindowScreenBoundsHelper.CalculateConstrainedBounds(
            new PixelRect(0, 0, 3840, 2160),
            new PixelPoint(320, 180),
            desiredWidthDip: 1280,
            desiredHeightDip: 760,
            originalMinWidthDip: 980,
            originalMinHeightDip: 620,
            scaling: 1d);

        Assert.Equal(1280d, bounds.WidthDip);
        Assert.Equal(760d, bounds.HeightDip);
        Assert.Equal(980d, bounds.MinWidthDip);
        Assert.Equal(620d, bounds.MinHeightDip);
        Assert.Equal(new PixelPoint(320, 180), bounds.Position);
    }

    [Fact]
    public void CalculateConstrainedBounds_ClampsSmallScreenHeightAndTopEdge()
    {
        var bounds = WindowScreenBoundsHelper.CalculateConstrainedBounds(
            new PixelRect(0, 0, 1366, 728),
            new PixelPoint(-40, -20),
            desiredWidthDip: 1280,
            desiredHeightDip: 760,
            originalMinWidthDip: 980,
            originalMinHeightDip: 620,
            scaling: 1d);

        Assert.Equal(1280d, bounds.WidthDip);
        Assert.Equal(728d, bounds.HeightDip);
        Assert.Equal(980d, bounds.MinWidthDip);
        Assert.Equal(620d, bounds.MinHeightDip);
        Assert.Equal(new PixelPoint(0, 0), bounds.Position);
    }

    [Fact]
    public void CalculateConstrainedBounds_LowersMinimumSizeWhenScreenIsSmallerThanWindow()
    {
        var bounds = WindowScreenBoundsHelper.CalculateConstrainedBounds(
            new PixelRect(1920, 40, 1200, 750),
            new PixelPoint(2500, 300),
            desiredWidthDip: 1280,
            desiredHeightDip: 820,
            originalMinWidthDip: 980,
            originalMinHeightDip: 620,
            scaling: 1.25d);

        Assert.Equal(960d, bounds.WidthDip, 6);
        Assert.Equal(600d, bounds.HeightDip, 6);
        Assert.Equal(960d, bounds.MinWidthDip, 6);
        Assert.Equal(600d, bounds.MinHeightDip, 6);
        Assert.Equal(new PixelPoint(1920, 40), bounds.Position);
    }

    [Fact]
    public void CalculateConstrainedBounds_ClampsPositionInsideWorkingArea()
    {
        var bounds = WindowScreenBoundsHelper.CalculateConstrainedBounds(
            new PixelRect(1920, 0, 2560, 1400),
            new PixelPoint(4300, 1000),
            desiredWidthDip: 760,
            desiredHeightDip: 680,
            originalMinWidthDip: 680,
            originalMinHeightDip: 600,
            scaling: 1d);

        Assert.Equal(new PixelPoint(3720, 720), bounds.Position);
    }
}
