using ColtonStack.Client.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ColtonStack.Tests;

/// <summary>
/// The sidebar pane registry is the whole extension surface for navigation: registration,
/// duplicate detection, deterministic ordering, lazy content. If these invariants hold,
/// extensions can add panes without touching the shell — the compile-checked replacement
/// for the old pane enum.
/// </summary>
public sealed class SidebarPaneRegistryTests
{
    private static SidebarPaneDefinition MakePane(string id, int order) => new(
        id,
        title: $"Pane {id}",
        iconGlyph: "\uE80F",
        order,
        contentFactory: _ => new object(),
        activatedAsync: null);

    [Fact]
    public void Register_AcceptsDistinctIds_AndOrdersByExplicitOrder()
    {
        var registry = new SidebarPaneRegistry();
        registry.Register(MakePane("people", order: 20));
        registry.Register(MakePane("channels", order: 10));
        registry.Register(MakePane("audit", order: 30));

        var panes = registry.Panes;

        Assert.Equal(["channels", "people", "audit"], panes.Select(pane => pane.Id).ToArray());
    }

    [Fact]
    public void Register_WithDuplicateId_ThrowsImmediately()
    {
        var registry = new SidebarPaneRegistry();
        registry.Register(MakePane("channels", order: 10));

        // Fail at startup — not at the first click on a doubled rail tile.
        var duplicate = Assert.Throws<InvalidOperationException>(() => registry.Register(MakePane("channels", order: 99)));
        Assert.Contains("channels", duplicate.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Content_IsLazy_AndBuiltThroughTheAttachedProvider()
    {
        var registry = new SidebarPaneRegistry();
        var buildCount = 0;
        var pane = new SidebarPaneDefinition(
            "lazy",
            "Lazy pane",
            "\uE80F",
            order: 10,
            contentFactory: _ => { buildCount++; return new object(); });
        registry.Register(pane);

        // No provider attached yet: touching content now would hide a composition bug.
        Assert.Throws<InvalidOperationException>(() => pane.Content);
        Assert.Equal(0, buildCount);

        using var provider = new ServiceCollection().BuildServiceProvider();
        registry.Attach(provider);

        var first = pane.Content;
        var second = pane.Content;

        Assert.Same(first, second); // built once, then cached
        Assert.Equal(1, buildCount);
    }

    [Fact]
    public void ActivateAsync_WithNoHook_CompletesWithoutTouchingTheProvider()
    {
        var pane = new SidebarPaneDefinition("plain", "Plain pane", "\uE80F", 10, _ => new object());

        Assert.Equal(Task.CompletedTask, pane.ActivateAsync());
    }

    [Fact]
    public async Task ActivateAsync_RunsTheExtensionHook_WithTheAttachedProvider()
    {
        var registry = new SidebarPaneRegistry();
        var hookRuns = 0;
        var pane = new SidebarPaneDefinition(
            "hooked",
            "Hooked pane",
            "\uE80F",
            order: 10,
            contentFactory: _ => new object(),
            activatedAsync: _ => { hookRuns++; return Task.CompletedTask; });
        registry.Register(pane);

        using var provider = new ServiceCollection().BuildServiceProvider();
        registry.Attach(provider);

        await pane.ActivateAsync();

        Assert.Equal(1, hookRuns);
    }
}