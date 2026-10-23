using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using TroyWebProperty.Core.Entities;
using TroyWebProperty.Infrastructure.Data;

namespace TroyWebProperty.IntegrationTests;

// SQLite cannot generate SQL Server rowversions. Emulation is strictly test-only;
// live SQL tests assert the native identity and rowversion behavior separately.
public sealed class SqliteTestDbContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
{

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        foreach (var version in builder.Model.GetEntityTypes()
                     .Where(static x => typeof(IRowVersioned).IsAssignableFrom(x.ClrType)).Select(entity =>
                         builder.Entity(entity.ClrType).Property<byte[]>("RowVersion")))
            version.HasColumnType("BLOB").IsConcurrencyToken().ValueGeneratedNever();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries<IRowVersioned>()
                     .Where(static x => x.State is EntityState.Added or EntityState.Modified))
            entry.Entity.RowVersion = RandomNumberGenerator.GetBytes(8);

        return base.SaveChangesAsync(cancellationToken);
    }

}