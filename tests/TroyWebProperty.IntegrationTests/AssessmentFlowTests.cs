using System.Net;
using Microsoft.EntityFrameworkCore;
using TroyWebProperty.Core;
using TroyWebProperty.Infrastructure;
using TroyWebProperty.Infrastructure.Data;
using Xunit;
using static TroyWebProperty.IntegrationTests.RentalFactory;

namespace TroyWebProperty.IntegrationTests;

public sealed class AssessmentFlowTests
{

    [Theory]
    [InlineData(AppRoles.Applicant)]
    [InlineData(AppRoles.PropertyManager)]
    public async Task RegistrationCreatesSelectedRoleAndLinkedProfile(string role)
    {
        await using var factory = new RentalFactory();
        using var browser = await factory.Register("signup@example.test", role);
        var roles = await factory.Read(static db =>
            (from u in db.Users
                join ur in db.UserRoles on u.Id equals ur.UserId
                join r in db.Roles on ur.RoleId equals r.Id
                where u.Email == "signup@example.test"
                select r.Name).ToListAsync());
        Assert.Equal(new[] { role }, roles);
        Assert.Equal(2, await factory.Read(static db => db.Roles.CountAsync()));
        Assert.True(await factory.Read(role, static (db, state) =>
            state == AppRoles.Applicant
                ? db.Applicants.AnyAsync(static x => x.Email == "signup@example.test")
                : db.PropertyManagers.AnyAsync(static x => x.Email == "signup@example.test")));
        var home = await browser.GetStringAsync("/Applications");
        Assert.Equal(role == AppRoles.PropertyManager, home.Contains("Properties &amp; units"));
        var properties = await browser.GetAsync("/Properties");
        Assert.Equal(role == AppRoles.PropertyManager ? HttpStatusCode.OK : HttpStatusCode.Redirect,
            properties.StatusCode);
    }

    [Fact]
    public async Task UnknownRoleIsRejectedAndAccountFormsRequireAntiforgery()
    {
        await using var factory = new RentalFactory();
        using var browser = factory.Browser();
        var response = await Post(browser, "/Account/Register",
            new Dictionary<string, string>
            {
                ["FirstName"] = "Test", ["LastName"] = "Applicant", ["Email"] = "bad@example.test",
                ["Password"] = Password,
                ["ConfirmPassword"] = Password, ["Role"] = "Administrator"
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(await factory.Read(static db => db.Users.AnyAsync(static x => x.Email == "bad@example.test")));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await browser.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>())))
            .StatusCode);
    }

    [Fact]
    public async Task AnonymousUsersAreChallengedAndLoginLogoutUseRealCookies()
    {
        await using var factory = new RentalFactory();
        using var browser = await factory.Register("login@example.test", AppRoles.Applicant);
        var logout = await Post(browser, "/Account/Logout", new Dictionary<string, string>(), "/Applications");
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Contains("/Account/Login",
            Assert.IsType<Uri>((await browser.GetAsync("/Applications")).Headers.Location).OriginalString);
        var login = await Post(browser, "/Account/Login",
            new Dictionary<string, string>
            {
                ["Email"] = "login@example.test", ["Password"] = Password, ["ReturnUrl"] = "https://evil.example"
            });
        Assert.Equal("/Applications", Assert.IsType<Uri>(login.Headers.Location).OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/Applications")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync("/Account/Logout")).StatusCode);
    }

    [Fact]
    public async Task ApplicationOwnershipIsEnforcedOnPagesAndModalEndpoints()
    {
        await using var factory = new RentalFactory();
        using var owner = await factory.Register("owner@example.test", AppRoles.Applicant);
        using var other = await factory.Register("other@example.test", AppRoles.Applicant);
        var id = await Start(factory, owner);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/Applications/Edit/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await other.GetAsync($"/Applications/Residence?applicationId={id}")).StatusCode);
        Assert.DoesNotContain($"/Applications/Edit/{id}\"", await other.GetStringAsync("/Applications"));
    }

    [Fact]
    public async Task FullWorkflow_ValidatesSections_ReturnsResubmitsApprovesAndPreventsSecondLease()
    {
        await using var factory = new RentalFactory();
        using var applicant = await factory.Register("workflow@example.test", AppRoles.Applicant);
        using var manager = await factory.Register("manager@example.test", AppRoles.PropertyManager);
        var id = await Start(factory, applicant);
        var invalid = await Wizard(applicant, id, 1, "Continue");
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        Assert.False(await factory.Read(id, static (db, state) =>
            db.Applications.Where(x => x.Id == state).Select(static x => x.InformationSaved).SingleAsync()));
        Assert.Equal(HttpStatusCode.Redirect, (await Wizard(applicant, id, 1, "Continue", Info())).StatusCode);
        var history = await Wizard(applicant, id, 2, "Continue");
        Assert.Contains("at least one prior residence", await history.Content.ReadAsStringAsync());
        var modalPath = $"/Applications/Residence?applicationId={id}";
        var html = await applicant.GetStringAsync(modalPath);
        var residence = new Dictionary<string, string>
        {
            ["ApplicationId"] = id.ToString(), ["RowVersion"] = Field(html, "RowVersion"),
            ["Address"] = "12 Prior Road",
            ["LandlordName"] = "Prior Landlord", ["LandlordPhone"] = "202-555-0101", ["MoveIn"] = "2020-01-01",
            ["MoveOut"] = "2024-01-01"
        };
        Assert.Equal(HttpStatusCode.OK,
            (await Post(applicant, "/Applications/Residence", residence, modalPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Wizard(applicant, id, 2, "Continue")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Wizard(applicant, id, 3, "Submit")).StatusCode);
        var reviewPath = $"/Applications/Review/{id}";
        html = await manager.GetStringAsync(reviewPath);
        var review = new Dictionary<string, string>
        {
            ["Id"] = id.ToString(), ["RowVersion"] = Field(html, "RowVersion"), ["Outcome"] = "Returned",
            ["StartDate"] = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")
        };
        Assert.Equal((HttpStatusCode)422, (await Post(manager, "/Applications/Review", review, reviewPath)).StatusCode);
        review["Comment"] = "Please correct your address.";
        Assert.Equal(HttpStatusCode.OK, (await Post(manager, "/Applications/Review", review, reviewPath)).StatusCode);
        Assert.Contains("Please correct your address.", await applicant.GetStringAsync($"/Applications/Edit/{id}"));
        Assert.Equal(HttpStatusCode.Redirect, (await Wizard(applicant, id, 1, "Continue", Info())).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Wizard(applicant, id, 2, "Continue")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Wizard(applicant, id, 3, "Submit")).StatusCode);
        html = await manager.GetStringAsync(reviewPath);
        review["RowVersion"] = Field(html, "RowVersion");
        review["Outcome"] = "Approved";
        Assert.Equal(HttpStatusCode.OK, (await Post(manager, "/Applications/Review", review, reviewPath)).StatusCode);
        var lease = await factory.Read(id,
            static (db, state) => db.Leases.SingleAsync(x => x.RentalApplicationId == state));
        Assert.Equal(lease.StartDate.AddMonths(12), lease.EndDate);
        Assert.Equal("Approved",
            await factory.Read(id,
                static (db, state) => db.Applications.Where(x => x.Id == state).Select(static x => x.Status.Code)
                    .SingleAsync()));
        Assert.Equal(HttpStatusCode.Conflict, (await Wizard(applicant, id, 1, "Continue", Info())).StatusCode);
        var otherId = await factory.Read((id, lease), static (db, state) =>
            db.Applications
                .Where(x => x.UnitId == state.lease.UnitId && x.Id != state.id && x.Status.Code == "Submitted")
                .Select(static x => x.Id).FirstAsync());
        var otherReview = $"/Applications/Review/{otherId}";
        html = await manager.GetStringAsync(otherReview);
        review["Id"] = otherId.ToString();
        review["RowVersion"] = Field(html, "RowVersion");
        Assert.Equal((HttpStatusCode)422,
            (await Post(manager, "/Applications/Review", review, otherReview)).StatusCode);
        Assert.Equal(1,
            await factory.Read(lease, static (db, state) => db.Leases.CountAsync(x => x.UnitId == state.UnitId)));
        Assert.Equal("Submitted",
            await factory.Read(otherId, static (db, state) =>
                db.Applications.Where(x => x.Id == state).Select(static x => x.Status.Code).SingleAsync()));
        Assert.Contains("Status and review history", await manager.GetStringAsync($"/Applications/Edit/{id}"));
        Assert.DoesNotContain("Status and review history", await applicant.GetStringAsync($"/Applications/Edit/{id}"));
        var available = await applicant.GetStringAsync("/Units");
        Assert.DoesNotContain($"name=\"unitId\" value=\"{lease.UnitId}\"", available);
    }

    [Fact]
    public async Task PropertyAndUnitModals_ReturnValidationAndSupportCrudAndInactiveTypes()
    {
        await using var factory = new RentalFactory();
        using var manager = await factory.Register("crud@example.test", AppRoles.PropertyManager);
        Assert.Equal((HttpStatusCode)422,
            (await Post(manager, "/Properties/Edit", new Dictionary<string, string>())).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await Post(manager, "/Properties/Edit",
                new Dictionary<string, string>
                    { ["Name"] = "Test Property", ["Address"] = "10 Test Road" })).StatusCode);
        var propertyId = await factory.Read(static db =>
            db.Properties.Where(static x => x.Name == "Test Property").Select(static x => x.Id).SingleAsync());
        var form = new Dictionary<string, string>
        {
            ["PropertyId"] = propertyId.ToString(), ["Number"] = "1", ["Bedrooms"] = "2", ["MonthlyRent"] = "1500",
            ["UnitTypeId"] =
                (await factory.Read(static db =>
                    db.UnitTypes.Where(static x => x.Code == "Loft").Select(static x => x.Id).SingleAsync()))
                .ToString()
        };
        var path = $"/Units/Edit?propertyId={propertyId}";
        var rejected = await Post(manager, "/Units/Edit", form, path);
        Assert.Equal((HttpStatusCode)422, rejected.StatusCode);
        Assert.Contains("inactive", await rejected.Content.ReadAsStringAsync());
        form["UnitTypeId"] =
            (await factory.Read(static db =>
                db.UnitTypes.Where(static x => x.Code == "Apartment").Select(static x => x.Id).SingleAsync()))
            .ToString();
        Assert.Equal(HttpStatusCode.OK, (await Post(manager, "/Units/Edit", form, path)).StatusCode);
        var unitId = await factory.Read(propertyId, static (db, state) =>
            db.Units.Where(x => x.PropertyId == state).Select(static x => x.Id).SingleAsync());
        form["Id"] = unitId.ToString();
        form["MonthlyRent"] = "1700";
        Assert.Equal(HttpStatusCode.OK, (await Post(manager, "/Units/Edit", form, $"/Units/Edit/{unitId}")).StatusCode);
        Assert.Equal(1700m,
            await factory.Read(unitId,
                static (db, state) =>
                    db.Units.Where(x => x.Id == state).Select(static x => x.MonthlyRent).SingleAsync()));
        Assert.Equal(HttpStatusCode.OK,
            (await Post(manager, "/Units/Edit",
                new Dictionary<string, string> { ["Id"] = unitId.ToString(), ["Delete"] = "true" },
                $"/Units/Edit/{unitId}?delete=true")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await Post(manager, "/Properties/Edit",
                new Dictionary<string, string> { ["Id"] = propertyId.ToString(), ["Delete"] = "true" },
                $"/Properties/Edit/{propertyId}?delete=true")).StatusCode);
    }

    [Fact]
    public async Task SeedIsIdempotentAndApplicationsCoverEveryRequiredStatus()
    {
        await using var factory = new RentalFactory();
        using var browser = factory.Browser();
        await browser.GetAsync("/");
        var counts = await factory.Read(static async db => new[]
        {
            await db.Applications.CountAsync(), await db.Properties.CountAsync(), await db.Users.CountAsync(),
            await db.Leases.CountAsync()
        });
        await factory.Read(static async db =>
        {
            await DatabaseSeeder.SeedAsync(db);

            return true;
        });
        Assert.Equal(counts,
            await factory.Read(static async db => new[]
            {
                await db.Applications.CountAsync(), await db.Properties.CountAsync(), await db.Users.CountAsync(),
                await db.Leases.CountAsync()
            }));
        Assert.Equal(ApplicationRules.Statuses.Order(),
            (await factory.Read(static db =>
                db.Applications.Select(static x => x.Status.Code).Distinct().ToListAsync())).Order());
        Assert.Equal(4, counts[3]);
    }

    [Fact]
    public async Task BackDoesNotSave_ResidenceModalsEditAndDelete_AndWithdrawalIsTerminal()
    {
        await using var factory = new RentalFactory();
        using var applicant = await factory.Register("changes@example.test", AppRoles.Applicant);
        var id = await Start(factory, applicant);
        var edits = Info();
        edits["Info.FirstName"] = "Must not be saved";
        edits["Info.LastName"] = "Applicant";
        Assert.Equal(HttpStatusCode.Redirect, (await Wizard(applicant, id, 2, "Back", edits)).StatusCode);
        Assert.NotEqual("Must not be saved",
            await factory.Read(id,
                static (db, state) =>
                    db.Applications.Where(x => x.Id == state).Select(static x => x.FirstName).SingleAsync()));
        await Wizard(applicant, id, 1, "Continue", Info());
        var modal = $"/Applications/Residence?applicationId={id}";
        var html = await applicant.GetStringAsync(modal);
        var fields = new Dictionary<string, string>
        {
            ["ApplicationId"] = id.ToString(), ["RowVersion"] = Field(html, "RowVersion"), ["Address"] = "Old address",
            ["LandlordName"] = "Landlord", ["LandlordPhone"] = "202-555-0100", ["MoveIn"] = "2020-01-01",
            ["MoveOut"] = "2019-01-01"
        };
        Assert.Equal((HttpStatusCode)422, (await Post(applicant, "/Applications/Residence", fields, modal)).StatusCode);
        fields["MoveOut"] = "2023-01-01";
        Assert.Equal(HttpStatusCode.OK, (await Post(applicant, "/Applications/Residence", fields, modal)).StatusCode);
        var residenceId = await factory.Read(id, static (db, state) =>
            db.Residences.Where(x => x.RentalApplicationId == state).Select(static x => x.Id).SingleAsync());
        modal += $"&id={residenceId}";
        html = await applicant.GetStringAsync(modal);
        fields["Id"] = residenceId.ToString();
        fields["RowVersion"] = Field(html, "RowVersion");
        fields["Address"] = "Corrected address";
        Assert.Equal(HttpStatusCode.OK, (await Post(applicant, "/Applications/Residence", fields, modal)).StatusCode);
        Assert.Equal("Corrected address",
            await factory.Read(residenceId, static (db, state) =>
                db.Residences.Where(x => x.Id == state).Select(static x => x.Address).SingleAsync()));
        html = await applicant.GetStringAsync(modal);
        fields["RowVersion"] = Field(html, "RowVersion");
        fields["Delete"] = "true";
        Assert.Equal(HttpStatusCode.OK, (await Post(applicant, "/Applications/Residence", fields, modal)).StatusCode);
        Assert.False(await factory.Read(residenceId, static (db, state) => db.Residences.AnyAsync(x => x.Id == state)));
        html = await applicant.GetStringAsync($"/Applications/Edit/{id}");
        Assert.Equal(HttpStatusCode.Redirect,
            (await Post(applicant, "/Applications/Withdraw",
                new Dictionary<string, string> { ["id"] = id.ToString(), ["rowVersion"] = Field(html, "RowVersion") },
                $"/Applications/Edit/{id}"))
            .StatusCode);
        Assert.Equal("Withdrawn",
            await factory.Read(id,
                static (db, state) => db.Applications.Where(x => x.Id == state).Select(static x => x.Status.Code)
                    .SingleAsync()));
        Assert.Equal(HttpStatusCode.Conflict,
            (await applicant.GetAsync($"/Applications/Residence?applicationId={id}")).StatusCode);
    }

    [Fact]
    public async Task FiltersAreAppliedAndInactiveExistingTypeRemainsUsable()
    {
        await using var factory = new RentalFactory();
        using var manager = await factory.Register("filter@example.test", AppRoles.PropertyManager);
        var propertyId = await factory.Read(static db =>
            db.Properties.OrderBy(static x => x.Id).Select(static x => x.Id).FirstAsync());
        var html = await manager.GetStringAsync($"/Applications?status=Draft&propertyId={propertyId}");
        var expected = await factory.Read(propertyId, static (db, state) =>
            db.Applications.Where(x => x.Status.Code == "Draft" && x.Unit.PropertyId == state).Select(static x => x.Id)
                .ToListAsync());
        foreach (var id in expected) Assert.Contains($"/Applications/Edit/{id}\"", html);
        var excluded = await factory.Read(propertyId, static (db, state) =>
            db.Applications.Where(x => x.Status.Code != "Draft" || x.Unit.PropertyId != state).Select(static x => x.Id)
                .ToListAsync());
        foreach (var id in excluded) Assert.DoesNotContain($"/Applications/Edit/{id}\"", html);
        var unit = await factory.Read(static db => db.Units.FirstAsync(static x => x.UnitType.Code == "Loft"));
        var modal = await manager.GetStringAsync($"/Units/Edit/{unit.Id}");
        Assert.Contains("Loft (retired)", modal);
        var response = await Post(manager, "/Units/Edit",
            new Dictionary<string, string>
            {
                ["Id"] = unit.Id.ToString(), ["PropertyId"] = unit.PropertyId.ToString(), ["Number"] = unit.Number,
                ["Bedrooms"] = unit.Bedrooms.ToString(), ["MonthlyRent"] = "1234",
                ["UnitTypeId"] =
                    (await factory.Read(static db =>
                        db.UnitTypes.Where(static x => x.Code == "Loft").Select(static x => x.Id).SingleAsync()))
                    .ToString()
            }, $"/Units/Edit/{unit.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Loft",
            await factory.Read(unit,
                static (db, state) =>
                    db.Units.Where(x => x.Id == state.Id).Select(static x => x.UnitType.Code).SingleAsync()));
    }

    [Fact]
    public async Task StaleApplicationPostIsRejectedRatherThanOverwriting()
    {
        await using var factory = new RentalFactory();
        using var applicant = await factory.Register("stale@example.test", AppRoles.Applicant);
        var id = await Start(factory, applicant);
        var old = await applicant.GetStringAsync($"/Applications/Edit/{id}");
        await Wizard(applicant, id, 1, "Continue", Info());
        var fields = Info();
        fields["Id"] = id.ToString();
        fields["Step"] = "1";
        fields["RowVersion"] = Field(old, "RowVersion");
        fields["command"] = "Continue";
        fields["Info.FirstName"] = "Stale change";
        fields["Info.LastName"] = "Applicant";
        var response = await Post(applicant, "/Applications/Edit", fields, $"/Applications/Edit/{id}");
        Assert.Contains("Reload the page", await response.Content.ReadAsStringAsync());
        Assert.NotEqual("Stale change",
            await factory.Read(id,
                static (db, state) =>
                    db.Applications.Where(x => x.Id == state).Select(static x => x.FirstName).SingleAsync()));
    }

    [Fact]
    public async Task DenialRequiresComment_RecordsHistory_AndDoesNotCreateLease()
    {
        await using var factory = new RentalFactory();
        using var manager = await factory.Register("denial@example.test", AppRoles.PropertyManager);
        var id = await factory.Read(static db =>
            db.Applications.Where(static x => x.Status.Code == "Submitted").Select(static x => x.Id).FirstAsync());
        var path = $"/Applications/Review/{id}";
        var html = await manager.GetStringAsync(path);
        var fields = new Dictionary<string, string>
        {
            ["Id"] = id.ToString(), ["RowVersion"] = Field(html, "RowVersion"), ["Outcome"] = "Denied",
            ["StartDate"] = "2026-10-20"
        };
        Assert.Equal((HttpStatusCode)422, (await Post(manager, "/Applications/Review", fields, path)).StatusCode);
        fields["Comment"] = "Incomplete eligibility documentation.";
        Assert.Equal(HttpStatusCode.OK, (await Post(manager, "/Applications/Review", fields, path)).StatusCode);
        Assert.Equal("Denied",
            await factory.Read(id,
                static (db, state) => db.Applications.Where(x => x.Id == state).Select(static x => x.Status.Code)
                    .SingleAsync()));
        Assert.False(await factory.Read(id,
            static (db, state) => db.Leases.AnyAsync(x => x.RentalApplicationId == state)));
        Assert.True(await factory.Read(id, static (db, state) => db.StatusChanges.AnyAsync(x =>
            x.RentalApplicationId == state && x.ToStatus == "Denied" &&
            x.Comment == "Incomplete eligibility documentation.")));
        Assert.Equal(HttpStatusCode.Conflict, (await manager.GetAsync(path)).StatusCode);
    }

    private static Dictionary<string, string> Info()
    {
        return new Dictionary<string, string>
        {
            ["Info.FirstName"] = "Test Applicant", ["Info.LastName"] = "Applicant",
            ["Info.Email"] = "workflow@example.test", ["Info.Phone"] = "202-555-0102",
            ["Info.CurrentAddress"] = "10 Current Lane"
        };
    }

    private static async Task<int> Start(RentalFactory factory, HttpClient client)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var unit = await factory.Read(today, static (db, state) =>
            new RentalRepository(db).AvailableUnits(state)
                .OrderBy(static x => x.Id).Select(static x => x.Id).FirstAsync());
        var response = await Post(client, "/Applications/Start",
            new Dictionary<string, string> { ["unitId"] = unit.ToString() }, "/Units");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        return int.Parse(Assert.IsType<Uri>(response.Headers.Location).OriginalString.Split('/').Last());
    }

}