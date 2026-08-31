using Microsoft.Extensions.DependencyInjection;

namespace ColtonStack.Client.Extensions;

/// <summary>Everything an extension may touch, handed to it explicitly — no service locator.</summary>
public interface IClientStartupContext
{
    /// <summary>Register view models and services for this extension. Runs before host build.</summary>
    IServiceCollection Services { get; }

    /// <summary>Navigation nodes: register sidebar panes here instead of editing a core enum.</summary>
    ISidebarPaneRegistry Panes { get; }

    /// <summary>
    /// Merges an extension-supplied ResourceDictionary (implicit DataTemplates for its pane
    /// view models, extension-local styles) into the application resources at startup.
    /// Use a pack URI, e.g. <c>pack://application:,,,/ColtonStack.Client;component/Extensions/Audit/AuditPaneTemplates.xaml</c>.
    /// </summary>
    void AddResourceDictionary(string source);
}