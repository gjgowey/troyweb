using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TroyWebProperty.Core;
using TroyWebProperty.Infrastructure;
using Xunit;
using static TroyWebProperty.IntegrationTests.RentalFactory;

namespace TroyWebProperty.IntegrationTests;

public sealed class ViewTemplateTests
{

    [Fact]
    public async Task SharedNameEditorPreservesPrefixesErrorsAndValues_AndSummaryUsesReadOnlyDisplay()
    {
        await using var factory = new RentalFactory();
        using var browser = await factory.Register("templates@example.test", AppRoles.Applicant);
        var registration = await browser.GetStringAsync("/Account/Register");
        AssertNameFields(registration, "");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var unit = await factory.Read(today, static (db, state) =>
            new RentalRepository(db).AvailableUnits(state)
                .Select(static x => x.Id).FirstAsync());
        var start = await Post(browser, "/Applications/Start",
            new Dictionary<string, string> { ["unitId"] = unit.ToString() }, "/Units");
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        var location = start.Headers.Location;
        Assert.NotNull(location);
        var id = int.Parse(location.OriginalString.Split('/').Last());
        var edit = await browser.GetStringAsync($"/Applications/Edit/{id}?step=1");
        AssertNameFields(edit, "Info.");
        var invalid = await Wizard(browser, id, 1, "Continue", new Dictionary<string, string>
        {
            ["Info.Title"] = "Dr.", ["Info.FirstName"] = "<b>Ada</b>", ["Info.LastName"] = "",
            ["Info.MiddleInitial"] = "B", ["Info.Phone"] = "202-555-0102",
            ["Info.Email"] = "ada@example.test", ["Info.CurrentAddress"] = "10 Main Street"
        });
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        var errors = await invalid.Content.ReadAsStringAsync();
        Assert.Equal("<b>Ada</b>", Field(errors, "Info.FirstName"));
        Assert.DoesNotContain("<b>Ada</b>", errors);
        Assert.Contains("The Last name field is required.", errors);
        var saved = await Wizard(browser, id, 1, "Continue", new Dictionary<string, string>
        {
            ["Info.Title"] = "Dr.", ["Info.FirstName"] = "Ada", ["Info.LastName"] = "Lovelace",
            ["Info.MiddleInitial"] = "B", ["Info.Phone"] = "202-555-0102",
            ["Info.Email"] = "ada@example.test", ["Info.CurrentAddress"] = "10 Main Street"
        });
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        var summary = await browser.GetStringAsync($"/Applications/Edit/{id}?step=3");
        Assert.Contains("class=\"detail-list\"", summary);
        Assert.Contains("Dr. Ada B. Lovelace", summary);
        Assert.DoesNotContain("name=\"Info.FirstName\"", summary);
        var list = await browser.GetStringAsync("/Applications");
        Assert.Contains("<time datetime=", list);
        Assert.Contains("status-badge", list);
    }

    private static void AssertNameFields(string html, string prefix)
    {
        foreach (var name in
                 new[] { "Title", "FirstName", "MiddleInitial", "LastName" }.Select(field => prefix + field))
        {
            Assert.Single(Regex.Matches(html, "name=\"" + Regex.Escape(name) + "\""));
            Assert.Contains("data-valmsg-for=\"" + name + "\"", html);
            Assert.Contains("aria-describedby=\"" + name.Replace('.', '_') + "-validation\"", html);
        }

        Assert.Contains("autocomplete=\"honorific-prefix\"", html);
        Assert.Contains("data-val-required=", html);
    }

}