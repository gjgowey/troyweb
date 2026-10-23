using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TroyWebProperty.Core;
using TroyWebProperty.Infrastructure.Data;
using Xunit;

namespace TroyWebProperty.IntegrationTests;

public sealed class DevelopmentPasswordResetTests
{

    [Fact]
    public async Task DevelopmentStartupRestoresTheSharedPasswordForARegisteredAccount()
    {
        var path = Path.Combine(Path.GetTempPath(), $"rental-dev-password-{Guid.NewGuid():N}.db");
        const string email = "registered-reset@example.test";
        const string chosen = "registered-secret";
        const string changed = "changed-secret";
        DevelopmentDatabaseFactory? first = null;

        try
        {
            first = new DevelopmentDatabaseFactory(path);

            using (var browser = Client(first))
            {
                using var created = await RentalFactory.Post(browser, "/Account/Register",
                    new Dictionary<string, string>
                    {
                        ["FirstName"] = "Reg", ["LastName"] = "User", ["Email"] = email,
                        ["Role"] = AppRoles.Applicant, ["Password"] = chosen, ["ConfirmPassword"] = chosen
                    });
                Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
            }

            using (var scope = first.Services.CreateScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await users.FindByEmailAsync(email);
                Assert.NotNull(user);
                var token = await users.GeneratePasswordResetTokenAsync(user);
                var reset = await users.ResetPasswordAsync(user, token, changed);
                Assert.True(reset.Succeeded);
            }

            await first.DisposeAsync();
            first = null;

            await using var second = new DevelopmentDatabaseFactory(path);
            using var client = Client(second);
            using var shared = await RentalFactory.Post(client, "/Account/Login", new Dictionary<string, string>
            {
                ["Email"] = email, ["Password"] = DatabaseSeeder.TestPassword
            });
            Assert.Equal(HttpStatusCode.Redirect, shared.StatusCode);
            using var rejected = await RentalFactory.Post(client, "/Account/Login", new Dictionary<string, string>
            {
                ["Email"] = email, ["Password"] = changed
            });
            Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        }
        finally
        {
            if (first is not null)
            {
                await first.DisposeAsync();
            }

            File.Delete(path);
            File.Delete(path + "-wal");
            File.Delete(path + "-shm");
        }
    }

    private static HttpClient Client(WebApplicationFactory<Program> factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
    }

    private sealed class DevelopmentDatabaseFactory(string path) : WebApplicationFactory<Program>
    {

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ApplicationDbContext>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddScoped<ApplicationDbContext>(_ => new SqliteTestDbContext(
                    new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(
                        $"Data Source={path};Foreign Keys=True;Pooling=False").Options));
                services.RemoveAll<IDatabaseInitializer>();
                services.AddScoped<IDatabaseInitializer, TestInitializer>();
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
            });
        }

        private sealed class TestInitializer(ApplicationDbContext db) : IDatabaseInitializer
        {

            public async Task InitializeAsync()
            {
                await db.Database.EnsureCreatedAsync();
                await DatabaseSeeder.SeedAsync(db);
            }

        }

    }

}