using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using TroyWebProperty.Core;
using TroyWebProperty.Core.Entities;

namespace TroyWebProperty.Web.Models;

public sealed class PropertyForm
{

    [Base64String] public string? RowVersion { get; init; }

    public int Id { get; init; }
    public bool Delete { get; init; }
    [Required] [StringLength(150)] public string Name { get; init; } = "";
    [Required] [StringLength(300)] public string Address { get; init; } = "";

}

public sealed class UnitForm
{

    [Base64String] public string? RowVersion { get; init; }

    public int Id { get; init; }
    public int PropertyId { get; init; }
    public bool Delete { get; init; }
    [Required] [StringLength(30)] public string Number { get; init; } = "";
    [Range(0, 20)] public int Bedrooms { get; init; }

    [Range(typeof(decimal), "0.01", "1000000")]
    public decimal MonthlyRent { get; init; }

    [Range(1, int.MaxValue)] public int UnitTypeId { get; init; }
    [BindNever] public List<SelectListItem> Types { get; set; } = [];

}

public sealed record PropertyRow(int Id, string Name, string Address, IReadOnlyList<UnitRow> Units);

public sealed record UnitRow(
    int Id,
    int PropertyId,
    string PropertyName,
    string Address,
    string Number,
    int Bedrooms,
    decimal Rent,
    string Type);

public sealed record ApplicationRow(
    int Id,
    string Applicant,
    string Property,
    string Unit,
    string Status,
    DateTime CreatedAt);

public sealed class ApplicationListModel
{

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public int TotalCount { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));


    public string? Status { get; init; }
    public int? PropertyId { get; init; }
    public List<SelectListItem> Properties { get; init; } = [];
    public IReadOnlyList<ApplicationRow> Rows { get; init; } = [];

}

public sealed class ApplicationEditModel
{

    public int Id { get; init; }
    [Required] [Base64String] public string RowVersion { get; set; } = "";
    [Range(1, 3)] public int Step { get; init; } = 1;
    public ApplicantInformation Info { get; set; } = new();
    [BindNever] public bool ReadOnly { get; init; }
    [BindNever] public bool Manager { get; init; }
    [BindNever] public bool CanSubmit { get; init; }
    [BindNever] public bool CanWithdraw { get; init; }
    [BindNever] public string Status { get; init; } = "";
    [BindNever] public string UnitDescription { get; init; } = "";
    [BindNever] public string? ReturnComment { get; init; }
    [BindNever] public IReadOnlyList<Residence> Residences { get; init; } = [];
    [BindNever] public IReadOnlyList<StatusChange> Changes { get; init; } = [];
    [BindNever] public Lease? Lease { get; init; }

}

public sealed class ResidenceForm
{

    public int ApplicationId { get; init; }
    public int Id { get; init; }
    [Required] [Base64String] public string RowVersion { get; init; } = "";
    public bool Delete { get; init; }
    [Required] [StringLength(300)] public string Address { get; init; } = "";
    [Required] [StringLength(150)] public string LandlordName { get; init; } = "";
    [Required] [Phone] [StringLength(50)] public string LandlordPhone { get; init; } = "";
    [DataType(DataType.Date)] public DateOnly MoveIn { get; init; }
    [DataType(DataType.Date)] public DateOnly MoveOut { get; init; }

}

public sealed class ReviewForm
{

    public int Id { get; init; }
    [Required] [Base64String] public string RowVersion { get; init; } = "";

    [Required]
    [RegularExpression("^(Approved|Returned|Denied)$")]
    public string Outcome { get; init; } = "Approved";

    [StringLength(2000)] public string? Comment { get; init; }
    [DataType(DataType.Date)] public DateOnly StartDate { get; init; }

}