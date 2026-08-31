using ColtonStack.Client.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ColtonStack.Client.Extensions;

/// <summary>
/// Registers the workspace's original panes — through the same extension contract third
/// parties use. The shell ships no private pane API: channels and people are just panes
/// that shipped first. Swapping them for replacements, reordering, or removing them is a
/// one-line change in the composition root's extension list.
/// </summary>
public sealed class CorePanesExtension : IClientStartup
{
    public void Configure(IClientStartupContext context)
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