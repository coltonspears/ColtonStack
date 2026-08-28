namespace ColtonStack.Client.ViewModels;

/// <summary>One tiny, testable function shared by every avatar in the app — no base class required.</summary>
public static class NameInitials
{
    public static string From(string displayName) => string.Concat(
        displayName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2)
            .Select(part => part[0]))
        .ToUpperInvariant();
}
