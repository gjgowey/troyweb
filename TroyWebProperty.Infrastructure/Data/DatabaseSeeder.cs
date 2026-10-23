using Bogus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TroyWebProperty.Core;
using TroyWebProperty.Core.Entities;

namespace TroyWebProperty.Infrastructure.Data;

public static class DatabaseSeeder
{

    public const string TestPassword = "password";

    public static async Task SeedAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        await using var transaction =
            db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;

        // Serialize seed runs without relying on EF's migration-history infrastructure.
        if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync("""
                                                 DECLARE @result int;
                                                 EXEC @result = sys.sp_getapplock @Resource=N'TroyWebProperty:Seed', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=60000;
                                                 IF @result < 0 THROW 51000, 'Could not acquire the database seed lock.', 1;
                                                 """, ct);
        }

        var statuses = await db.ApplicationStatuses.ToDictionaryAsync(static x => x.Code, ct);

        for (var i = 0; i < SeedCatalog.Statuses.Length; i++)
        {
            var (code, name) = SeedCatalog.Statuses[i];

            if (!statuses.ContainsKey(code))
            {
                var row = new ApplicationStatus { Code = code, Name = name, SortOrder = i };
                db.Add(row);
                statuses.Add(code, row);
            }
        }

        var types = await db.UnitTypes.ToDictionaryAsync(static x => x.Code, ct);

        foreach (var (code, name, active) in SeedCatalog.UnitTypes)
            if (!types.ContainsKey(code))
            {
                var row = new UnitType { Code = code, Name = name, IsActive = active };
                db.Add(row);
                types.Add(code, row);
            }

        foreach (var roleName in new[] { AppRoles.Applicant, AppRoles.PropertyManager })
            if (!await db.Roles.AnyAsync(x => x.NormalizedName == roleName.ToUpper(), ct))
            {
                db.Roles.Add(new IdentityRole(roleName)
                    { NormalizedName = roleName.ToUpperInvariant() });
            }

        await db.SaveChangesAsync(ct);
        var managers = await Load(db.PropertyManagers, ct);

        for (var i = 1; i <= 3; i++)
        {
            var key = $"demo:manager:{i:D3}";
            var user = await EnsureUser(db, $"manager{i}@example.test", AppRoles.PropertyManager,
                ct);

            if (!managers.ContainsKey(key))
            {
                var f = Fake(1000 + i);
                var row = new PropertyManager
                {
                    SeedKey = key, UserId = user.Id, FirstName = f.Name.FirstName(), LastName = f.Name.LastName(),
                    Email = user.Email ??
                            throw new InvalidOperationException("A seeded user must have an email address."),
                    Phone = f.Phone.PhoneNumber("###-###-####")
                };
                db.Add(row);
                managers.Add(key, row);
            }
        }

        var applicants = await Load(db.Applicants, ct);

        for (var i = 1; i <= 24; i++)
        {
            var key = $"demo:applicant:{i:D3}";
            var user = await EnsureUser(db, $"applicant{i}@example.test", AppRoles.Applicant,
                ct);

            if (!applicants.ContainsKey(key))
            {
                var f = Fake(2000 + i);
                var row = new Applicant
                {
                    SeedKey = key, UserId = user.Id, FirstName = f.Name.FirstName(), LastName = f.Name.LastName(),
                    Email = user.Email ??
                            throw new InvalidOperationException("A seeded user must have an email address."),
                    Phone = f.Phone.PhoneNumber("###-###-####")
                };
                db.Add(row);
                applicants.Add(key, row);
            }
        }

        var properties = await Load(db.Properties, ct);

        for (var i = 1; i <= 4; i++)
        {
            var key = $"demo:property:{i:D3}";

            if (!properties.ContainsKey(key))
            {
                var f = Fake(3000 + i);
                var row = new Property
                {
                    SeedKey = key, Name = $"{f.Commerce.ProductAdjective()} Residences {i}",
                    Address = f.Address.FullAddress(), PropertyManager = managers[$"demo:manager:{(i - 1) % 3 + 1:D3}"]
                };
                db.Add(row);
                properties.Add(key, row);
            }
        }

        var units = await Load(db.Units, ct);

        for (var i = 1; i <= 12; i++)
        {
            var key = $"demo:unit:{i:D3}";

            if (!units.ContainsKey(key))
            {
                var row = new Unit
                {
                    SeedKey = key, Number = $"{101 + (i - 1) % 3}", Bedrooms = (i - 1) % 3,
                    Property = properties[$"demo:property:{(i - 1) / 3 + 1:D3}"],
                    UnitType = types[SeedCatalog.UnitTypes[(i - 1) % 3].Code],
                    MonthlyRent = Fake(4000 + i).Random.Int(900, 2600)
                };
                db.Add(row);
                units.Add(key, row);
            }
        }

        var applications = await Load(db.Applications, ct);

        for (var s = 0; s < SeedCatalog.Statuses.Length; s++)
            for (var n = 1; n <= 4; n++)
            {
                var code = SeedCatalog.Statuses[s].Code;
                var key = $"demo:application:{code}:{n:D3}";

                if (applications.ContainsKey(key))
                {
                    continue;
                }

                var index = s * 4 + n;
                var f = Fake(5000 + index);
                var applicant = applicants[$"demo:applicant:{index:D3}"];
                var created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(index);
                var row = new RentalApplication
                {
                    SeedKey = key, Applicant = applicant, Unit = units[$"demo:unit:{(index - 1) % 12 + 1:D3}"],
                    Status = statuses[code], CreatedAtUtc = created,
                    SubmittedAtUtc = code is "Draft" or "Withdrawn" ? null : created.AddHours(1),
                    FirstName = applicant.FirstName, LastName = applicant.LastName,
                    MiddleInitial = applicant.MiddleInitial, Title = applicant.Title, Email = applicant.Email,
                    Phone = applicant.Phone,
                    CurrentAddress = f.Address.FullAddress(), InformationSaved = true, HistorySaved = true
                };
                row.Residences.Add(new Residence
                {
                    Address = f.Address.FullAddress(), LandlordName = f.Name.FullName(),
                    LandlordPhone = f.Phone.PhoneNumber("###-###-####"), MoveIn = new DateOnly(2022, 1, 1),
                    MoveOut = new DateOnly(2024, 12, 31)
                });
                row.Changes.Add(new StatusChange
                {
                    FromStatus = "", ToStatus = "Draft", ActorUserId = applicant.UserId, ActorName = applicant.Email,
                    ChangedAtUtc = created
                });

                if (code != "Draft")
                {
                    row.Changes.Add(new StatusChange
                    {
                        FromStatus = "Draft", ToStatus = code is "Withdrawn" ? "Withdrawn" : "Submitted",
                        ActorUserId = applicant.UserId, ActorName = applicant.Email, ChangedAtUtc = created.AddHours(1)
                    });
                }

                if (code is "Returned" or "Approved" or "Denied")
                {
                    row.Changes.Add(new StatusChange
                    {
                        FromStatus = "Submitted", ToStatus = code, ActorUserId = managers["demo:manager:001"].UserId,
                        ActorName = managers["demo:manager:001"].Email, ChangedAtUtc = created.AddHours(2),
                        Comment = code == "Returned" ? "Please confirm your previous address." :
                            code == "Denied" ? "Application did not meet the rental criteria." : "Application approved."
                    });
                }

                db.Add(row);

                if (code == "Approved")
                {
                    var start = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-1);
                    db.Leases.Add(new Lease
                        { Application = row, Unit = row.Unit, StartDate = start, EndDate = start.AddMonths(12) });
                }
            }

        await db.SaveChangesAsync(ct);

        if (transaction is not null)
        {
            await transaction.CommitAsync(ct);
        }
    }

    private static async Task<ApplicationUser> EnsureUser(ApplicationDbContext db, string email, string role,
        CancellationToken ct)
    {
        ApplicationUser? user;
        {
            var normalizedEmail = email.ToUpperInvariant();
            user = await db.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, ct);
        }

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email, NormalizedUserName = email.ToUpperInvariant(), Email = email,
                NormalizedEmail = email.ToUpperInvariant(), SecurityStamp = Guid.NewGuid().ToString(),
                LockoutEnabled = true
            };
            db.Add(user);
        }

        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, TestPassword);
        }

        await db.SaveChangesAsync(ct);
        string roleId;
        {
            var normalizedRole = role.ToUpper();
            roleId = (await db.Roles.SingleAsync(x => x.NormalizedName == normalizedRole, ct)).Id;
        }

        if (!await db.UserRoles.AnyAsync(x => x.UserId == user.Id && x.RoleId == roleId, ct))
        {
            db.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = roleId });
        }

        await db.SaveChangesAsync(ct);

        return user;
    }

    public static async Task SetDevelopmentPasswordsAsync(ApplicationDbContext db)
    {
        var hasher = new PasswordHasher<ApplicationUser>();

        foreach (var user in await db.Users.ToListAsync())
        {
            if (user.PasswordHash is not null && hasher.VerifyHashedPassword(user, user.PasswordHash, TestPassword)
                == PasswordVerificationResult.Success)
            {
                continue;
            }

            user.PasswordHash = hasher.HashPassword(user, TestPassword);
            user.SecurityStamp = Guid.NewGuid().ToString();
            user.ConcurrencyStamp = Guid.NewGuid().ToString();
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
        }

        await db.SaveChangesAsync();
    }

    private static Faker Fake(int seed)
    {
        return new Faker { Random = new Randomizer(seed) };
    }

    private static Task<Dictionary<string, T>> Load<T>(IQueryable<T> set, CancellationToken ct) where T : SeedableEntity
    {
        return set.Where(static x => x.SeedKey != null).ToDictionaryAsync(
            static x => x.SeedKey ?? throw new InvalidOperationException("A seeded entity must have a seed key."), ct);
    }

}