using ColtonStack.Client.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ColtonStack.Client.Extensions;

/// <summary>
/// The audit trail, shipped as a feature extension rather than a core screen: registers its
/// view model, its navigation node and its own XAML (implicit DataTemplates) via
/// <see cref="IClientStartupContext.AddResourceDictionary"/>. The core app requires zero
/// edits to gain this pane — no enum, no shared file, no release of unrelated code.
///
/// Its server half lives in <c>ColtonStack.Server.Extensions.AuditExtension</c>, which owns
/// the <c>/api/audit</c> endpoint. One feature, two planes, one extension each.
/// </summary>
public sealed class AuditPaneExtension : IClientStartup
{
    public void Configure(IClientStartupContext context)
    {
        context.Services.AddSingleton<AuditViewModel>();

        context.Panes.Register(new SidebarPaneDefinition(
            id: "audit",
            title: "Audit trail",
            iconGlyph: "\uE81C", // MDL2 "History"
            order: 30,
            contentFactory: services => services.GetRequiredService<AuditViewModel>(),
            activatedAsync: services => services.GetRequiredService<AuditViewModel>().LoadCommand.ExecuteAsync(null)));

        context.AddResourceDictionary(
            "pack://application:,,,/ColtonStack.Client;component/Extensions/Audit/AuditPaneTemplates.xaml");
    }
}