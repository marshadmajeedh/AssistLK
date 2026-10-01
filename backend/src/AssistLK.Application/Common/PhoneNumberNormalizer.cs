using System.Text.RegularExpressions;

namespace AssistLK.Application.Common;

public static class PhoneNumberNormalizer
{
    private static readonly Regex CanonicalSriLankanMobileRegex =
        new(@"^\+947[0-9]{8}$", RegexOptions.Compiled);

    /// <summary>
    /// Normalizes a user-provided phone number into canonical Sri Lankan mobile E.164 format (+947XXXXXXXX).
    /// Returns true if valid and outputs the normalized number; otherwise false.
    /// </summary>
    public static bool TryNormalizeSriLankanMobile(string? rawPhoneNumber, out string normalizedPhoneNumber)
    {
        normalizedPhoneNumber = string.Empty;

        if (string.IsNullOrWhiteSpace(rawPhoneNumber))
        {
            return false;
        }

        // Remove whitespace, dashes, and parentheses
        var cleaned = Regex.Replace(rawPhoneNumber.Trim(), @"[\s\-\(\)\.]", "");

        string candidate;

        if (cleaned.StartsWith("+94", StringComparison.Ordinal))
        {
            candidate = cleaned;
        }
        else if (cleaned.StartsWith("0094", StringComparison.Ordinal))
        {
            candidate = "+" + cleaned[2..];
        }
        else if (cleaned.StartsWith("94", StringComparison.Ordinal) && cleaned.Length == 11)
        {
            candidate = "+" + cleaned;
        }
        else if (cleaned.StartsWith("07", StringComparison.Ordinal) && cleaned.Length == 10)
        {
            candidate = "+94" + cleaned[1..];
        }
        else if (cleaned.StartsWith("7", StringComparison.Ordinal) && cleaned.Length == 9)
        {
            candidate = "+94" + cleaned;
        }
        else
        {
            return false;
        }

        if (CanonicalSriLankanMobileRegex.IsMatch(candidate))
        {
            normalizedPhoneNumber = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Masks a canonical phone number (+94771234567) into customer-friendly masked format (+94 77 *** *567).
    /// </summary>
    public static string Mask(string canonicalPhoneNumber)
    {
        if (string.IsNullOrWhiteSpace(canonicalPhoneNumber) || canonicalPhoneNumber.Length != 12)
        {
            return canonicalPhoneNumber;
        }

        // "+94" + "77" + "1234" + "567" -> "+94 77 *** *567"
        var prefix = canonicalPhoneNumber[..3]; // "+94"
        var network = canonicalPhoneNumber.Substring(3, 2); // "77"
        var suffix = canonicalPhoneNumber.Substring(9, 3); // "567"

        return $"{prefix} {network} *** *{suffix}";
    }
}
