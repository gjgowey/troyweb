using System.Net;
using TroyWebProperty.Core;
using Xunit;
using static TroyWebProperty.IntegrationTests.RentalFactory;

namespace TroyWebProperty.IntegrationTests;

public sealed class FrontendAccessibilityTests
{

    [Fact]
    public async Task InvalidAccountFieldsLinkToErrorsAndExposeInvalidState()
    {
        await using var factory = new RentalFactory();
        using var browser = factory.Browser();
        var first = await browser.GetStringAsync("/Account/Login");
        Assert.Contains("aria-describedby=\"Email-validation\"", first);
        Assert.Contains("id=\"Email-validation\"", first);
        Assert.DoesNotContain("aria-invalid=\"true\"", first);
        using var invalid = await Post(browser, "/Account/Login", new Dictionary<string, string>
            { ["Email"] = "invalid", ["Password"] = "" });
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        var html = await invalid.Content.ReadAsStringAsync();
        Assert.Contains("aria-invalid=\"true\"", html);
        Assert.Contains("aria-describedby=\"Password-validation\"", html);
    }

    [Fact]
    public async Task ScrollRegionsAndModalSaveStatusHaveAccessibleNames()
    {
        await using var factory = new RentalFactory();
        using var browser = await factory.Register("frontend@example.test", AppRoles.PropertyManager);
        var applications = await browser.GetStringAsync("/Applications");
        Assert.Contains("tabindex=\"0\" role=\"region\" aria-label=\"Applications\"", applications);
        // Filters are not validated fields and should not point to nonexistent error spans.
        Assert.DoesNotContain("aria-describedby=\"Status-validation\"", applications);
        var modal = await browser.GetStringAsync("/Properties/Edit");
        Assert.Contains("aria-describedby=\"Name-validation\"", modal);
        Assert.Contains("id=\"Name-validation\"", modal);
        Assert.Contains("modal-save-status", modal);
        Assert.Contains("aria-live=\"polite\"", modal);
    }

}