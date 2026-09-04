namespace ColtonStack.Client.Extensions.Settings;

/// <summary>Concrete settings-section registry; mirrors <see cref="SidebarPaneRegistry"/>.</summary>
public sealed class SettingsRegistry : ISettingsRegistry
{
    private readonly List<SettingsSectionDefinition> _sections = [];
    private IServiceProvider? _services;

    public IReadOnlyList<SettingsSectionDefinition> Sections => [.. _sections.OrderBy(section => section.Order).ThenBy(section => section.Title, StringComparer.OrdinalIgnoreCase)];

    public void Register(SettingsSectionDefinition section)
    {
        if (_sections.Any(existing => string.Equals(existing.Id, section.Id, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"A settings section with id '{section.Id}' is already registered.");
        }

        _sections.Add(section);
        if (_services is { } services)
        {
            section.Attach(services);
        }
    }

    /// <summary>Called once by the composition root after the host is built.</summary>
    public void Attach(IServiceProvider services)
    {
        _services = services;
        foreach (var section in _sections)
        {
            section.Attach(services);
        }
    }
}
