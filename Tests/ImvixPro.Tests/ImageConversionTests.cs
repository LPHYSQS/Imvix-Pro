using ImvixPro.Models;
using ImvixPro.Services;

namespace ImvixPro.Tests;

public sealed class ImageConversionTests
{
    [Theory]
    [InlineData(OutputImageFormat.Png, ".png", true)]
    [InlineData(OutputImageFormat.Jpeg, ".jpg", false)]
    [InlineData(OutputImageFormat.Webp, ".webp", true)]
    [InlineData(OutputImageFormat.Bmp, ".bmp", true)]
    [InlineData(OutputImageFormat.Gif, ".gif", false)]
    [InlineData(OutputImageFormat.Tiff, ".tiff", true)]
    [InlineData(OutputImageFormat.Ico, ".ico", true)]
    [InlineData(OutputImageFormat.Svg, ".svg", true)]
    public void OutputFormatMetadata_RemainsStable(OutputImageFormat format, string expectedExtension, bool supportsTransparency)
    {
        Assert.Equal(expectedExtension, ImageConversionService.GetFileExtension(format));
        Assert.Equal(supportsTransparency, ImageConversionService.OutputFormatSupportsTransparency(format));
    }

    [Fact]
    public void SupportedInputExtensions_ContainCoreFormats()
    {
        string[] expectedExtensions = [".png", ".jpg", ".gif", ".pdf", ".psd", ".exe", ".lnk"];

        foreach (var extension in expectedExtensions)
        {
            Assert.Contains(extension, ImageConversionService.SupportedInputExtensions);
        }
    }
}
