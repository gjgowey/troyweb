using Microsoft.EntityFrameworkCore;
using TroyWebProperty.Core.Entities;
using TroyWebProperty.Infrastructure.Data;

namespace TroyWebProperty.Infrastructure;

public sealed class RentalRepository(ApplicationDbContext db)
{

    public IQueryable<RentalApplication> Applications => db.Applications;
    public IQueryable<Unit> Units => db.Units;

    public IQueryable<Unit> AvailableUnits(DateOnly today)
    {
        var occupiedUnitIds = OccupiedUnitIds(today);

        return db.Units.Where(unit => !occupiedUnitIds.Contains(unit.Id));
    }

    private IQueryable<int> OccupiedUnitIds(DateOnly today)
    {
        return db.Leases.Where(lease => lease.StartDate <= today && lease.EndDate > today)
            .Select(static lease => lease.UnitId);
    }

    internal Task<bool> OccupiedAsync(int unitId, DateOnly today)
    {
        return db.Leases.AnyAsync(x => x.UnitId == unitId && x.StartDate <= today && x.EndDate > today);
    }

    internal Task<RentalApplication?> LoadAsync(int id)
    {
        return db.Applications
            .Include(static x => x.Status).Include(static x => x.Applicant).Include(static x => x.Unit)
            .ThenInclude(static x => x.Property)
            .Include(static x => x.Residences).Include(static x => x.Changes).SingleOrDefaultAsync(x => x.Id == id);
    }

    // Every availability-sensitive workflow acquires the SAME unit write lock first.
    // SQL Server holds it until the transaction commits, serializing competing approvals.
    internal Task<int> LockUnitAsync(int unitId)
    {
        return db.Units.Where(x => x.Id == unitId)
            .ExecuteUpdateAsync(static x => x.SetProperty(static u => u.LockVersion, static u => u.LockVersion + 1));
    }

}