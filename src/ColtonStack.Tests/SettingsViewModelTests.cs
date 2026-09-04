using ColtonStack.Client.Extensions.Settings;
using ColtonStack.Client.Messages;
using ColtonStack.Client.Services;
using ColtonStack.Client.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace ColtonStack.Tests;

/// <summary>
/// The Settings page knows no setting. It shows whatever sections the registry holds, in
/// registry order, and lazily builds each section's view model through DI on first visit.
/// </summary>
public sealed class SettingsViewModelTests : IDisposable
{
    private readonly SettingsRegistry _registry = new();
    private readonly ISettingsStore _store = Substitute.For<ISettingsStore>();
    private readonly ServiceProvider _provider = new ServiceCollection().BuildServiceProvider();
    private int _pokemonBuilds;

    public SettingsViewModelTests()
    {
        _registry.Register(new SettingsSectionDefinition("pokemon", "Pokémon", "Cards", "\uE7C1", order: 50, _ => { _pokemonBuilds++; return new object(); }));
        _registry.Register(new SettingsSectionDefinition("profile", "Profile", "You", "\uE77B", order: 10, _ => new object()));
        _registry.Register(new SettingsSectionDefinition("audit", "Audit", "Trail", "\uE9D9", order: 50, _ => new object()));
        _registry.Attach(_provider);
    }

    public void Dispose() => _provider.Dispose();

    [Fact]
    public void Sections_ComeFromTheRegistry_OrderedByOrderThenTitle()
    {
        var vm = new SettingsViewModel(_registry, _store);

        Assert.Equal(["profile", "audit", "pokemon"], vm.Sections.Select(s => s.Id));
        Assert.Null(vm.ActiveSection);
        Assert.Null(vm.ActiveContent);
    }

    [Fact]
    public void Receive_WithSectionId_ActivatesThatSection_AndBuildsItsContentOnce()
    {
        var vm = new SettingsViewModel(_registry, _store);

        vm.Receive(new SettingsRequestedMessage("pokemon"));
        var first = vm.ActiveContent;
        var second = vm.ActiveContent;

        Assert.Equal("pokemon", vm.ActiveSection?.Id);
        Assert.Same(first, second);
        Assert.Equal(1, _pokemonBuilds);
    }

    [Fact]
    public void Receive_WithoutSectionId_DefaultsToFirstSection_AndKeepsCurrentIfAny()
    {
        var vm = new SettingsViewModel(_registry, _store);

        vm.Receive(new SettingsRequestedMessage(null));
        Assert.Equal("profile", vm.ActiveSection?.Id);

        vm.Receive(new SettingsRequestedMessage("audit"));
        vm.Receive(new SettingsRequestedMessage(null));
        Assert.Equal("audit", vm.ActiveSection?.Id);
    }

    [Fact]
    public void Receive_WithUnknownSectionId_FallsBackInsteadOfClearing()
    {
        var vm = new SettingsViewModel(_registry, _store);
        vm.Receive(new SettingsRequestedMessage("audit"));

        vm.Receive(new SettingsRequestedMessage("does-not-exist"));

        Assert.Equal("audit", vm.ActiveSection?.Id);
    }

    [Fact]
    public void FirstVisit_TriggersOneStoreLoad_UntilLoaded()
    {
        _store.IsLoaded.Returns(false);
        _store.LoadAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var vm = new SettingsViewModel(_registry, _store);

        vm.Receive(new SettingsRequestedMessage("profile"));
        _ = _store.Received(1).LoadAsync(Arg.Any<CancellationToken>());

        _store.IsLoaded.Returns(true);
        vm.Receive(new SettingsRequestedMessage("audit"));
        _ = _store.Received(1).LoadAsync(Arg.Any<CancellationToken>());
    }
}
