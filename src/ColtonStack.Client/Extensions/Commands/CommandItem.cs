namespace ColtonStack.Client.Extensions.Commands;

/// <summary>
/// A ready-to-run row in the command palette. Static commands are projected into these; dynamic
/// sources (jump to a channel, open a pane) produce them per query.
/// </summary>
public sealed record CommandItem(
    string Title,
    string Detail,
    string IconGlyph,
    string Category,
    Func<CancellationToken, Task> ExecuteAsync);
