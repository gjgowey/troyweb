using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TroyWebProperty.Core;
using TroyWebProperty.Core.Entities;
using TroyWebProperty.Infrastructure;
using TroyWebProperty.Infrastructure.Data;
using Xunit;

namespace TroyWebProperty.IntegrationTests;

public sealed class SqlServerFactAttribute : FactAttribute
{

    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RENTAL_TEST_SQLSERVER")))
        {
            Skip = "Set RENTAL_TEST_SQLSERVER to run real SQL Server schema and competing-approval verification.";
        }
    }

}

public sealed class SqlServerTests
{

    [Fact]
    public void SqlServerCreateScriptHasDomainSchemaAndNoMigrationHistory()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        options.ConfigureRentalDatabase(
            "Server=(local);Database=ScriptOnly;Integrated Security=True;TrustServerCertificate=True");
        using var db = new ApplicationDbContext(options.Options);
        var script = db.Database.GenerateCreateScript();
        Assert.Contains("CREATE TABLE [Leases]", script);
        Assert.Contains("CREATE TABLE [AspNetUsers]", script);
        Assert.Contains("CREATE TABLE [Residences]", script);
        Assert.DoesNotContain("__EFMigrationsHistory", script);
    }

    [SqlServerFact]
    public async Task RealSqlServer_CreatesSeedsRestartsAndSerializesCompetingApprovals()
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("RENTAL_TEST_SQLSERVER"))
            { InitialCatalog = "RentalAssessment_Test_" + Guid.NewGuid().ToString("N") };

        ApplicationDbContext Open()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>();
            options.ConfigureRentalDatabase(connection.ConnectionString);

            return new ApplicationDbContext(options.Options);
        }

        try
        {
            int firstId, secondId, unitId;
            byte[] firstVersion, secondVersion;
            string managerId;

            await using (var db = Open())
            {
                await new DatabaseInitializer(db).InitializeAsync();
                Assert.Equal(24, await db.Applications.CountAsync());
                Assert.Equal(2, await db.Roles.CountAsync());
                await SchemaConventionTests.VerifyLiveSchemaAndConcurrency(db, Open);
                await new DatabaseInitializer(db).InitializeAsync();
                Assert.Equal(24, await db.Applications.CountAsync());
                var first = await db.Applications.FirstAsync(static x => x.Status.Code == "Submitted");
                var second = new RentalApplication
                {
                    ApplicantId = first.ApplicantId, UnitId = first.UnitId, StatusId = first.StatusId,
                    InformationSaved = true, HistorySaved = true, CreatedAtUtc = DateTime.UtcNow
                };
                db.Add(second);
                await db.SaveChangesAsync();
                firstId = first.Id;
                secondId = second.Id;
                unitId = first.UnitId;
                managerId = await db.Users.Where(static x => x.Email == "manager1@example.test")
                    .Select(static x => x.Id)
                    .SingleAsync();
                firstVersion = first.RowVersion;
                secondVersion = second.RowVersion;
            }

            async Task<bool> Approve(int id, byte[] version)
            {
                await using var db = Open();
                var service = new ApplicationService(db, new RentalRepository(db), TimeProvider.System);

                try
                {
                    await service.ReviewAsync(id, version, managerId, "manager1@example.test", "Approved",
                        "Concurrency test", DateOnly.FromDateTime(DateTime.UtcNow));

                    return true;
                }
                catch (RuleException)
                {
                    return false;
                }
            }

            var results = await Task.WhenAll(Approve(firstId, firstVersion), Approve(secondId, secondVersion));
            Assert.Equal(1, results.Count(static x => x));
            await using var verify = Open();
            Assert.Equal(1, await verify.Leases.CountAsync(x => x.UnitId == unitId));
        }
        finally
        {
            // Only deletes the unique disposable database created by this test.
            await using var cleanup = Open();
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

}