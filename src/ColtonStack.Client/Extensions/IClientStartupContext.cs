using ColtonStack.Client.Extensions.Attachments;
using ColtonStack.Client.Extensions.Commands;
using ColtonStack.Client.Extensions.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace ColtonStack.Client.Extensions;

/// <summary>
/// Everything an extension may touch, handed to it explicitly — no service locator. Each
/// registry is a plain list with a duplicate guard; the shell binds to whatever ends up in them.
/// </summary>
public interface IClientStartupContext
{
    /// <summary>Register view models and services for this extension. Runs before host build.</summary>
    IServiceCollection Services { get; }

    /// <summary>Navigation nodes: register sidebar panes here instead of editing a core enum.</summary>
    ISidebarPaneRegistry Panes { get; }

    /// <summary>Palette commands, slash commands and dynamic palette rows.</summary>
    ICommandRegistry Commands { get; }

    /// <summary>Pages of the in-window Settings view.</summary>
    ISettingsRegistry Settings { get; }

    /// <summary>Renderers for message attachment kinds this extension owns.</summary>
    IAttachmentRegistry Attachments { get; }

    /// <summary>
    /// Merges an extension-supplied ResourceDictionary (implicit DataTemplates for its view
    /// models, extension-local styles) into the application resources at startup.
    /// Use a pack URI, e.g. <c>pack://application:,,,/ColtonStack.Client;component/Extensions/Audit/AuditPaneTemplates.xaml</c>.
    /// </summary>
    void AddResourceDictionary(string source);
}
