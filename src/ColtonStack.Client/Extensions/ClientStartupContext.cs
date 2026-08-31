using Microsoft.Extensions.DependencyInjection;

namespace ColtonStack.Client.Extensions;

/// <summary>Concrete context handed to <see cref="IClientStartup.Configure"/>.</summary>
public sealed class ClientStartupContext(IServiceCollection services, SidebarPaneRegistry panes) : IClientStartupContext
{
    private readonly List<string> _resourceDictionaries = [];

    public IServiceCollection Services { get; } = services;

    public ISidebarPaneRegistry Panes { get; } = panes;

    /// <summary>Collected during the Configure phase; merged into Application.Resources after the host starts.</summary>
    public IReadOnlyList<string> ResourceDictionaries => _resourceDictionaries;

    void IClientStartupContext.AddResourceDictionary(string source) => _resourceDictionaries.Add(source);
}