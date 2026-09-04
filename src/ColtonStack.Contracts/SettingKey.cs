namespace ColtonStack.Contracts;

/// <summary>
/// The shape of a settings key: dotted lower-case segments such as <c>pokemon.artwork</c>.
/// Shared so the client refuses a bad key before the request and the server refuses it before
/// the database — one rule, two enforcement points, no drift.
/// </summary>
public static class SettingKey
{
    public const int MaxLength = 64;

    /// <summary>Each dot-separated segment starts with a letter and continues with letters or digits, all ASCII lower-case.</summary>
    public static bool IsValid(string? key)
    {
        if (key is not { Length: > 0 and <= MaxLength })
        {
            return false;
        }

        foreach (var segment in key.Split('.'))
        {
            if (segment.Length == 0 || !char.IsAsciiLetterLower(segment[0]))
            {
                return false;
            }

            foreach (var c in segment)
            {
                if (!char.IsAsciiLetterLower(c) && !char.IsAsciiDigit(c))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
