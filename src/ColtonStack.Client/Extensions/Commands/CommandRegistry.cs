namespace ColtonStack.Client.Extensions.Commands;

/// <summary>Concrete registry the composition root fills from the extension list, then attaches to the DI provider once it exists.</summary>
public sealed class CommandRegistry : ICommandRegistry
{
    private readonly List<CommandDefinition> _commands = [];
    private readonly List<CommandItemSource> _sources = [];
    private IServiceProvider? _services;

    public IReadOnlyList<CommandDefinition> Commands => _commands;

    public IReadOnlyList<CommandItemSource> Sources => _sources;

    public void Register(CommandDefinition command)
    {
        if (_commands.Any(existing => string.Equals(existing.Id, command.Id, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"A command with id '{command.Id}' is already registered.");
        }

        if (command.SlashName is { } slash && FindSlash(slash) is not null)
        {
            throw new InvalidOperationException($"A command with slash name '/{slash}' is already registered.");
        }

        _commands.Add(command);
        if (_services is { } services)
        {
            command.Attach(services);
        }
    }

    public void AddSource(CommandItemSource source)
    {
        if (_sources.Any(existing => string.Equals(existing.Id, source.Id, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"A command source with id '{source.Id}' is already registered.");
        }

        _sources.Add(source);
        if (_services is { } services)
        {
            source.Attach(services);
        }
    }

    public CommandDefinition? FindSlash(string slashName) =>
        _commands.Find(command => string.Equals(command.SlashName, slashName, StringComparison.OrdinalIgnoreCase));

    /// <summary>Called once by the composition root after the host is built.</summary>
    public void Attach(IServiceProvider services)
    {
        _services = services;
        foreach (var command in _commands)
        {
            command.Attach(services);
        }

        foreach (var source in _sources)
        {
            source.Attach(services);
        }
    }
}
