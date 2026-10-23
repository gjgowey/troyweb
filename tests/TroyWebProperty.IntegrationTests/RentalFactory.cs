using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TroyWebProperty.Infrastructure.Data;
using Xunit;

namespace TroyWebProperty.IntegrationTests;

public sealed class RentalFactory : WebApplicationFactory<Program>
{

    public const string Password = "Assessment-Test123!";
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"rental-assessment-{Guid.NewGuid():N}.db");
    public CommandCapture Sql { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddScoped<ApplicationDbContext>(_ => new SqliteTestDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(
                    $"Data Source={_path};Foreign Keys=True;Pooling=False").AddInterceptors(Sql).Options));
            services.RemoveAll<IDatabaseInitializer>();
            services.AddScoped<IDatabaseInitializer, TestInitializer>();
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
        });
    }

    public HttpClient Browser()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
    }

    public async Task<HttpClient> Register(string email, string role)
    {
        var browser = Browser();
        var response = await Post(browser, "/Account/Register",
            new Dictionary<string, string>
            {
                ["FirstName"] = "Test Applicant", ["LastName"] = "Applicant", ["Email"] = email,
                ["Password"] = Password,
                ["ConfirmPassword"] = Password, ["Role"] = role
            });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        return browser;
    }

    public async Task<T> Read<T>(Func<ApplicationDbContext, Task<T>> read)
    {
        using var scope = Services.CreateScope();

        return await read(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    public async Task<T> Read<TState, T>(TState state, Func<ApplicationDbContext, TState, Task<T>> read)
    {
        using var scope = Services.CreateScope();

        return await read(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(), state);
    }

    public static string Field(string html, string name)
    {
        var match = Regex.Match(html, "<input[^>]*name=\"" + Regex.Escape(name) + "\"[^>]*value=\"([^\"]*)\"");
        Assert.True(match.Success, $"Missing field {name}");

        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    public static async Task<HttpResponseMessage> Post(HttpClient client, string path,
        Dictionary<string, string> fields, string? getPath = null)
    {
        var html = await client.GetStringAsync(getPath ?? path);
        fields["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken");

        if ((!fields.ContainsKey("RowVersion") || string.IsNullOrEmpty(fields["RowVersion"])) &&
            html.Contains("name=\"RowVersion\""))
        {
            fields["RowVersion"] = Field(html, "RowVersion");
        }

        return await client.PostAsync(path, new FormUrlEncodedContent(fields));
    }

    public static async Task<HttpResponseMessage> Wizard(HttpClient client, int id, int step, string command,
        Dictionary<string, string>? extra = null)
    {
        var html = await client.GetStringAsync($"/Applications/Edit/{id}?step={step}");
        var fields = extra ?? new Dictionary<string, string>();
        fields["Id"] = id.ToString();
        fields["RowVersion"] = Field(html, "RowVersion");
        fields["Step"] = step.ToString();
        fields["command"] = command;
        fields["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken");

        if ((!fields.ContainsKey("RowVersion") || string.IsNullOrEmpty(fields["RowVersion"])) &&
            html.Contains("name=\"RowVersion\""))
        {
            fields["RowVersion"] = Field(html, "RowVersion");
        }

        return await client.PostAsync("/Applications/Edit", new FormUrlEncodedContent(fields));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            File.Delete(_path);
            File.Delete(_path + "-wal");
            File.Delete(_path + "-shm");
        }
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