using System.Data;
using Microsoft.EntityFrameworkCore;
using TroyWebProperty.Core;
using TroyWebProperty.Core.Entities;
using TroyWebProperty.Infrastructure.Data;

namespace TroyWebProperty.Infrastructure;

public sealed class ApplicationService(ApplicationDbContext db, RentalRepository repository, TimeProvider clock)
{

    public DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    public async Task<int> StartAsync(int unitId, string userId, string name)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        if (await repository.LockUnitAsync(unitId) != 1)
        {
            throw new RuleException("Unit not found.");
        }

        if (await repository.OccupiedAsync(unitId, Today))
        {
            throw new RuleException("This unit is no longer available.");
        }

        var applicant = await db.Applicants.SingleAsync(x => x.UserId == userId);
        var row = new RentalApplication
        {
            ApplicantId = applicant.Id, UnitId = unitId,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime, FirstName = applicant.FirstName,
            LastName = applicant.LastName, MiddleInitial = applicant.MiddleInitial, Title = applicant.Title,
            Email = applicant.Email,
            Phone = applicant.Phone
        };
        await ChangeAsync(row, "Draft", userId, name, null);
        db.Applications.Add(row);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return row.Id;
    }

    public Task SaveInformationAsync(int id, byte[] rowVersion, string userId, ApplicantInformation info)
    {
        return Mutate(id, rowVersion, userId, false, row =>
        {
            ApplicationRules.RequireEditable(row.StatusCode);

            if (info.Validate().Count > 0)
            {
                throw new RuleException("Complete all applicant information fields correctly.");
            }

            row.FirstName = info.FirstName.Trim();
            row.LastName = info.LastName.Trim();
            row.MiddleInitial = info.MiddleInitial?.Trim().ToUpperInvariant();
            row.Title = info.Title;
            row.Email = info.Email.Trim();
            row.Phone = info.Phone.Trim();
            row.CurrentAddress = info.CurrentAddress.Trim();
            row.InformationSaved = true;

            return Task.CompletedTask;
        });
    }

    public Task SaveHistoryAsync(int id, byte[] rowVersion, string userId)
    {
        return Mutate(id, rowVersion, userId, false,
            row =>
            {
                ApplicationRules.RequireEditable(row.StatusCode);

                if (!row.InformationSaved)
                {
                    throw new RuleException("Save applicant information first.");
                }

                ApplicationRules.History(row.Residences, Today);
                row.HistorySaved = true;

                return Task.CompletedTask;
            });
    }

    public Task SaveResidenceAsync(int id, byte[] rowVersion, string userId, Residence input, bool delete)
    {
        return Mutate(id, rowVersion, userId, false, row =>
        {
            ApplicationRules.RequireEditable(row.StatusCode);
            var residence = input.Id == 0
                ? new Residence()
                : FindResidence(row.Residences, input.Id)
                  ?? throw new RuleException("Residence not found on this application.");

            if (delete)
            {
                if (input.Id == 0)
                {
                    throw new RuleException("Residence not found.");
                }

                db.Residences.Remove(residence);
            }
            else
            {
                ApplicationRules.ResidenceDates(input.MoveIn, input.MoveOut, Today);

                if (string.IsNullOrWhiteSpace(input.Address) || string.IsNullOrWhiteSpace(input.LandlordName) ||
                    string.IsNullOrWhiteSpace(input.LandlordPhone))
                {
                    throw new RuleException("Complete every residence field.");
                }

                residence.Address = input.Address.Trim();
                residence.LandlordName = input.LandlordName.Trim();
                residence.LandlordPhone = input.LandlordPhone.Trim();
                residence.MoveIn = input.MoveIn;
                residence.MoveOut = input.MoveOut;

                if (input.Id == 0)
                {
                    row.Residences.Add(residence);
                }
            }

            row.HistorySaved = false;

            return Task.CompletedTask;
        });
    }

    private static Residence? FindResidence(IEnumerable<Residence> residences, int residenceId)
    {
        Residence? result = null;

        foreach (var residence in residences.Where(residence => residence.Id == residenceId))
        {
            if (result is not null)
            {
                throw new InvalidOperationException("Sequence contains more than one matching element");
            }

            result = residence;
        }

        return result;
    }

    public Task SubmitAsync(int id, byte[] rowVersion, string userId, string name)
    {
        return Mutate(id, rowVersion, userId,
            false,
            async row =>
            {
                ApplicationRules.Submit(row, await repository.OccupiedAsync(row.UnitId, Today));
                ApplicationRules.History(row.Residences, Today);

                if (new ApplicantInformation
                    {
                        FirstName = row.FirstName, LastName = row.LastName, MiddleInitial = row.MiddleInitial,
                        Title = row.Title, Phone = row.Phone, Email = row.Email, CurrentAddress = row.CurrentAddress
                    }.Validate().Count > 0)
                {
                    throw new RuleException("Applicant information is incomplete.");
                }

                row.SubmittedAtUtc = clock.GetUtcNow().UtcDateTime;
                await ChangeAsync(row, "Submitted", userId, name, null);
            });
    }

    public Task WithdrawAsync(int id, byte[] rowVersion, string userId, string name)
    {
        return Mutate(id, rowVersion, userId, false,
            row => !ApplicationRules.CanWithdraw(row.StatusCode)
                ? throw new RuleException("This application cannot be withdrawn.")
                : ChangeAsync(row, "Withdrawn", userId, name, null));
    }

    public Task ReviewAsync(int id, byte[] rowVersion, string userId, string name, string outcome, string? comment,
        DateOnly start)
    {
        return Mutate(id, rowVersion, userId, true, async row =>
        {
            ApplicationRules.Review(row.StatusCode, outcome, comment,
                await repository.OccupiedAsync(row.UnitId, Today));

            if (outcome == "Approved")
            {
                if (start < Today || start == default)
                {
                    throw new RuleException("Lease start must be today or later.");
                }

                var end = start.AddMonths(12);

                if (await HasOverlappingLeaseAsync(db, row.UnitId, start, end))
                {
                    throw new RuleException("The proposed twelve-month lease overlaps an existing lease.");
                }

                db.Leases.Add(new Lease
                    { RentalApplicationId = row.Id, UnitId = row.UnitId, StartDate = start, EndDate = end });
            }

            await ChangeAsync(row, outcome, userId, name, comment?.Trim());
        });
    }

    private static Task<bool> HasOverlappingLeaseAsync(ApplicationDbContext context, int unitId,
        DateOnly start, DateOnly end)
    {
        return context.Leases.AnyAsync(x => x.UnitId == unitId && x.StartDate < end && x.EndDate > start);
    }

    private async Task Mutate(int id, byte[] rowVersion, string userId, bool manager,
        Func<RentalApplication, Task> action)
    {
        var unitId = await db.Applications.AsNoTracking().Where(x => x.Id == id).Select(static x => (int?)x.UnitId)
                         .SingleOrDefaultAsync()
                     ?? throw new RuleException("Application not found.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await repository.LockUnitAsync(unitId);
        var row = await repository.LoadAsync(id) ?? throw new RuleException("Application not found.");

        if (!manager && row.Applicant.UserId != userId)
        {
            throw new UnauthorizedAccessException();
        }

        if (!row.RowVersion.AsSpan().SequenceEqual(rowVersion))
        {
            throw new RuleException("This application changed. Reload the page before saving again.");
        }

        await action(row);
        db.Entry(row).Property(static x => x.HistorySaved).IsModified = true; // Touch parent for residence-only edits.
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private async Task ChangeAsync(RentalApplication row, string next, string actor, string name, string? comment)
    {
        row.Changes.Add(new StatusChange
        {
            FromStatus = row.StatusId == 0 ? "" : row.StatusCode, ToStatus = next, ActorUserId = actor,
            ActorName = name, Comment = comment, ChangedAtUtc = clock.GetUtcNow().UtcDateTime
        });
        row.Status = await db.ApplicationStatuses.SingleAsync(x => x.Code == next);
    }

}