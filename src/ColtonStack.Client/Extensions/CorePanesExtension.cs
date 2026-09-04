using ColtonStack.Client.Extensions.Commands;
using ColtonStack.Client.Extensions.Settings;
using ColtonStack.Client.Messages;
using ColtonStack.Client.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace ColtonStack.Client.Extensions;

/// <summary>
/// Registers the workspace's original panes, palette commands and the Profile settings section
/// — through the same extension contract third parties use. The shell ships no private pane,
/// command or settings API: channels, people and profile are just contributions that shipped
/// first. Reordering or removing them is a one-line change in the composition root's list.
/// </summary>
public sealed class CorePanesExtension : IClientStartup
{
    public void Configure(IClientStartupContext context)
    {
        context.Services.AddSingleton<ProfileSettingsViewModel>();

        RegisterPanes(context);
        RegisterSettings(context);
        RegisterNavigationSources(context);
        RegisterWorkspaceCommands(context);
        RegisterChatCommands(context);
    }

    private static void RegisterPanes(IClientStartupContext context)
    {
        context.Panes.Register(new SidebarPaneDefinition(
            id: "channels",
            title: "Home — channels",
            iconGlyph: "\uE80F",
            order: 10,
            contentFactory: services => services.GetRequiredService<ChannelListViewModel>(),
            activatedAsync: services => LoadChannelsOnceAsync(services.GetRequiredService<ChannelListViewModel>())));

        context.Panes.Register(new SidebarPaneDefinition(
            id: "people",
            title: "People",
            iconGlyph: "\uE716",
            order: 20,
            contentFactory: services => services.GetRequiredService<PeopleViewModel>(),
            activatedAsync: services => LoadPeopleOnceAsync(services.GetRequiredService<PeopleViewModel>())));
    }

    private static void RegisterSettings(IClientStartupContext context)
    {
        context.Settings.Register(new SettingsSectionDefinition(
            id: "profile",
            title: "Profile",
            description: "Your name and avatar across the workspace",
            iconGlyph: "\uE77B",
            order: 10,
            contentFactory: services =>
            {
                var profile = services.GetRequiredService<ProfileSettingsViewModel>();
                _ = profile.LoadCommand.ExecuteAsync(null);
                return profile;
            }));
    }

    /// <summary>Dynamic palette rows: channels, panes and settings sections, read live from their registries.</summary>
    private static void RegisterNavigationSources(IClientStartupContext context)
    {
        // Jump to a channel: a dynamic source reading the sidebar's live list. The lambda owns
        // the ChannelListViewModel reference; the palette only ever sees CommandItem rows.
        context.Commands.AddSource(new CommandItemSource("channels", (services, query, _) =>
        {
            var channels = services.GetRequiredService<ChannelListViewModel>();
            IReadOnlyList<CommandItem> items = [.. channels.Channels
                .Where(channel => query.Length == 0 || channel.Name.Contains(query.TrimStart('#'), StringComparison.OrdinalIgnoreCase))
                .Select(channel => new CommandItem(
                    $"#{channel.Name}",
                    channel.Summary.Topic.Length > 0 ? channel.Summary.Topic : "Jump to channel",
                    "\uE8BD",
                    "Channels",
                    _ =>
                    {
                        channels.SelectedChannel = channel;
                        services.GetRequiredService<IMessenger>().Send(new PaneRequestedMessage("channels"));
                        return Task.CompletedTask;
                    }))];
            return Task.FromResult(items);
        }));

        // Open a pane: one row per registered pane, whatever extensions added.
        context.Commands.AddSource(new CommandItemSource("panes", (services, query, _) =>
        {
            var panes = services.GetRequiredService<ISidebarPaneRegistry>().Panes;
            var messenger = services.GetRequiredService<IMessenger>();
            IReadOnlyList<CommandItem> items = [.. panes
                .Where(pane => query.Length == 0 || pane.Title.Contains(query, StringComparison.OrdinalIgnoreCase) || "pane".Contains(query, StringComparison.OrdinalIgnoreCase))
                .Select(pane => new CommandItem(
                    $"Open {pane.Title}",
                    "Sidebar pane",
                    pane.IconGlyph,
                    "Navigate",
                    _ =>
                    {
                        messenger.Send(new PaneRequestedMessage(pane.Id));
                        return Task.CompletedTask;
                    }))];
            return Task.FromResult(items);
        }));

        RegisterSettingsSource(context);
    }

    /// <summary>Open a settings section: one row per registered section, over the settings registry.</summary>
    private static void RegisterSettingsSource(IClientStartupContext context)
    {
        context.Commands.AddSource(new CommandItemSource("settings", (services, query, _) =>
        {
            var sections = services.GetRequiredService<ISettingsRegistry>().Sections;
            var messenger = services.GetRequiredService<IMessenger>();
            IReadOnlyList<CommandItem> items = [.. sections
                .Where(section => query.Length == 0 || section.Title.Contains(query, StringComparison.OrdinalIgnoreCase) || "settings preferences".Contains(query, StringComparison.OrdinalIgnoreCase))
                .Select(section => new CommandItem(
                    $"Settings: {section.Title}",
                    section.Description,
                    section.IconGlyph,
                    "Settings",
                    _ =>
                    {
                        messenger.Send(new SettingsRequestedMessage(section.Id));
                        return Task.CompletedTask;
                    }))];
            return Task.FromResult(items);
        }));
    }

    /// <summary>Static commands that flip workspace-level state on other view models.</summary>
    private static void RegisterWorkspaceCommands(IClientStartupContext context)
    {
        context.Commands.Register(new CommandDefinition(
            id: "workspace.simulator",
            title: "Toggle chat simulator",
            description: "Start or stop the server's fake teammates",
            iconGlyph: "\uE768",
            category: "Workspace",
            keywords: ["sim", "bots", "activity"],
            executeAsync: (services, _) =>
            {
                var status = services.GetRequiredService<StatusBarViewModel>();
                status.SimEnabled = !status.SimEnabled;
                return Task.CompletedTask;
            }));

        context.Commands.Register(new CommandDefinition(
            id: "workspace.chaos",
            title: "Toggle chaos mode",
            description: "Make ~40% of API requests fail and watch the resilience pipeline",
            iconGlyph: "\uE945",
            category: "Workspace",
            keywords: ["failure", "resilience", "retry", "circuit"],
            executeAsync: (services, _) =>
            {
                var status = services.GetRequiredService<StatusBarViewModel>();
                status.ChaosEnabled = !status.ChaosEnabled;
                return Task.CompletedTask;
            }));

        context.Commands.Register(new CommandDefinition(
            id: "workspace.diagnostics",
            title: "Toggle diagnostics panel",
            description: "Live ILogger output: retries, reconnects, failures",
            iconGlyph: "\uE9D9",
            category: "Workspace",
            keywords: ["logs", "log", "debug"],
            executeAsync: (services, _) =>
            {
                var diagnostics = services.GetRequiredService<DiagnosticsViewModel>();
                diagnostics.IsOpen = !diagnostics.IsOpen;
                return Task.CompletedTask;
            }));

        context.Commands.Register(new CommandDefinition(
            id: "channels.create",
            title: "Create a channel",
            description: "Open the new-channel box in the sidebar",
            iconGlyph: "\uE710",
            category: "Channels",
            keywords: ["new", "add"],
            executeAsync: (services, _) =>
            {
                services.GetRequiredService<IMessenger>().Send(new PaneRequestedMessage("channels"));
                services.GetRequiredService<ChannelListViewModel>().IsAddingChannel = true;
                return Task.CompletedTask;
            }));
    }

    /// <summary>Commands that act on the current conversation, including the smallest slash command.</summary>
    private static void RegisterChatCommands(IClientStartupContext context)
    {
        context.Commands.Register(new CommandDefinition(
            id: "chat.search",
            title: "Search messages",
            description: "Filter the current conversation",
            iconGlyph: "\uE721",
            category: "Chat",
            keywords: ["find", "filter"],
            executeAsync: (services, _) =>
            {
                services.GetRequiredService<ChatViewModel>().IsSearchActive = true;
                return Task.CompletedTask;
            }));

        // The slash form of "shrug" — the smallest possible slash command, so the mechanism is
        // visible without any server involvement.
        context.Commands.Register(new CommandDefinition(
            id: "chat.shrug",
            title: "Shrug",
            description: "Append ¯\\_(ツ)_/¯ to your message",
            iconGlyph: "\uE76E",
            category: "Chat",
            slashName: "shrug",
            argumentHint: "optional text",
            executeAsync: async (services, invocation) =>
            {
                if (invocation.ChannelId is not { } channelId)
                {
                    return;
                }

                var text = invocation.Argument.Length > 0 ? $"{invocation.Argument} ¯\\_(ツ)_/¯" : "¯\\_(ツ)_/¯";
                var api = services.GetRequiredService<Services.IColtonStackApiClient>();
                await api.SendMessageAsync(channelId, text, invocation.CancellationToken);
            }));
    }

    private static async Task LoadChannelsOnceAsync(ChannelListViewModel channels)
    {
        // The initial load doubles as the first activation; a failed first load retries here.
        if (channels.Channels.Count == 0)
        {
            await channels.LoadCommand.ExecuteAsync(null);
        }
    }

    private static async Task LoadPeopleOnceAsync(PeopleViewModel people)
    {
        if (people.People.Count == 0)
        {
            await people.LoadCommand.ExecuteAsync(null);
        }
    }
}
