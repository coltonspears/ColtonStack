// THE OLD WORLD — for demo contrast only. See LegacyTableAttribute.cs.

#pragma warning disable RS0030 // Banned APIs are the point of this file.

using System.ComponentModel;
using System.Reflection;

namespace ColtonStack.Client.Legacy;

/// <summary>
/// The legacy base class: every model and view model inherits ALL of this, always.
///
/// Features you get whether you asked for them or not:
///   1. Change notification that walks properties by reflection and accepts arbitrary strings.
///   2. "Built-in auditing" — set <see cref="EnableAuditing"/> to true and saves stamp audit
///      fields and write an audit trail, via reflection, inside the base class.
///   3. Soft delete via <see cref="IsDeleted"/> baked into every save.
///   4. Dirty tracking that reflects over every property on every write.
///   5. A Save() that knows SQL — so a UI class owns persistence concerns forever.
///   6. RaiseAllChanged(), which reflects over every property and raises everything.
///
/// Each subclass is heavier to construct, impossible to test without a database-shaped world,
/// and one mistyped string away from a runtime bug no compiler can see.
/// </summary>
public abstract class LegacyEntityBase : INotifyPropertyChanged
{
    private static readonly Dictionary<Type, Dictionary<string, PropertyInfo>> PropertyCache = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    // ---- Feature: auditing by bool -----------------------------------------
    public bool EnableAuditing { get; set; }

    public string? AuditUser { get; set; }

    public DateTimeOffset? CreatedAtUtc { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAtUtc { get; private set; }

    public string? ModifiedBy { get; private set; }

    // ---- Feature: soft delete on everything --------------------------------
    public bool IsDeleted { get; private set; }

    // ---- Feature: dirty tracking via reflection ----------------------------
    public bool IsDirty { get; private set; }

    /// <summary>
    /// Reflects every column property and builds the INSERT. When <see cref="EnableAuditing"/>
    /// is on, silently stamps audit columns and writes the trail row. The model "saves itself",
    /// so it also needs to know about SQL, auditing, transactions and time.
    /// </summary>
    public LegacySaveResult Save()
    {
        var table = GetType().GetCustomAttribute<LegacyTableAttribute>()?.TableName
            ?? throw new InvalidOperationException($"{GetType().Name} has no [{nameof(LegacyTableAttribute)}].");

        var columns = GetColumnProperties();
        if (columns.Count == 0)
        {
            throw new InvalidOperationException($"{GetType().Name} declares no [{nameof(LegacyColumnAttribute)}] properties.");
        }

        if (EnableAuditing)
        {
            Stamp("CreatedBy", AuditUser ?? "unknown");
            Stamp("CreatedAtUtc", DateTimeOffset.UtcNow);
            Stamp("ModifiedBy", AuditUser ?? "unknown");
            Stamp("ModifiedAtUtc", DateTimeOffset.UtcNow);
        }

        var sql = LegacySqlMapper.BuildInsert(table, columns.Keys);
        LegacySqlMapper.Execute(sql, columns.Values.Select(property => property.GetValue(this)));

        if (EnableAuditing)
        {
            WriteAuditTrail(table, columns);
        }

        IsDirty = false;
        return new LegacySaveResult(table, columns.Count, Audited: EnableAuditing);
    }

    /// <summary>Soft delete: the row stays, every query everywhere must remember to filter.</summary>
    public void Delete() => IsDeleted = true;

    /// <summary>Reflection-based change notification: resolves the property by string.</summary>
    protected void RaisePropertyChanged(string propertyName)
    {
        // A typo in propertyName compiles and silently notifies nothing.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        IsDirty = true;
    }

    /// <summary>Legacy convenience: set-and-notify against an explicitly declared backing field.</summary>
    protected bool SetAndRaise<T>(ref T field, T value, string propertyName)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        RaisePropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// When none of the string names can be trusted: reflect over everything and raise it all.
    /// Every bound control re-reads every property. ("Performance" was not a design goal.)
    /// </summary>
    protected void RaiseAllChanged()
    {
        foreach (var property in GetColumnProperties().Values)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property.Name));
        }
    }

    private Dictionary<string, PropertyInfo> GetColumnProperties()
    {
        var type = GetType();
        if (!PropertyCache.TryGetValue(type, out var cached))
        {
            cached = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => (Property: property, Column: property.GetCustomAttribute<LegacyColumnAttribute>()?.ColumnName))
                .Where(entry => entry.Column is not null)
                .ToDictionary(entry => entry.Column!, entry => entry.Property, StringComparer.OrdinalIgnoreCase);
            PropertyCache[type] = cached;
        }

        return cached;
    }

    private void Stamp(string auditProperty, object value)
    {
        // Reflection "just works" — until the property is renamed and this returns null here,
        // which is swallowed, which is why audit columns are sometimes silently empty.
        var property = GetType().GetProperty(auditProperty);
        property?.SetValue(this, value);
    }

    private void WriteAuditTrail(string table, Dictionary<string, PropertyInfo> columns)
    {
        var before = string.Join(", ", columns.Select(entry => $"{entry.Key}={entry.Value.GetValue(this)}"));
        LegacySqlMapper.ExecuteAuditTrail(table, AuditUser ?? "unknown", before);
    }
}
