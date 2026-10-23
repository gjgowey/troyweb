using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TroyWebProperty.Core;
using TroyWebProperty.Infrastructure;
using TroyWebProperty.Infrastructure.Data;
using TroyWebProperty.Web.Controllers;
using Xunit;
using static TroyWebProperty.IntegrationTests.RentalFactory;

namespace TroyWebProperty.IntegrationTests;

public sealed class AspNetCorePipelineTests
{

    [Fact]
    public async Task GlobalAntiforgeryProtectsNewControllersAndErrorReexecutionStillWorks()
    {
        await using var factory = new RentalFactory();
        await using var host = factory.WithWebHostBuilder(static builder => builder.ConfigureServices(static services =>
            services.AddControllersWithViews().AddApplicationPart(typeof(AntiforgeryProbeController).Assembly)));
        using var browser = host.CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        using var missing = await browser.PostAsync("/_tests/antiforgery", new StringContent(""));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        var token = await browser.GetStringAsync("/_tests/antiforgery");
        using var valid = await browser.PostAsync("/_tests/antiforgery", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        // The exception handler re-executes POSTs using the original method.
        using var errorPage = await browser.PostAsync("/Home/Error", new StringContent(""));
        Assert.Equal(HttpStatusCode.OK, errorPage.StatusCode);
        Assert.True(errorPage.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task PublicAssetsSkipIdentityButProtectedPagesStillAuthenticate()
    {
        var counter = new CountingClaimsTransformation();
        await using var factory = new RentalFactory();
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IClaimsTransformation>(counter)));
        using var browser = host.CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        using var registration = await Post(browser, "/Account/Register", new Dictionary<string, string>
        {
            ["FirstName"] = "Pipeline", ["LastName"] = "Applicant", ["Email"] = "pipeline@example.test",
            ["Role"] = AppRoles.Applicant, ["Password"] = Password, ["ConfirmPassword"] = Password
        });
        Assert.Equal(HttpStatusCode.Redirect, registration.StatusCode);
        counter.Calls = 0;
        using var asset = await browser.GetAsync("/css/site.css");
        Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
        Assert.Equal("text/css", asset.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(asset.Headers.ETag);
        Assert.Equal(0, counter.Calls);
        using var page = await browser.GetAsync("/Applications");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.True(counter.Calls > 0);
        using var managementModal = await browser.GetAsync("/Units/Edit");
        Assert.Equal(HttpStatusCode.Redirect, managementModal.StatusCode);
        Assert.Contains("AccessDenied", Assert.IsType<Uri>(managementModal.Headers.Location).OriginalString);
    }

    [Fact]
    public async Task NonDevelopmentStartupCreatesAndSeedsTheDatabase()
    {
        await using var factory = new RentalFactory();
        await using var host = factory.WithWebHostBuilder(static builder => builder.UseEnvironment("Production"));
        using var browser = host.CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        using var home = await browser.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(24, await db.Applications.CountAsync());
        Assert.Equal(2, await db.Roles.CountAsync());
        Assert.Equal(4, await db.Properties.CountAsync());
        Assert.Equal(12, await db.Units.CountAsync());
    }

    [Fact]
    public async Task DuplicateEmailRegistrationIsRejectedWithoutASecondProfile()
    {
        await using var factory = new RentalFactory();
        using var browser = factory.Browser();
        var fields = new Dictionary<string, string>
        {
            ["FirstName"] = "Dup", ["LastName"] = "User", ["Email"] = "dup@example.test",
            ["Role"] = AppRoles.Applicant, ["Password"] = Password, ["ConfirmPassword"] = Password
        };
        using var created = await Post(browser, "/Account/Register", new Dictionary<string, string>(fields));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        using var duplicate = await Post(browser, "/Account/Register", fields);
        var html = await duplicate.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Contains("already taken", html, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1,
            await factory.Read(static db => db.Users.CountAsync(static x => x.NormalizedEmail == "DUP@EXAMPLE.TEST")));
        Assert.Equal(1,
            await factory.Read(static db => db.Applicants.CountAsync(static x => x.Email == "dup@example.test")));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public async Task RegistrationAcceptsPasswordsFromOneToOneHundredCharacters(int length)
    {
        var password = new string('a', length);
        await using var factory = new RentalFactory();
        using var browser = factory.Browser();
        using var response = await Post(browser, "/Account/Register", new Dictionary<string, string>
        {
            ["FirstName"] = "Len", ["LastName"] = "Ok", ["Email"] = $"len{length}@example.test",
            ["Role"] = AppRoles.Applicant, ["Password"] = password, ["ConfirmPassword"] = password
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task RegistrationRejectsPasswordsLongerThanOneHundredCharacters()
    {
        var password = new string('b', 101);
        await using var factory = new RentalFactory();
        using var browser = factory.Browser();
        using var response = await Post(browser, "/Account/Register", new Dictionary<string, string>
        {
            ["FirstName"] = "Len", ["LastName"] = "Over", ["Email"] = "len101@example.test",
            ["Role"] = AppRoles.Applicant, ["Password"] = password, ["ConfirmPassword"] = password
        });
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Create an account", html);
        Assert.Equal(0,
            await factory.Read(static db => db.Users.CountAsync(static x => x.Email == "len101@example.test")));
    }

    [Fact]
    public async Task RegistrationAcceptsAPasswordWithNoCharacterComplexity()
    {
        const string password = "nocomplexity";
        await using var factory = new RentalFactory();
        using var browser = factory.Browser();
        using var response = await Post(browser, "/Account/Register", new Dictionary<string, string>
        {
            ["FirstName"] = "Simple", ["LastName"] = "Secret", ["Email"] = "simple@example.test",
            ["Role"] = AppRoles.Applicant, ["Password"] = password, ["ConfirmPassword"] = password
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task NonDevelopmentErrorsUseTheGenericPageWithoutDeveloperExceptionDetails()
    {
        await using var factory = new RentalFactory();
        await using var host = factory.WithWebHostBuilder(static builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureServices(static services =>
                services.AddControllersWithViews().AddApplicationPart(typeof(ErrorProbeController).Assembly));
        });
        using var browser = host.CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        using var response = await browser.GetAsync("/_tests/error");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("contact the site administrator", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Swapping to", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ASPNETCORE_ENVIRONMENT", html);
        Assert.DoesNotContain("Probe failure", html);
    }

    [Fact]
    public async Task ReadQueriesHonorCancelledRequests()
    {
        await using var factory = new RentalFactory();
        using var browser = factory.Browser();
        _ = await browser.GetStringAsync("/");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await factory.Read(cancellation.Token, static async (db, token) =>
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new PropertiesController(db).Index(token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new UnitsController(db,
                new ApplicationService(db, new RentalRepository(db), TimeProvider.System)).Index(token));

            return true;
        });
    }

    private sealed class CountingClaimsTransformation : IClaimsTransformation
    {

        public int Calls;

        public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            Interlocked.Increment(ref Calls);

            return Task.FromResult(principal);
        }

    }

}

[Route("_tests/error")]
public sealed class ErrorProbeController : Controller
{

    [HttpGet]
    public IActionResult Boom()
    {
        throw new InvalidOperationException("Probe failure");
    }

}

// Test-host-only controller deliberately has no antiforgery attributes.
[Route("_tests/antiforgery")]
public sealed class AntiforgeryProbeController(IAntiforgery antiforgery) : Controller
{

    [HttpGet]
    public IActionResult Token()
    {
        return Content(antiforgery.GetAndStoreTokens(HttpContext).RequestToken ??
                       throw new InvalidOperationException("Antiforgery did not generate a request token."));
    }

    [HttpPost]
    public IActionResult Save()
    {
        return Ok();
    }

}