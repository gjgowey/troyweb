using System.ComponentModel.DataAnnotations;
using TroyWebProperty.Core;

namespace TroyWebProperty.Web.Models;

public sealed class RegisterViewModel : PersonNameInput
{

    [Required]
    [RegularExpression("^(Applicant|PropertyManager)$")]
    public string Role { get; init; } = "Applicant";

    [Required] [EmailAddress] public string Email { get; init; } = "";

    [Required]
    [StringLength(100, MinimumLength = 1)]
    [DataType(DataType.Password)]
    public string Password { get; init; } = "";

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password))]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; init; } = "";

    public string? ReturnUrl { get; set; }

}

public sealed class LoginViewModel
{

    [Required] [EmailAddress] public string Email { get; init; } = "";

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; init; } = "";

    [Display(Name = "Remember me")] public bool RememberMe { get; init; }
    public string? ReturnUrl { get; set; }

}