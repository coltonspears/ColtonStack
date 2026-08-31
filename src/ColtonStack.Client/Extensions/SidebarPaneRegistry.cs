using Microsoft.Extensions.DependencyInjection;

namespace ColtonStack.Client.Extensions;

/// <summary>
/// The one mutable, explicit registry of sidebar panes. The composition root creates it,
/// extensions register into it during the Configure phase, and the shell binds to the sorted
/// snapshot. No base class, no discovery magic — a list with a duplicate guard.
/// </summary>
public sealed class SidebarPaneRegistry : ISidebarPaneRegistry
{
    private readonly List<SidebarPaneDefinition> _panes = [];
    private IServiceProvider? _services;

    public void Register(SidebarPaneDefinition pane)
    {
        if (_panes.Any(existing => string.Equals(existing.Id, pane.Id, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"A sidebar pane with id '{pane.Id}' is already registered.");
        }

        _panes.Add(pane);

        // Late registration (after Attach) still gets the provider.
        if (_services is { } services)
        {
            pane.Attach(services);
        }
    }

    /// <summary>Called once by the composition root after the host starts; all panes receive the provider.</summary>
    public void Attach(IServiceProvider services)
    {
        _services = services;
        foreach (var pane in _panes)
        {
            pane.Attach(services);
        }
    }

    /// <summary>Stable, order-sorted snapshot for binding. Registration is complete before the shell is resolved.</summary>
    public IReadOnlyList<SidebarPaneDefinition> Panes => [.. _panes.OrderBy(pane => pane.Order)];
}