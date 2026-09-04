using ColtonStack.Client.Extensions.Commands;
using ColtonStack.Client.Extensions.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace ColtonStack.Client.Extensions.Audit;

/// <summary>
/// The audit trail, shipped as a feature extension rather than a core screen: registers its
/// view models, its navigation node, a palette command, a settings section and its own XAML
/// (implicit DataTemplates) via <see cref="IClientStartupContext.AddResourceDictionary"/>.
/// The core app requires zero edits to gain any of it.
///
/// Its server half lives in <c>ColtonStack.Server.Extensions.AuditExtension</c>, which owns
/// the <c>/api/audit</c> endpoint. One feature, two planes, one extension each.
/// </summary>
public sealed class AuditPaneExtension : IClientStartup
{
    public void Configure(IClientStartupContext context)
    {
        context.Services.AddSingleton<AuditViewModel>();
        context.Services.AddSingleton<AuditSettingsViewModel>();

        context.Panes.Register(new SidebarPaneDefinition(
            id: "audit",
            title: "Audit trail",
            iconGlyph: "\uE81C", // MDL2 "History"
            order: 30,
            contentFactory: services => services.GetRequiredService<AuditViewModel>(),
            activatedAsync: services => services.GetRequiredService<AuditViewModel>().LoadCommand.ExecuteAsync(null)));

        context.Settings.Register(new SettingsSectionDefinition(
            id: "audit",
            title: "Audit trail",
            description: "How much history the audit pane loads",
            iconGlyph: "\uE81C",
            order: 30,
            contentFactory: services => services.GetRequiredService<AuditSettingsViewModel>()));

        context.Commands.Register(new CommandDefinition(
            id: "audit.refresh",
            title: "Refresh audit trail",
            description: "Reload the newest audit entries from the server",
            iconGlyph: "\uE72C",
            category: "Audit",
            keywords: ["history", "log"],
            executeAsync: (services, invocation) => services.GetRequiredService<AuditViewModel>().LoadCommand.ExecuteAsync(null)));

        context.AddResourceDictionary(
            "pack://application:,,,/ColtonStack.Client;component/Extensions/Audit/AuditPaneTemplates.xaml");
    }
}
