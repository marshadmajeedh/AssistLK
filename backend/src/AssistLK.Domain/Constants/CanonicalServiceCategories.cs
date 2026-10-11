namespace AssistLK.Domain.Constants;

public static class CanonicalServiceCategories
{
    public const string Plumbing = "Plumbing";
    public const string Electrical = "Electrical";
    public const string VehicleRepair = "Vehicle Repair";
    public const string ApplianceRepair = "Appliance Repair";
    public const string Unclassified = "Unclassified";

    public static readonly IReadOnlySet<string> AllowedCategoryHints = new HashSet<string>(StringComparer.Ordinal)
    {
        Plumbing,
        Electrical,
        VehicleRepair,
        ApplianceRepair
    };

    /// <summary>
    /// Validates whether the given hint is an allowed CategoryHint.
    /// Accepted values: null, empty/whitespace (normalized to null), "Plumbing", "Electrical", "Vehicle Repair", "Appliance Repair".
    /// Values such as "Vehicle Assistance", "Cleaning", "vehicle", or arbitrary text return false.
    /// </summary>
    public static bool IsValidHint(string? hint, out string? normalized)
    {
        if (string.IsNullOrWhiteSpace(hint))
        {
            normalized = null;
            return true;
        }

        var trimmed = hint.Trim();
        foreach (var allowed in AllowedCategoryHints)
        {
            if (string.Equals(allowed, trimmed, StringComparison.Ordinal))
            {
                normalized = allowed;
                return true;
            }
        }

        normalized = null;
        return false;
    }
}
