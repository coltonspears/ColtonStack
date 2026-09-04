using System.Net.Http;
using ColtonStack.Client.Messages;
using ColtonStack.Client.Services;
using ColtonStack.Contracts;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace ColtonStack.Tests;

/// <summary>
/// The client settings store: an in-memory snapshot of the server's key/value table with lenient
/// typed getters and a change broadcast. Extensions read it synchronously from bindings, so the
/// fallback behaviour matters as much as the happy path.
/// </summary>
public sealed class SettingsStoreTests
{
    private readonly IColtonStackApiClient _api = Substitute.For<IColtonStackApiClient>();
    private readonly WeakReferenceMessenger _messenger = new();
    private readonly SettingsStore _store;

    public SettingsStoreTests()
    {
        _store = new SettingsStore(_api, _messenger, NullLogger<SettingsStore>.Instance);
    }

    [Fact]
    public async Task LoadAsync_PopulatesSnapshot_AndMarksLoaded()
    {
        var now = DateTimeOffset.UtcNow;
        _api.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new SettingDto("pokemon.artwork", "sprite", now),
            new SettingDto("pokemon.shiny", "true", now),
            new SettingDto("audit.pagesize", "75", now),
        ]);

        Assert.False(_store.IsLoaded);
        await _store.LoadAsync(CancellationToken.None);

        Assert.True(_store.IsLoaded);
        Assert.Equal("sprite", _store.GetString("pokemon.artwork", "official"));
        Assert.True(_store.GetBool("pokemon.shiny", fallback: false));
        Assert.Equal(75, _store.GetInt("audit.pagesize", 50));
    }

    [Fact]
    public async Task LoadAsync_WhenServerIsDown_StillMarksLoaded_AndDefaultsApply()
    {
        _api.GetSettingsAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("boom"));

        await _store.LoadAsync(CancellationToken.None);

        Assert.True(_store.IsLoaded);
        Assert.Equal("official", _store.GetString("pokemon.artwork", "official"));
    }

    [Fact]
    public async Task TypedGetters_FallBack_WhenValueDoesNotParse()
    {
        var now = DateTimeOffset.UtcNow;
        _api.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new SettingDto("pokemon.shiny", "yes please", now),
            new SettingDto("audit.pagesize", "lots", now),
        ]);
        await _store.LoadAsync(CancellationToken.None);

        Assert.False(_store.GetBool("pokemon.shiny", fallback: false));
        Assert.Equal(50, _store.GetInt("audit.pagesize", 50));
    }

    [Fact]
    public async Task Keys_AreCaseInsensitive()
    {
        _api.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns([new SettingDto("Pokemon.Artwork", "sprite", DateTimeOffset.UtcNow)]);
        await _store.LoadAsync(CancellationToken.None);

        Assert.Equal("sprite", _store.GetString("pokemon.artwork", "official"));
    }

    [Fact]
    public async Task SetAsync_PersistsThroughTheApi_UpdatesSnapshot_AndBroadcasts()
    {
        _api.PutSettingAsync("pokemon.shiny", "true", Arg.Any<CancellationToken>())
            .Returns(new SettingDto("pokemon.shiny", "true", DateTimeOffset.UtcNow));
        SettingChangedMessage? received = null;
        _messenger.Register<SettingChangedMessage>(this, (_, message) => received = message);

        await _store.SetAsync("pokemon.shiny", "true", CancellationToken.None);

        Assert.True(_store.GetBool("pokemon.shiny", fallback: false));
        Assert.NotNull(received);
        Assert.Equal("pokemon.shiny", received.Key);
        Assert.Equal("true", received.Value);
    }

    [Fact]
    public async Task SetAsync_WhenServerRejects_Throws_AndKeepsOldValue()
    {
        _api.GetSettingsAsync(Arg.Any<CancellationToken>()).Returns([new SettingDto("pokemon.shiny", "false", DateTimeOffset.UtcNow)]);
        await _store.LoadAsync(CancellationToken.None);
        _api.PutSettingAsync("pokemon.shiny", "true", Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("400"));

        await Assert.ThrowsAsync<HttpRequestException>(() => _store.SetAsync("pokemon.shiny", "true", CancellationToken.None));

        Assert.False(_store.GetBool("pokemon.shiny", fallback: true));
    }
}
