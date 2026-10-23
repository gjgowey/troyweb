using Microsoft.EntityFrameworkCore;
using TroyWebProperty.Core;
using TroyWebProperty.Core.Entities;
using TroyWebProperty.Infrastructure;
using TroyWebProperty.Infrastructure.Data;
using Xunit;

namespace TroyWebProperty.IntegrationTests;

// SQLite serializes writers with a database lock and an emulated rowversion.
// This does not prove SQL Server's unit write lock or native rowversion behavior.
public sealed class SqliteCompetingApprovalTests
{

    [Fact]
    public async Task CompetingApprovalsCreateOnlyOneLease()
    {
        var path = Path.Combine(Path.GetTempPath(), $"rental-race-{Guid.NewGuid():N}.db");

        SqliteTestDbContext Open()
        {
            return new SqliteTestDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(
                $"Data Source={path};Foreign Keys=True;Pooling=False").Options);
        }

        try
        {
            int firstId, secondId, unitId;
            byte[] firstVersion, secondVersion;
            string managerId;

            await using (var db = Open())
            {
                await new DatabaseInitializer(db).InitializeAsync();
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
                var connection = db.Database.GetDbConnection();
                await connection.OpenAsync();

                await using (var pragma = connection.CreateCommand())
                {
                    pragma.CommandText = "PRAGMA busy_timeout = 8000;";
                    await pragma.ExecuteNonQueryAsync();
                }

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
            File.Delete(path);
            File.Delete(path + "-wal");
            File.Delete(path + "-shm");
        }
    }

}