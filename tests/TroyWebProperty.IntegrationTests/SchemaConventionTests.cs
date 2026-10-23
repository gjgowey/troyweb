using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using TroyWebProperty.Core.Entities;
using TroyWebProperty.Infrastructure.Data;
using Xunit;

namespace TroyWebProperty.IntegrationTests;

public sealed class SchemaConventionTests
{

    [Fact]
    public void DomainTablesUseIntegerIdentityKeysAndRowVersions_IdentityRetainsItsDefaults()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        options.ConfigureRentalDatabase(
            "Server=(local);Database=ModelOnly;Integrated Security=True;TrustServerCertificate=True");
        using var db = new ApplicationDbContext(options.Options);
        var domain = db.Model.GetEntityTypes().Where(static x => typeof(IRowVersioned).IsAssignableFrom(x.ClrType))
            .ToList();
        Assert.Equal(10, domain.Count);

        foreach (var entity in domain)
        {
            var primaryKey = entity.FindPrimaryKey();
            Assert.NotNull(primaryKey);
            var key = Assert.Single(primaryKey.Properties);
            Assert.Equal("Id", key.Name);
            Assert.Equal(typeof(int), key.ClrType);
            Assert.Equal(SqlServerValueGenerationStrategy.IdentityColumn,
                SqlServerPropertyExtensions.GetValueGenerationStrategy(key));
            var version = entity.FindProperty("RowVersion");
            Assert.NotNull(version);
            Assert.Equal(typeof(byte[]), version.ClrType);
            Assert.True(version.IsConcurrencyToken);
            Assert.Equal(ValueGenerated.OnAddOrUpdate, version.ValueGenerated);
        }

        var identity = db.Model.GetEntityTypes().Where(static entity =>
        {
            var tableName = entity.GetTableName();
            Assert.NotNull(tableName);

            return tableName.StartsWith("AspNet", StringComparison.Ordinal);
        }).ToList();
        Assert.Equal(7, identity.Count);
        Assert.All(identity, static entity => Assert.Null(entity.FindProperty("RowVersion")));

        var userEntity = db.Model.FindEntityType(typeof(ApplicationUser));
        Assert.NotNull(userEntity);
        var userKey = userEntity.FindPrimaryKey();
        Assert.NotNull(userKey);
        Assert.Equal(typeof(string), Assert.Single(userKey.Properties).ClrType);

        foreach (var (tableName, expectedKeyCount) in new[]
                 {
                     ("AspNetUserRoles", 2), ("AspNetUserLogins", 2), ("AspNetUserTokens", 3)
                 })
        {
            var entity = Assert.Single(identity, entity => entity.GetTableName() == tableName);
            var primaryKey = entity.FindPrimaryKey();
            Assert.NotNull(primaryKey);
            Assert.Equal(expectedKeyCount, primaryKey.Properties.Count);
        }
    }

    [Fact]
    public void ApplicationFiltersAndNewestFirstListsHaveCompositeIndexes()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>();
        options.ConfigureRentalDatabase(
            "Server=(local);Database=ModelOnly;Integrated Security=True;TrustServerCertificate=True");
        using var db = new ApplicationDbContext(options.Options);
        var sql = db.Database.GenerateCreateScript();
        Assert.Contains("ON [Applications] ([ApplicantId], [CreatedAtUtc] DESC)", sql);
        Assert.Contains("ON [Applications] ([StatusId], [CreatedAtUtc] DESC)", sql);
        Assert.Contains("ON [Applications] ([UnitId], [StatusId], [CreatedAtUtc] DESC)", sql);
        Assert.Contains("ON [Applications] ([CreatedAtUtc] DESC)", sql);
        Assert.Contains("ON [Properties] ([Name])", sql);
        // Composite leftmost columns replace redundant convention-generated FK indexes.
        Assert.DoesNotContain("CREATE INDEX [IX_Applications_ApplicantId] ", sql);
        Assert.DoesNotContain("CREATE INDEX [IX_Applications_StatusId] ", sql);
        Assert.DoesNotContain("CREATE INDEX [IX_Applications_UnitId] ", sql);
    }

    internal static async Task VerifyLiveSchemaAndConcurrency(ApplicationDbContext db, Func<ApplicationDbContext> open)
    {
        var tables = db.Model.GetEntityTypes().Where(static x => typeof(IRowVersioned).IsAssignableFrom(x.ClrType))
            .Select(static entity =>
            {
                var tableName = entity.GetTableName();
                Assert.NotNull(tableName);

                return tableName;
            }).ToHashSet();
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
                              SELECT t.name,
                                  (SELECT COUNT(*) FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id WHERE i.object_id=t.object_id AND i.is_primary_key=1),
                                  (SELECT COUNT(*) FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=t.object_id AND i.is_primary_key=1 AND c.system_type_id=56 AND c.is_identity=1),
                                  (SELECT COUNT(*) FROM sys.columns c WHERE c.object_id=t.object_id AND c.name='RowVersion' AND c.system_type_id=189 AND c.is_nullable=0)
                              FROM sys.tables t WHERE t.is_ms_shipped=0
                              """;

        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var table = reader.GetString(0);

                if (!tables.Contains(table))
                {
                    continue;
                }

                Assert.Equal(1, reader.GetInt32(1));
                Assert.Equal(1, reader.GetInt32(2));
                Assert.Equal(1, reader.GetInt32(3));
                tables.Remove(table);
            }
        }

        Assert.Empty(tables);
        command.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE name = '__EFMigrationsHistory'";
        Assert.Equal(0, Convert.ToInt32(await command.ExecuteScalarAsync()));
        await using var first = open();
        await using var stale = open();
        var a = await first.Properties.FirstAsync();
        Property b;
        {
            var propertyId = a.Id;
            b = await stale.Properties.SingleAsync(x => x.Id == propertyId);
        }
        Assert.Equal(8, a.RowVersion.Length);
        var original = a.RowVersion.ToArray();
        a.Address += " updated";
        await first.SaveChangesAsync();
        Assert.False(original.SequenceEqual(a.RowVersion));
        b.Address += " stale";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
    }

}