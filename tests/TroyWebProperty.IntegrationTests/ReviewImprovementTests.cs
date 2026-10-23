using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TroyWebProperty.Core;
using TroyWebProperty.Core.Entities;
using Xunit;

namespace TroyWebProperty.IntegrationTests;

public sealed class ReviewImprovementTests
{

    [Fact]
    public async Task PagingIsBoundedFilteredAndStableForEqualCreationDates()
    {
        await using var factory = new RentalFactory();
        using var browser = await factory.Register("paging@example.test", AppRoles.Applicant);
        var data = await factory.Read(static async db =>
        {
            var owner = await db.Applicants.SingleAsync(static x => x.Email == "paging@example.test");
            var unit = await db.Units.OrderBy(static x => x.Id).FirstAsync();
            var status = await db.ApplicationStatuses.SingleAsync(static x => x.Code == "Draft");
            var rows = Enumerable.Range(0, 30).Select(_ => new RentalApplication
            {
                ApplicantId = owner.Id, UnitId = unit.Id, StatusId = status.Id,
                FirstName = "Paging", LastName = "Applicant", CreatedAtUtc = new DateTime(2030, 1, 1)
            }).ToArray();
            db.Applications.AddRange(rows);
            await db.SaveChangesAsync();

            return (unit.PropertyId, Ids: rows.Select(static x => x.Id).OrderDescending().ToArray());
        });
        factory.Sql.Commands.Clear();
        var path = $"/Applications?status=Draft&propertyId={data.PropertyId}";
        var first = await browser.GetStringAsync(path);
        var second = await browser.GetStringAsync(path + "&page=2");
        Assert.Equal(data.Ids.Take(25), Ids(first));
        Assert.Equal(data.Ids.Skip(25), Ids(second));
        Assert.Contains("30 results", first);
        Assert.Contains("aria-label=\"Application pages\"", first);
        Assert.Contains("status=Draft", first);
        Assert.Contains($"propertyId={data.PropertyId}", first);
        Assert.Equal(Ids(second), Ids(await browser.GetStringAsync(path + "&page=2147483647")));
        Assert.Equal(Ids(first), Ids(await browser.GetStringAsync(path + "&page=-1")));
        var commands = factory.Sql.Commands.Where(static x => x.Contains("LIMIT") && x.Contains("CreatedAtUtc"));
        var cmdArr = commands as string[] ?? [.. commands];
        Assert.NotEmpty(cmdArr);
        // ReSharper disable once ParameterOnlyUsedForPreconditionCheck
        Assert.All(cmdArr, static sql =>
        {
            Assert.Contains("OFFSET", sql);
            Assert.Contains("UserId", sql);
        });
    }

    [Fact]
    public async Task PersonalPagesPreventCachingAndDetailCollectionsLoadSeparately()
    {
        await using var factory = new RentalFactory();
        using var browser = await factory.Register("review-manager@example.test", AppRoles.PropertyManager);
        var id = await factory.Read(static db => db.Applications.Select(static x => x.Id).FirstAsync());
        factory.Sql.Commands.Clear();
        using var detail = await browser.GetAsync($"/Applications/Edit/{id}");
        detail.EnsureSuccessStatusCode();
        Assert.True(detail.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain(factory.Sql.Commands, static sql =>
            sql.Contains("JOIN \"Residences\"") && sql.Contains("JOIN \"StatusChanges\""));
        Assert.Contains(factory.Sql.Commands, static sql => sql.Contains("JOIN \"Residences\""));
        using var properties = await browser.GetAsync("/Properties");
        Assert.True(properties.Headers.CacheControl?.NoStore);
        using var login = await browser.GetAsync("/Account/Login");
        Assert.True(login.Headers.CacheControl?.NoStore);
    }

    private static int[] Ids(string html)
    {
        return
        [
            .. Regex.Matches(html, "href=\"/Applications/Edit/(\\d+)\"")
                .Select(static x => int.Parse(x.Groups[1].Value))
        ];
    }

}