// THE OLD WORLD — for demo contrast only. See LegacyTableAttribute.cs.
//
// Read this next to the modern MessageViewModel + ChatViewModel:
//   * every property needs a declared backing field AND a string name;
//   * "Txt" on line marked below compiles — and notifies exactly nothing at runtime;
//   * EnableAuditing = true is all it takes to inherit auditing, soft delete, dirty tracking
//     and self-saving — none of it unit-testable without the whole base class.

#pragma warning disable RS0030, CA1507 // Banned APIs and magic strings are the point of this file.

using System.Reflection;

namespace ColtonStack.Client.Legacy;

[LegacyTable("Messages")]
public sealed class LegacyMessageViewModel : LegacyEntityBase
{
    private string _text = string.Empty; // every property: one field + one string, forever

    private string _authorName = string.Empty;

    [LegacyColumn("text")]
    public string Text
    {
        get => _text;
        set => SetAndRaise(ref _text, value, "Text");
    }

    [LegacyColumn("author")]
    public string AuthorName
    {
        get => _authorName;
        set
        {
            _authorName = value;
            RaisePropertyChanged("AutherName"); // ← typo: compiles, notifies nothing, ever

            // ...and when one string isn't trustworthy enough:
            RaiseAllChanged();
        }
    }

    public LegacyMessageViewModel()
    {
        // Look how little it takes to get "enterprise features":
        EnableAuditing = true;
        AuditUser = "colton";
    }

    /// <summary>Mistyped column names fail the same way: at runtime, far from the cause.</summary>
    public static string ProbeSchemaColumns()
    {
        var columns = typeof(LegacyMessageViewModel)
            .GetProperties()
            .Select(property => property.GetCustomAttribute<LegacyColumnAttribute>()?.ColumnName ?? property.Name);

        return string.Join(", ", columns);
    }
}
