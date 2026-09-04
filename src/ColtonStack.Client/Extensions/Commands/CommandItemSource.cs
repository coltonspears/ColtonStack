namespace ColtonStack.Client.Extensions.Commands;

/// <summary>
/// A palette contributor whose rows depend on live state — "Go to #general", "Open pane: Audit".
/// The delegate gets the DI provider so an extension can read its own view models; the palette
/// itself only ever sees the resulting <see cref="CommandItem"/> rows.
/// </summary>
public sealed class CommandItemSource(
    string id,
    Func<IServiceProvider, string, CancellationToken, Task<IReadOnlyList<CommandItem>>> getItemsAsync)
{
    private IServiceProvider? _services;

    public string Id { get; } = id;

    internal void Attach(IServiceProvider services) => _services = services;

    public Task<IReadOnlyList<CommandItem>> GetItemsAsync(string query, CancellationToken cancellationToken) =>
        _services is { } services
            ? getItemsAsync(services, query, cancellationToken)
            : Task.FromResult<IReadOnlyList<CommandItem>>([]);
}
