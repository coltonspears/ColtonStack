using ColtonStack.Client.Extensions.Commands;
using ColtonStack.Client.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ColtonStack.Tests;

/// <summary>
/// The composer's slash-command helper, tested against a real <see cref="CommandRegistry"/>
/// with a zero debounce. Covers the two phases — name completion, then argument autocomplete —
/// and the resolve step the send path relies on.
/// </summary>
public sealed class SlashCommandInputTests : IDisposable
{
    private readonly CommandRegistry _registry = new();
    private readonly ServiceProvider _provider = new ServiceCollection().BuildServiceProvider();
    private readonly SlashCommandInput _slash;

    public SlashCommandInputTests()
    {
        _registry.Register(new CommandDefinition(
            "chat.shrug", "Shrug", "Append a shrug", "\uE76E", "Chat",
            executeAsync: (_, _) => Task.CompletedTask,
            slashName: "shrug"));

        _registry.Register(new CommandDefinition(
            "pokemon.share", "Share a Pokémon", "Post a card", "\uE7C1", "Pokémon",
            executeAsync: (_, _) => Task.CompletedTask,
            slashName: "pokemon",
            argumentHint: "name",
            suggestAsync: (_, prefix, _) => Task.FromResult<IReadOnlyList<CommandSuggestion>>(
                new[] { "pikachu", "pidgey", "charizard" }
                    .Where(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .Select(name => new CommandSuggestion(name, name))
                    .ToArray())));

        _registry.Attach(_provider);
        _slash = new SlashCommandInput(_registry, TimeSpan.Zero);
    }

    public void Dispose()
    {
        _slash.Dispose();
        _provider.Dispose();
    }

    [Fact]
    public async Task PlainText_IsInactive()
    {
        await _slash.UpdateAsync("hello /world");

        Assert.False(_slash.IsActive);
        Assert.Empty(_slash.Suggestions);
        Assert.False(_slash.TryResolve(out _, out _));
    }

    [Fact]
    public async Task Slash_Alone_ListsEveryCommand()
    {
        await _slash.UpdateAsync("/");

        Assert.True(_slash.IsActive);
        Assert.Equal(["/pokemon", "/shrug"], _slash.Suggestions.Select(s => s.Label).Order(StringComparer.Ordinal));
        Assert.All(_slash.Suggestions, s => Assert.False(s.IsArgument));
        Assert.Null(_slash.Command);
    }

    [Fact]
    public async Task PartialName_FiltersByPrefix()
    {
        await _slash.UpdateAsync("/po");

        var only = Assert.Single(_slash.Suggestions);
        Assert.Equal("/pokemon", only.Label);
        Assert.Equal("/pokemon ", only.CompletedDraft); // trailing space moves the user to the argument
    }

    [Fact]
    public async Task CompleteName_ResolvesCommand_AndAsksItForArgumentSuggestions()
    {
        await _slash.UpdateAsync("/pokemon pi");

        Assert.NotNull(_slash.Command);
        Assert.Equal("pokemon", _slash.Command.SlashName);
        Assert.Equal("pi", _slash.Argument);
        Assert.Equal(["pikachu", "pidgey"], _slash.Suggestions.Select(s => s.Label));
        Assert.All(_slash.Suggestions, s => Assert.True(s.IsArgument));
        Assert.Equal("/pokemon pikachu", _slash.Suggestions[0].CompletedDraft);
        Assert.True(_slash.TryResolve(out var command, out var argument));
        Assert.Same(_slash.Command, command);
        Assert.Equal("pi", argument);
    }

    [Fact]
    public async Task CompleteName_WithoutSuggestions_ResolvesWithEmptyPopup()
    {
        await _slash.UpdateAsync("/shrug hi there");

        Assert.True(_slash.TryResolve(out var command, out var argument));
        Assert.Equal("chat.shrug", command.Id);
        Assert.Equal("hi there", argument);
        Assert.Empty(_slash.Suggestions);
        Assert.False(_slash.HasSuggestions);
    }

    [Fact]
    public async Task UnknownName_IsActiveButUnresolved()
    {
        await _slash.UpdateAsync("/dance now");

        Assert.True(_slash.IsActive);
        Assert.Null(_slash.Command);
        Assert.False(_slash.TryResolve(out _, out _));
    }

    [Fact]
    public async Task MoveSelection_WrapsAround()
    {
        await _slash.UpdateAsync("/pokemon p");
        Assert.Equal("pikachu", _slash.Selected?.Label);

        _slash.MoveSelection(+1);
        Assert.Equal("pidgey", _slash.Selected?.Label);

        _slash.MoveSelection(+1);
        Assert.Equal("pikachu", _slash.Selected?.Label); // wrapped

        _slash.MoveSelection(-1);
        Assert.Equal("pidgey", _slash.Selected?.Label);
    }

    [Fact]
    public async Task Reset_ClearsEverything()
    {
        await _slash.UpdateAsync("/pokemon pi");

        _slash.Reset();

        Assert.False(_slash.IsActive);
        Assert.Null(_slash.Command);
        Assert.Equal(string.Empty, _slash.Argument);
        Assert.Empty(_slash.Suggestions);
    }

    [Fact]
    public async Task StatusText_ShowsHintUntilArgumentTyped()
    {
        await _slash.UpdateAsync("/pokemon ");
        Assert.Equal("/pokemon name", _slash.StatusText);

        await _slash.UpdateAsync("/pokemon pi");
        Assert.Contains("Post a card", _slash.StatusText, StringComparison.Ordinal);
    }
}
