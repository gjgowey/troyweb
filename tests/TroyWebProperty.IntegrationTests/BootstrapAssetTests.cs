using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace TroyWebProperty.IntegrationTests;

public sealed class BootstrapAssetTests
{

    [Fact]
    public async Task CdnAndLocalFallbackAssetsShareIntegrityAndLoadBeforeApplicationScript()
    {
        await using var factory = new RentalFactory();
        using var browser = factory.Browser();
        var html = await browser.GetStringAsync("/");
        const string css = "sha384-sRIl4kxILFvY47J16cr9ZwB07vP4J8+LH7qKQnuqkuIAvNWLzeN8tE5YBujZqJLB";
        const string js = "sha384-FKyoEForCGlyvwx9Hj09JcYn3nv7wiPVlz7YYwJrWVcXK/BmnVDxM+D2scQbITxI";
        Assert.Contains("https://cdn.jsdelivr.net/npm/bootstrap@5.3.8/dist/css/bootstrap.min.css", html);
        Assert.Contains("https://cdn.jsdelivr.net/npm/bootstrap@5.3.8/dist/js/bootstrap.bundle.min.js", html);
        Assert.Contains(css, html);
        Assert.Contains(js, html);
        Assert.Contains("crossorigin=\"anonymous\"", html);
        Assert.Contains("referrerpolicy=\"no-referrer\"", html);
        Assert.Contains("/lib/bootstrap/dist/css/bootstrap.min.css", html);
        Assert.Contains("/lib/bootstrap/dist/js/bootstrap.bundle.min.js", html);
        Assert.Contains("window.bootstrap.Modal", html);
        Assert.Contains("window.bootstrap.Collapse", html);
        Assert.Contains("visually-hidden", html);
        Assert.DoesNotContain("asp-fallback", html);
        Assert.True(html.IndexOf("bootstrap.bundle.min.js", StringComparison.Ordinal) <
                    html.IndexOf("/js/site.js", StringComparison.Ordinal));
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !Directory.Exists(Path.Combine(directory.FullName, "TroyWebProperty.Web", "wwwroot")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var root = Path.Combine(directory.FullName, "TroyWebProperty.Web", "wwwroot", "lib", "bootstrap", "dist");
        Assert.Equal(css,
            "sha384-" + Convert.ToBase64String(
                SHA384.HashData(await File.ReadAllBytesAsync(Path.Combine(root, "css", "bootstrap.min.css")))));
        Assert.Equal(js,
            "sha384-" + Convert.ToBase64String(
                SHA384.HashData(await File.ReadAllBytesAsync(Path.Combine(root, "js", "bootstrap.bundle.min.js")))));
    }

    [Fact]
    public async Task EveryClientLibraryHasMatchingLocalBytesAndIsLoadedOnceInDependencyOrder()
    {
        await using var factory = new RentalFactory();
        using var browser = factory.Browser();
        var html = await browser.GetStringAsync("/Account/Register");
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "client-assets.json")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        using var manifest =
            JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory.FullName, "client-assets.json")));

        foreach (var asset in manifest.RootElement.EnumerateArray())
        {
            var url = Assert.IsType<string>(asset.GetProperty("Url").GetString());
            var file = Assert.IsType<string>(asset.GetProperty("File").GetString());
            var hash = Assert.IsType<string>(asset.GetProperty("Hash").GetString());
            Assert.Contains(url, html);
            Assert.Contains(hash, html);
            Assert.Contains("/lib/" + file, html);
            var bytes = await File.ReadAllBytesAsync(Path.Combine(directory.FullName, "TroyWebProperty.Web", "wwwroot",
                "lib", file));
            Assert.Equal(hash, "sha384-" + Convert.ToBase64String(SHA384.HashData(bytes)));
            Assert.Single(Regex.Matches(html, "(?:src|href)=\"" + Regex.Escape(url) + "\""));
        }

        var jquery = html.IndexOf("jquery@3.7.1", StringComparison.Ordinal);
        var validation = html.IndexOf("jquery-validation@1.22.1", StringComparison.Ordinal);
        var unobtrusive = html.IndexOf("jquery-validation-unobtrusive@4.0.0", StringComparison.Ordinal);
        var bootstrap = html.IndexOf("/js/bootstrap.bundle.min.js", StringComparison.Ordinal);
        var site = html.IndexOf("/js/site.js", StringComparison.Ordinal);
        Assert.True(jquery < validation && validation < unobtrusive && unobtrusive < bootstrap && bootstrap < site);
        Assert.Contains("window.jQuery.validator.unobtrusive", html);
    }

}