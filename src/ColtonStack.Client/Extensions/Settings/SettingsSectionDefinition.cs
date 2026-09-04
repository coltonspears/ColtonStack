namespace ColtonStack.Client.Extensions.Settings;

/// <summary>
/// One page in the in-window Settings view, contributed by an extension. Same shape as a sidebar
/// pane: id, title, glyph, order and a lazily-built view model rendered through an implicit
/// DataTemplate the extension ships. The Settings shell has no list of sections of its own.
/// </summary>
public sealed class SettingsSectionDefinition(
    string id,
    string title,
    string description,
    string iconGlyph,
    int order,
    Func<IServiceProvider, object> contentFactory)
{
    private readonly LazyContent _content = new($"Settings section '{id}'", contentFactory);

    public string Id { get; } = id;

    public string Title { get; } = title;

    /// <summary>One line under the title in the section list.</summary>
    public string Description { get; } = description;

    public string IconGlyph { get; } = iconGlyph;

    public int Order { get; } = order;

    internal void Attach(IServiceProvider services) => _content.Attach(services);

    /// <summary>The section's view model. Created on first visit, then cached.</summary>
    public object Content => _content.Value;
}
