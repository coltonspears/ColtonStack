using ColtonStack.Client.Extensions.Attachments;
using ColtonStack.Client.Extensions.Commands;
using ColtonStack.Client.Extensions.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace ColtonStack.Client.Extensions;

/// <summary>Concrete context handed to <see cref="IClientStartup.Configure"/>. Owns the registries for the Configure phase.</summary>
public sealed class ClientStartupContext(IServiceCollection services) : IClientStartupContext
{
    private readonly List<string> _resourceDictionaries = [];

    public IServiceCollection Services { get; } = services;

    public SidebarPaneRegistry PaneRegistry { get; } = new();

    public CommandRegistry CommandRegistry { get; } = new();

    public SettingsRegistry SettingsRegistry { get; } = new();

    public AttachmentRegistry AttachmentRegistry { get; } = new();

    ISidebarPaneRegistry IClientStartupContext.Panes => PaneRegistry;

    ICommandRegistry IClientStartupContext.Commands => CommandRegistry;

    ISettingsRegistry IClientStartupContext.Settings => SettingsRegistry;

    IAttachmentRegistry IClientStartupContext.Attachments => AttachmentRegistry;

    /// <summary>Collected during the Configure phase; merged into Application.Resources before the window shows.</summary>
    public IReadOnlyList<string> ResourceDictionaries => _resourceDictionaries;

    void IClientStartupContext.AddResourceDictionary(string source) => _resourceDictionaries.Add(source);

    /// <summary>Registers every registry as its interface and hands them the provider once the host is built.</summary>
    public void RegisterRegistries()
    {
        Services.AddSingleton<ISidebarPaneRegistry>(PaneRegistry);
        Services.AddSingleton<ICommandRegistry>(CommandRegistry);
        Services.AddSingleton<ISettingsRegistry>(SettingsRegistry);
        Services.AddSingleton<IAttachmentRegistry>(AttachmentRegistry);
    }

    public void Attach(IServiceProvider provider)
    {
        PaneRegistry.Attach(provider);
        CommandRegistry.Attach(provider);
        SettingsRegistry.Attach(provider);
        AttachmentRegistry.Attach(provider);
    }
}
