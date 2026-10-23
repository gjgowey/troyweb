using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TroyWebProperty.Core.Entities;

public interface IRowVersioned
{

    byte[] RowVersion { get; set; }

}

public abstract class Entity : IRowVersioned
{

    public int Id { get; init; }
    public byte[] RowVersion { get; set; } = [];

}

public abstract class SeedableEntity : Entity
{

    public string? SeedKey { get; init; }

}

public sealed class PropertyManager : SeedableEntity
{

    public string UserId { get; init; } = "";

    [Required]
    [StringLength(100)]
    [Display(Name = "First name")]
    public string FirstName { get; init; } = "";

    [Required]
    [StringLength(100)]
    [Display(Name = "Last name")]
    public string LastName { get; init; } = "";

    [StringLength(1)]
    [RegularExpression(@"^[A-Za-z]$")]
    [Display(Name = "Middle initial")]
    public string? MiddleInitial { get; init; }

    [StringLength(20)] [PersonTitle] public string? Title { get; init; }

    [NotMapped] public string FullName => PersonNames.Format(Title, FirstName, MiddleInitial, LastName);

    [NotMapped] public string Name => FullName;
    public string Email { get; init; } = "";
    public string Phone { get; init; } = "";

}

public sealed class Applicant : SeedableEntity
{

    public string UserId { get; init; } = "";

    [Required]
    [StringLength(100)]
    [Display(Name = "First name")]
    public string FirstName { get; init; } = "";

    [Required]
    [StringLength(100)]
    [Display(Name = "Last name")]
    public string LastName { get; init; } = "";

    [StringLength(1)]
    [RegularExpression(@"^[A-Za-z]$")]
    [Display(Name = "Middle initial")]
    public string? MiddleInitial { get; init; }

    [StringLength(20)] [PersonTitle] public string? Title { get; init; }

    [NotMapped] public string FullName => PersonNames.Format(Title, FirstName, MiddleInitial, LastName);

    public string Email { get; init; } = "";
    public string Phone { get; init; } = "";

}

public sealed class Property : SeedableEntity
{

    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public int PropertyManagerId { get; init; }

    public PropertyManager PropertyManager
    {
        get => field ??
               throw new InvalidOperationException("PropertyManager has not been loaded or assigned.");
        init;
    }

}

public sealed class UnitType : Entity
{

    public required string Code { get; init; }
    public string Name { get; init; } = "";
    public bool IsActive { get; init; } = true;

}

public sealed class Unit : SeedableEntity
{

    public int PropertyId { get; set; }

    public Property Property
    {
        get => field ?? throw new InvalidOperationException("Property has not been loaded or assigned.");
        init;
    }

    public string Number { get; set; } = "";
    public int Bedrooms { get; set; }
    public int UnitTypeId { get; set; }

    public UnitType UnitType
    {
        get => field ?? throw new InvalidOperationException("UnitType has not been loaded or assigned.");
        init;
    }

    public decimal MonthlyRent { get; set; }
    public int LockVersion { get; init; }

}

public sealed class ApplicationStatus : Entity
{

    public required string Code { get; init; }
    public string Name { get; init; } = "";
    public int SortOrder { get; init; }

}

public sealed class RentalApplication : SeedableEntity
{

    public int ApplicantId { get; init; }

    public Applicant Applicant
    {
        get => field ?? throw new InvalidOperationException("Applicant has not been loaded or assigned.");
        init;
    }

    public int UnitId { get; init; }

    public Unit Unit
    {
        get => field ?? throw new InvalidOperationException("Unit has not been loaded or assigned.");
        init;
    }

    public int StatusId { get; init; }

    public ApplicationStatus Status
    {
        get => field ?? throw new InvalidOperationException("Status has not been loaded or assigned.");
        set;
    }

    [NotMapped] public string StatusCode => Status.Code;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? SubmittedAtUtc { get; set; }

    [Required]
    [StringLength(100)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = "";

    [Required]
    [StringLength(100)]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = "";

    [StringLength(1)]
    [RegularExpression(@"^[A-Za-z]$")]
    [Display(Name = "Middle initial")]
    public string? MiddleInitial { get; set; }

    [StringLength(20)] [PersonTitle] public string? Title { get; set; }

    [NotMapped] public string FullName => PersonNames.Format(Title, FirstName, MiddleInitial, LastName);

    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string CurrentAddress { get; set; } = "";
    public bool InformationSaved { get; set; }
    public bool HistorySaved { get; set; }
    public List<Residence> Residences { get; init; } = [];
    public List<StatusChange> Changes { get; init; } = [];

}

public sealed class Residence : Entity
{

    public int RentalApplicationId { get; init; }
    public string Address { get; set; } = "";
    public string LandlordName { get; set; } = "";
    public string LandlordPhone { get; set; } = "";
    public DateOnly MoveIn { get; set; }
    public DateOnly MoveOut { get; set; }

}

public sealed class Lease : Entity
{

    public int UnitId { get; init; }

    public Unit Unit
    {
        get => field ?? throw new InvalidOperationException("Unit has not been loaded or assigned.");
        init;
    }

    public int RentalApplicationId { get; init; }

    public RentalApplication Application
    {
        get => field ?? throw new InvalidOperationException("Application has not been loaded or assigned.");
        init;
    }

    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }

}

public sealed class StatusChange : Entity
{

    public int RentalApplicationId { get; init; }
    public string FromStatus { get; init; } = "";
    public string ToStatus { get; init; } = "";
    public string ActorUserId { get; init; } = "";
    public string ActorName { get; init; } = "";
    public DateTime ChangedAtUtc { get; init; }
    public string? Comment { get; init; }

}