using TroyWebProperty.Core;

namespace TroyWebProperty.Infrastructure.Data;

internal static class SeedCatalog
{

    public static readonly (string Code, string Name)[] Statuses =
        [.. ApplicationRules.Statuses.Select(static x => (x, x))];

    public static readonly (string Code, string Name, bool IsActive)[] UnitTypes =
        [("Apartment", "Apartment", true), ("Studio", "Studio", true), ("Loft", "Loft (retired)", false)];

}