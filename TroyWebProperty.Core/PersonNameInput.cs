using System.ComponentModel.DataAnnotations;

namespace TroyWebProperty.Core;

// Shared input metadata; domain entities retain their independent EF mappings.
public abstract class PersonNameInput
{

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

    public string FullName => PersonNames.Format(Title, FirstName, MiddleInitial, LastName);

}