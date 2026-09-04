namespace ColtonStack.Client.Extensions.Settings;

/// <summary>Extension-facing registry of Settings sections. The shell renders whatever is here, ordered by <c>Order</c>.</summary>
public interface ISettingsRegistry
{
    IReadOnlyList<SettingsSectionDefinition> Sections { get; }

    /// <summary>Adds a section; a duplicate id throws at startup.</summary>
    void Register(SettingsSectionDefinition section);
}
