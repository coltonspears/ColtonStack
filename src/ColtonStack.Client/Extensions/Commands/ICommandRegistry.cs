namespace ColtonStack.Client.Extensions.Commands;

/// <summary>
/// The extension-facing command surface. Extensions register commands (palette + optional slash
/// form) and dynamic item sources; the palette and composer read them. Nothing in the core app
/// has a switch statement over command names.
/// </summary>
public interface ICommandRegistry
{
    /// <summary>All registered commands, in registration order.</summary>
    IReadOnlyList<CommandDefinition> Commands { get; }

    /// <summary>All registered dynamic sources.</summary>
    IReadOnlyList<CommandItemSource> Sources { get; }

    /// <summary>Adds a command; a duplicate id throws at startup rather than shadowing silently.</summary>
    void Register(CommandDefinition command);

    /// <summary>Adds a dynamic palette source.</summary>
    void AddSource(CommandItemSource source);

    /// <summary>Finds the command reachable as <c>/{slashName}</c>, if any (case-insensitive).</summary>
    CommandDefinition? FindSlash(string slashName);
}
