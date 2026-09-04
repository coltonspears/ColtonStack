using ColtonStack.Client.Extensions.Settings;
using ColtonStack.Client.Messages;
using ColtonStack.Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace ColtonStack.Client.ViewModels;

/// <summary>
/// The in-window Settings page: a section list on the left, the active section's view model on
/// the right. Sections come from <see cref="ISettingsRegistry"/> — the core contributes Profile,
/// extensions contribute theirs — so this class contains no knowledge of any setting at all.
/// </summary>
public sealed partial class SettingsViewModel(
    ISettingsRegistry registry,
    ISettingsStore store) : ObservableObject, IRecipient<SettingsRequestedMessage>
{
    public IReadOnlyList<SettingsSectionDefinition> Sections { get; } = registry.Sections;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveContent))]
    public partial SettingsSectionDefinition? ActiveSection { get; set; }

    public object? ActiveContent => ActiveSection?.Content;

    partial void OnActiveSectionChanged(SettingsSectionDefinition? value)
    {
        if (value is not null && !store.IsLoaded)
        {
            // First visit: pull the persisted values so sections render real state.
            _ = store.LoadAsync(CancellationToken.None);
        }
    }

    public void Receive(SettingsRequestedMessage message)
    {
        if (message.SectionId is { } id)
        {
            ActiveSection = Sections.FirstOrDefault(section => string.Equals(section.Id, id, StringComparison.OrdinalIgnoreCase)) ?? ActiveSection;
        }

        ActiveSection ??= Sections.Count > 0 ? Sections[0] : null;
    }
}
