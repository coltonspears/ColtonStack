namespace ColtonStack.Client.Extensions.Commands;

/// <summary>
/// One thing a user can ask the app to do, contributed by an extension. Surfaces in two places
/// at once: the title-bar command palette (Ctrl+K) and, when <see cref="SlashName"/> is set, the
/// composer's <c>/command</c> autocomplete. Like <see cref="SidebarPaneDefinition"/>, it is a
/// plain sealed class with delegates — no base class to inherit, no attribute to scan for.
/// </summary>
public sealed class CommandDefinition(
    string id,
    string title,
    string description,
    string iconGlyph,
    string category,
    Func<IServiceProvider, CommandInvocation, Task> executeAsync,
    IReadOnlyList<string>? keywords = null,
    string? slashName = null,
    string? argumentHint = null,
    Func<IServiceProvider, string, CancellationToken, Task<IReadOnlyList<CommandSuggestion>>>? suggestAsync = null)
{
    private IServiceProvider? _services;

    /// <summary>Stable identifier; duplicate registration throws at startup.</summary>
    public string Id { get; } = id;

    public string Title { get; } = title;

    public string Description { get; } = description;

    /// <summary>Segoe MDL2 Assets glyph shown next to the command.</summary>
    public string IconGlyph { get; } = iconGlyph;

    /// <summary>Palette grouping label ("Navigate", "Workspace", "Pokémon", ...).</summary>
    public string Category { get; } = category;

    /// <summary>Extra words the palette matches on besides the title.</summary>
    public IReadOnlyList<string> Keywords { get; } = keywords ?? [];

    /// <summary>When set, the command is also reachable as <c>/{SlashName} argument</c> from the composer.</summary>
    public string? SlashName { get; } = slashName;

    /// <summary>Placeholder shown while the user types the slash command's argument.</summary>
    public string? ArgumentHint { get; } = argumentHint;

    /// <summary>True when the slash command offers argument autocomplete.</summary>
    public bool HasSuggestions => suggestAsync is not null;

    internal void Attach(IServiceProvider services) => _services = services;

    public Task ExecuteAsync(CommandInvocation invocation) =>
        executeAsync(Services, invocation);

    public Task<IReadOnlyList<CommandSuggestion>> SuggestAsync(string argument, CancellationToken cancellationToken) =>
        suggestAsync is null
            ? Task.FromResult<IReadOnlyList<CommandSuggestion>>([])
            : suggestAsync(Services, argument, cancellationToken);

    private IServiceProvider Services =>
        _services ?? throw new InvalidOperationException($"Command '{Id}' was invoked before the host finished starting.");
}
