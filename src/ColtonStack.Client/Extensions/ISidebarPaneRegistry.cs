namespace ColtonStack.Client.Extensions;

/// <summary>The navigation-node surface extensions use to contribute sidebar panes.</summary>
public interface ISidebarPaneRegistry
{
    /// <summary>Registers a pane. Duplicate ids fail fast — at startup, not at first click.</summary>
    void Register(SidebarPaneDefinition pane);
}