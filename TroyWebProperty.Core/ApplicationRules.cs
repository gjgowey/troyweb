using System.ComponentModel.DataAnnotations;
using TroyWebProperty.Core.Entities;

namespace TroyWebProperty.Core;

public sealed class RuleException(string message) : Exception(message);

public static class ApplicationRules
{

    public static readonly string[] Statuses = ["Draft", "Submitted", "Returned", "Approved", "Denied", "Withdrawn"];

    public static bool CanEdit(string status)
    {
        return status is "Draft" or "Returned";
    }

    public static bool CanWithdraw(string status)
    {
        return status is "Draft" or "Returned" or "Submitted";
    }

    public static bool CoversToday(DateOnly start, DateOnly end, DateOnly today)
    {
        return start <= today && today < end;
    }

    public static void RequireEditable(string status)
    {
        if (!CanEdit(status))
        {
            throw new RuleException("Only Draft or Returned applications can be edited.");
        }
    }

    public static void Submit(RentalApplication application, bool occupied)
    {
        RequireEditable(application.StatusCode);

        if (!application.InformationSaved || !application.HistorySaved)
        {
            throw new RuleException("Save both sections before submitting.");
        }

        if (occupied)
        {
            throw new RuleException("This unit has an active lease and is no longer available.");
        }
    }

    public static void Review(string status, string outcome, string? comment, bool occupied)
    {
        if (status != "Submitted")
        {
            throw new RuleException("Only Submitted applications can be reviewed.");
        }

        if (outcome is not ("Approved" or "Returned" or "Denied"))
        {
            throw new RuleException("Choose a valid review outcome.");
        }

        if (outcome is "Returned" or "Denied" && string.IsNullOrWhiteSpace(comment))
        {
            throw new RuleException("A comment is required for Return and Deny.");
        }

        if (outcome == "Approved" && occupied)
        {
            throw new RuleException("This unit already has an active lease. Approval was not saved.");
        }
    }

    public static void UnitType(bool active, int selected, int? original)
    {
        if (!active && selected != original)
        {
            throw new RuleException("An inactive unit type cannot be selected for another unit.");
        }
    }

    public static void ResidenceDates(DateOnly moveIn, DateOnly moveOut, DateOnly today)
    {
        if (moveIn == default || moveOut == default || moveOut < moveIn || moveOut > today)
        {
            throw new RuleException("Move-out must be on or after move-in and no later than today.");
        }
    }

    public static void History(IReadOnlyCollection<Residence> residences, DateOnly today)
    {
        if (residences.Count == 0)
        {
            throw new RuleException("Add at least one prior residence before continuing.");
        }

        foreach (var residence in residences)
        {
            ResidenceDates(residence.MoveIn, residence.MoveOut, today);

            if (string.IsNullOrWhiteSpace(residence.Address) || string.IsNullOrWhiteSpace(residence.LandlordName)
                                                             || string.IsNullOrWhiteSpace(residence.LandlordPhone))
            {
                throw new RuleException("Complete every residence.");
            }
        }
    }

}

public sealed class ApplicantInformation : PersonNameInput
{

    [Required] [Phone] [StringLength(50)] public string Phone { get; init; } = "";

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; init; } = "";

    [Required] [StringLength(300)] public string CurrentAddress { get; init; } = "";

    public IReadOnlyList<ValidationResult> Validate()
    {
        var errors = new List<ValidationResult>();
        Validator.TryValidateObject(this, new ValidationContext(this), errors, true);

        return errors;
    }

}