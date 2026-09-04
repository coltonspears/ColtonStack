using ColtonStack.Client.Extensions.Commands;
using ColtonStack.Client.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ColtonStack.Tests;

/// <summary>
/// The command registry is the palette's and the composer's only source of commands. Like the
/// pane registry it must reject duplicates at startup and hand the DI provider to every command,
/// including ones registered after the host started.
/// </summary>
public sealed class CommandRegistryTests
{
    private static CommandDefinition Command(string id, string? slash = null, Func<IServiceProvider, CommandInvocation, Task>? execute = null) => new(
        id, title: id, description: "d", iconGlyph: "\uE80F", category: "Test",
        executeAsync: execute ?? ((_, _) => Task.CompletedTask),
        keywords: ["kw"],
        slashName: slash);

    [Fact]
    public void Register_DuplicateId_Throws()
    {
        var registry = new CommandRegistry();
        registry.Register(Command("a"));

        var ex = Assert.Throws<InvalidOperationException>(() => registry.Register(Command("a")));
        Assert.Contains("'a'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Register_DuplicateSlashName_Throws_CaseInsensitively()
    {
        var registry = new CommandRegistry();
        registry.Register(Command("a", slash: "shrug"));

        var ex = Assert.Throws<InvalidOperationException>(() => registry.Register(Command("b", slash: "SHRUG")));
        Assert.Contains("/SHRUG", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FindSlash_IsCaseInsensitive_AndNullForUnknown()
    {
        var registry = new CommandRegistry();
        registry.Register(Command("a", slash: "pokemon"));

        Assert.NotNull(registry.FindSlash("POKEMON"));
        Assert.Null(registry.FindSlash("nope"));
    }

    [Fact]
    public async Task Execute_BeforeAttach_Throws_AfterAttach_ReceivesProvider()
    {
        var registry = new CommandRegistry();
        IServiceProvider? seen = null;
        var command = Command("a", execute: (services, _) => { seen = services; return Task.CompletedTask; });
        registry.Register(command);

        await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteAsync(new CommandInvocation(string.Empty, null, CancellationToken.None)));

        await using var provider = new ServiceCollection().BuildServiceProvider();
        registry.Attach(provider);
        await command.ExecuteAsync(new CommandInvocation("arg", 7, CancellationToken.None));

        Assert.Same(provider, seen);
    }

    [Fact]
    public async Task Register_AfterAttach_IsAttachedImmediately()
    {
        var registry = new CommandRegistry();
        await using var provider = new ServiceCollection().BuildServiceProvider();
        registry.Attach(provider);

        IServiceProvider? seen = null;
        var late = Command("late", execute: (services, _) => { seen = services; return Task.CompletedTask; });
        registry.Register(late);
        await late.ExecuteAsync(new CommandInvocation(string.Empty, null, CancellationToken.None));

        Assert.Same(provider, seen);
    }

    [Fact]
    public async Task Source_BeforeAttach_ReturnsNothing_AfterAttach_Runs()
    {
        var registry = new CommandRegistry();
        var source = new CommandItemSource("s", (_, query, _) => Task.FromResult<IReadOnlyList<CommandItem>>(
            [new CommandItem($"hit:{query}", "d", "\uE80F", "Test", _ => Task.CompletedTask)]));
        registry.AddSource(source);

        Assert.Empty(await source.GetItemsAsync("q", CancellationToken.None));

        await using var provider = new ServiceCollection().BuildServiceProvider();
        registry.Attach(provider);

        var items = await source.GetItemsAsync("q", CancellationToken.None);
        Assert.Equal("hit:q", Assert.Single(items).Title);
    }

    [Fact]
    public void AddSource_DuplicateId_Throws()
    {
        var registry = new CommandRegistry();
        registry.AddSource(new CommandItemSource("s", (_, _, _) => Task.FromResult<IReadOnlyList<CommandItem>>([])));

        Assert.Throws<InvalidOperationException>(() => registry.AddSource(new CommandItemSource("s", (_, _, _) => Task.FromResult<IReadOnlyList<CommandItem>>([]))));
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("toggle", true)]
    [InlineData("TOGGLE chaos", true)]
    [InlineData("workspace", true)]  // category
    [InlineData("retry", true)]      // keyword
    [InlineData("chaos retry", true)]
    [InlineData("pokemon", false)]
    public void PaletteMatches_EveryTokenMustHitTitleCategoryOrKeyword(string query, bool expected)
    {
        var command = new CommandDefinition(
            "workspace.chaos", "Toggle chaos mode", "d", "\uE945", "Workspace",
            executeAsync: (_, _) => Task.CompletedTask,
            keywords: ["failure", "retry"]);

        Assert.Equal(expected, CommandPaletteViewModel.Matches(command, query));
    }
}
