using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TroyWebProperty.Core.Entities;

namespace TroyWebProperty.Infrastructure.Data;

public sealed class ApplicationUser : IdentityUser
{

}

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{

    public DbSet<PropertyManager> PropertyManagers => Set<PropertyManager>();
    public DbSet<Applicant> Applicants => Set<Applicant>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<UnitType> UnitTypes => Set<UnitType>();
    public DbSet<ApplicationStatus> ApplicationStatuses => Set<ApplicationStatus>();
    public DbSet<RentalApplication> Applications => Set<RentalApplication>();
    public DbSet<Residence> Residences => Set<Residence>();
    public DbSet<Lease> Leases => Set<Lease>();
    public DbSet<StatusChange> StatusChanges => Set<StatusChange>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Application/domain tables use integer identity keys and native SQL Server rowversions. Identity retains its standard schema.
        foreach (var entity in b.Model.GetEntityTypes()
                     .Where(static x => typeof(IRowVersioned).IsAssignableFrom(x.ClrType)))
        {
            var key = entity.FindPrimaryKey();

            if (key?.Properties.Count == 1 && key.Properties[0].ClrType == typeof(int))
            {
                b.Entity(entity.ClrType).Property<int>(key.Properties[0].Name).UseIdentityColumn();
            }

            b.Entity(entity.ClrType).Property<byte[]>("RowVersion").IsRowVersion();
        }

        SeedKey<PropertyManager>(b);
        SeedKey<Applicant>(b);
        SeedKey<Property>(b);
        SeedKey<Unit>(b);
        SeedKey<RentalApplication>(b);
        b.Entity<PropertyManager>().HasIndex(static x => x.UserId).IsUnique();
        b.Entity<Applicant>().HasIndex(static x => x.UserId).IsUnique();
        b.Entity<PropertyManager>().HasOne<ApplicationUser>().WithMany().HasForeignKey(static x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Applicant>().HasOne<ApplicationUser>().WithMany().HasForeignKey(static x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<UnitType>().HasIndex(static x => x.Code).IsUnique();
        b.Entity<ApplicationStatus>().HasIndex(static x => x.Code).IsUnique();
        b.Entity<Unit>().HasIndex(static x => new { x.PropertyId, x.Number }).IsUnique();
        // Equality filters lead composite keys; newest-first dates follow for list ordering.
        b.Entity<RentalApplication>().HasIndex(static x => new { x.ApplicantId, x.CreatedAtUtc })
            .IsDescending(false, true);
        b.Entity<RentalApplication>().HasIndex(static x => new { x.StatusId, x.CreatedAtUtc })
            .IsDescending(false, true);
        // Property filtering reaches applications through Units(PropertyId, Number).
        b.Entity<RentalApplication>().HasIndex(static x => new { x.UnitId, x.StatusId, x.CreatedAtUtc })
            .IsDescending(false, false, true);
        b.Entity<RentalApplication>().HasIndex(static x => x.CreatedAtUtc).IsDescending();
        b.Entity<Property>().HasIndex(static x => x.Name);
        b.Entity<Unit>().Property(static x => x.MonthlyRent).HasPrecision(10, 2);
        b.Entity<Unit>().ToTable(static t =>
        {
            t.HasCheckConstraint("CK_Unit_Rent", "MonthlyRent > 0");
            t.HasCheckConstraint("CK_Unit_Bedrooms", "Bedrooms >= 0");
        });
        b.Entity<Property>().HasOne(static x => x.PropertyManager).WithMany()
            .HasForeignKey(static x => x.PropertyManagerId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Unit>().HasOne(static x => x.Property).WithMany().HasForeignKey(static x => x.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Unit>().HasOne(static x => x.UnitType).WithMany().HasForeignKey(static x => x.UnitTypeId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<RentalApplication>().HasOne(static x => x.Applicant).WithMany()
            .HasForeignKey(static x => x.ApplicantId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<RentalApplication>().HasOne(static x => x.Unit).WithMany().HasForeignKey(static x => x.UnitId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<RentalApplication>().HasOne(static x => x.Status).WithMany().HasForeignKey(static x => x.StatusId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<RentalApplication>().HasMany(static x => x.Residences).WithOne()
            .HasForeignKey(static x => x.RentalApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<RentalApplication>().HasMany(static x => x.Changes).WithOne()
            .HasForeignKey(static x => x.RentalApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<Lease>().HasOne(static x => x.Application).WithMany().HasForeignKey(static x => x.RentalApplicationId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Lease>().HasOne(static x => x.Unit).WithMany().HasForeignKey(static x => x.UnitId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Entity<Lease>().HasIndex(static x => x.RentalApplicationId).IsUnique();
        b.Entity<Lease>().HasIndex(static x => new { x.UnitId, x.StartDate, x.EndDate });
        b.Entity<Lease>().ToTable(static t => t.HasCheckConstraint("CK_Lease_Term", "EndDate > StartDate"));
        foreach (var property in b.Model.GetEntityTypes()
                     .Where(static x => x.ClrType.Namespace == typeof(Unit).Namespace).SelectMany(static type => type
                         .GetProperties()
                         .Where(static x => x.ClrType == typeof(string) && x.GetMaxLength() == null)))
            property.SetMaxLength(property.Name == "Comment" ? 2000 :
                property.Name == "UserId" || property.Name == "ActorUserId" ? 450 : 300);
    }

    private static void SeedKey<T>(ModelBuilder b) where T : SeedableEntity
    {
        b.Entity<T>().Property(static x => x.SeedKey).HasMaxLength(100);
        b.Entity<T>().HasIndex(static x => x.SeedKey).IsUnique().HasFilter("[SeedKey] IS NOT NULL");
    }

}