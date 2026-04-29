using ImvixPro.Services;

namespace ImvixPro.Tests;

public sealed class PreviewOcrServiceTests
{
    [Fact]
    public void SupportedLanguageOptions_IncludeThaiAndVietnamese()
    {
        Assert.Contains(PreviewOcrLanguageOption.Thai, PreviewOcrService.SupportedLanguageOptions);
        Assert.Contains(PreviewOcrLanguageOption.Vietnamese, PreviewOcrService.SupportedLanguageOptions);
    }
}
