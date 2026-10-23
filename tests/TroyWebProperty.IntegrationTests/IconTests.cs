using Xunit;

namespace TroyWebProperty.IntegrationTests;

public sealed class IconTests
{

    [Fact]
    public async Task PublicPagesRenderDecorativeFreeIconsAndCdnFallbacks()
    {
        await using var factory = new RentalFactory();
        using var browser = factory.CreateClient();
        var html = await browser.GetStringAsync("/");
        Assert.DoesNotContain("<fa-icon", html);
        Assert.Contains("fa-solid fa-house fa-icon", html);
        Assert.Contains("aria-hidden=\"true\"", html);
        Assert.Contains("fontawesome-free@7.3.1/css/fontawesome.min.css", html);
        Assert.Contains("fontawesome-free@7.3.1/css/solid.min.css", html);
        Assert.Contains("/lib/fontawesome/css/solid.min.css?v=7.3.1", html);
        Assert.Contains("--fa-style", html);
        Assert.DoesNotContain("kit.fontawesome.com", html);
    }

}