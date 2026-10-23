using System.ComponentModel.DataAnnotations;

namespace TroyWebProperty.Core;

public static class PersonNames
{

    public static IEnumerable<string> Titles { get; } = Array.AsReadOnly([
        "Mr.", "Mrs.", "Ms.", "Miss", "Mx.", "Dr.", "Prof.", "Rev.", "Fr.", "Sr.", "Br.",
        "Rabbi", "Imam", "Pastor", "Hon.", "Sir", "Dame", "Lord", "Lady", "Capt.", "Maj.", "Col.", "Gen."
    ]);

    internal static string Format(string? title, string first, string? middle, string last)
    {
        return string.Join(" ", new[] { title, first, string.IsNullOrWhiteSpace(middle) ? null : middle + ".", last }
            .Where(static x => !string.IsNullOrWhiteSpace(x)));
    }

}

[AttributeUsage(AttributeTargets.Property)]
internal sealed class PersonTitleAttribute : ValidationAttribute
{

    public PersonTitleAttribute()
    {
        ErrorMessage = "Choose a title from the list.";
    }

    public override bool IsValid(object? value)
    {
        return value is null || value is string text &&
            (text.Length == 0 || PersonNames.Titles.Contains(text));
    }

}