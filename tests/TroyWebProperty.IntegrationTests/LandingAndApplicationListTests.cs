using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TroyWebProperty.Core;
using TroyWebProperty.Core.Entities;
using Xunit;
using static TroyWebProperty.IntegrationTests.RentalFactory;

// ReSharper disable MemberCanBeFileLocal

namespace TroyWebProperty.IntegrationTests;

public sealed class LandingAndApplicationListTests
{

    [Fact]
    public async Task AnonymousHomeIsGenericAndApplicationListRequiresLogin()
    {
        await using var factory = new RentalFactory();
        using var browser = factory.Browser();
        var home = await browser.GetStringAsync("/");
        Assert.Contains("Welcome to your next home", home);
        Assert.Contains("Create an account", home);
        Assert.Contains("Log in", home);
        Assert.DoesNotContain("Applications by status", home);
        Assert.DoesNotContain("Rental sample data", home);
        Assert.DoesNotContain("<table", home);
        Assert.DoesNotContain("/Applications/Edit/", home);
        var protectedPage = await browser.GetAsync("/Applications");
        Assert.Equal(HttpStatusCode.Redirect, protectedPage.StatusCode);
        Assert.Contains("/Account/Login", Assert.IsType<Uri>(protectedPage.Headers.Location).OriginalString);
    }

    [Theory]
    [InlineData(AppRoles.Applicant)]
    [InlineData(AppRoles.PropertyManager)]
    public async Task LoginRegistrationAndAuthenticatedHomeLeadToApplications(string role)
    {
        await using var factory = new RentalFactory();
        using var browser = factory.Browser();
        var registration = await Post(browser, "/Account/Register", new Dictionary<string, string>
        {
            ["FirstName"] = "Landing User", ["LastName"] = "Applicant", ["Email"] = "landing@example.test",
            ["Role"] = role,
            ["Password"] = Password, ["ConfirmPassword"] = Password
        });
        Assert.Equal("/Applications", registration.Headers.Location?.OriginalString);
        var home = await browser.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, home.StatusCode);
        Assert.Equal("/Applications", home.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync(home.Headers.Location)).StatusCode);
        await Post(browser, "/Account/Logout", new Dictionary<string, string>(), "/Applications");
        var login = await Post(browser, "/Account/Login", new Dictionary<string, string>
        {
            ["Email"] = "landing@example.test", ["Password"] = Password
        });
        Assert.Equal("/Applications", login.Headers.Location?.OriginalString);
    }

    [Theory]
    [InlineData(AppRoles.Applicant)]
    [InlineData(AppRoles.PropertyManager)]
    public async Task ApplicationListFiltersInSqlAndScopesRowsByRole(string role)
    {
        await using var factory = new RentalFactory();
        using var applicant = await factory.Register("list-owner@example.test", AppRoles.Applicant);
        using var other = await factory.Register("list-other@example.test", AppRoles.Applicant);
        using var manager = await factory.Register("list-manager@example.test", AppRoles.PropertyManager);
        var data = await factory.Read(static async db =>
        {
            var ownerId = await db.Applicants.Where(static x => x.Email == "list-owner@example.test")
                .Select(static x => x.Id)
                .SingleAsync();
            var otherId = await db.Applicants.Where(static x => x.Email == "list-other@example.test")
                .Select(static x => x.Id)
                .SingleAsync();
            var units = await db.Units.OrderBy(static x => x.PropertyId).ToListAsync();
            var first = units[0];
            var second = units.First(x => x.PropertyId != first.PropertyId);
            var draftId = await db.ApplicationStatuses.Where(static x => x.Code == "Draft").Select(static x => x.Id)
                .SingleAsync();
            var submittedId = await db.ApplicationStatuses.Where(static x => x.Code == "Submitted")
                .Select(static x => x.Id)
                .SingleAsync();
            var baseline = await db.Applications
                .Where(x => x.Status.Code == "Draft" && x.Unit.PropertyId == first.PropertyId).Select(static x => x.Id)
                .ToListAsync();
            var own = new RentalApplication
            {
                ApplicantId = ownerId, UnitId = first.Id, StatusId = draftId, FirstName = "Own matching application",
                LastName = "Applicant",
                CreatedAtUtc = DateTime.UtcNow
            };
            var another = new RentalApplication
            {
                ApplicantId = otherId, UnitId = first.Id, StatusId = draftId,
                FirstName = "Another applicant's matching application", LastName = "Applicant",
                CreatedAtUtc = DateTime.UtcNow
            };
            db.AddRange(own, another,
                new RentalApplication
                {
                    ApplicantId = ownerId, UnitId = first.Id, StatusId = submittedId, FirstName = "Excluded by status",
                    LastName = "Applicant",
                    CreatedAtUtc = DateTime.UtcNow
                },
                new RentalApplication
                {
                    ApplicantId = ownerId, UnitId = second.Id, StatusId = draftId, FirstName = "Excluded by property",
                    LastName = "Applicant",
                    CreatedAtUtc = DateTime.UtcNow
                });
            await db.SaveChangesAsync();

            return (first.PropertyId, OwnId: own.Id, OtherId: another.Id, Baseline: baseline);
        });

        factory.Sql.Commands.Clear();
        var browser = role == AppRoles.Applicant ? applicant : manager;
        var page = await browser.GetStringAsync($"/Applications?status=Draft&propertyId={data.PropertyId}");
        var actual = Regex.Matches(page, "href=\"/Applications/Edit/(\\d+)\"")
            .Select(static x => int.Parse(x.Groups[1].Value)).Order().ToArray();
        var expected = role == AppRoles.Applicant
            ? new[] { data.OwnId }
            : data.Baseline.Concat([data.OwnId, data.OtherId]).Order().ToArray();
        Assert.Equal(expected, actual);
        Assert.DoesNotContain("Excluded by status", page);
        Assert.DoesNotContain("Excluded by property", page);

        var sql = Assert.Single(factory.Sql.Commands,
            static x => x.Contains("\"CreatedAtUtc\"") && x.Contains("FROM \"Applications\""));
        var where = sql[sql.IndexOf("WHERE", StringComparison.Ordinal)..];
        Assert.Contains("\"Code\"", where);
        Assert.Contains("\"PropertyId\"", where);
        Assert.Contains("@", where); // Values are sent as database parameters.
        Assert.Equal(role == AppRoles.Applicant, where.Contains("\"UserId\""));
    }

}