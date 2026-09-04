namespace ColtonStack.Client.Extensions;

/// <summary>
/// A sidebar navigation node contributed by an extension — identified by string id, ordered
/// by explicit number, carrying its own title, icon glyph, content factory and optional
/// activation hook. Nothing in the core app has an enum of panes; adding a pane means
/// calling <see cref="ISidebarPaneRegistry.Register"/>, not editing shared source.
///
/// The content object is created lazily on first activation (see <see cref="LazyContent"/>), so
/// an extension's view models are only constructed when its pane is actually visited.
/// </summary>
public sealed class SidebarPaneDefinition(
    string id,
    string title,
    string iconGlyph,
    int order,
    Func<IServiceProvider, object> contentFactory,
    Func<IServiceProvider, Task>? activatedAsync = null)
{
    private readonly LazyContent _content = new($"Pane '{id}'", contentFactory);

    /// <summary>Stable identifier — the anchor for selection state and templates. Duplicate registration throws.</summary>
    public string Id { get; } = id;

    /// <summary>Rail tooltip / accessible name.</summary>
    public string Title { get; } = title;

    /// <summary>Segoe MDL2 Assets glyph shown on the rail tile.</summary>
    public string IconGlyph { get; } = iconGlyph;

    /// <summary>Sort position among panes. Extensions can slot before or after core panes.</summary>
    public int Order { get; } = order;

    /// <summary>Called by the registry once the DI container exists; enables lazy content creation.</summary>
    internal void Attach(IServiceProvider services) => _content.Attach(services);

    /// <summary>The pane's root view model (its DataContext). Created on first access, then cached.</summary>
    public object Content => _content.Value;

    /// <summary>Runs when the pane becomes active — the extension's lazy-load hook.</summary>
    public Task ActivateAsync() => _content.RunAsync(activatedAsync);
}
