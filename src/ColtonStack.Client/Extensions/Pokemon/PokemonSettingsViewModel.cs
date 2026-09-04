using ColtonStack.Client.Messages;
using ColtonStack.Client.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace ColtonStack.Client.Extensions.Pokemon;

/// <summary>
/// The Pokémon extension's settings section: artwork style and shiny default, persisted on the
/// server under keys this extension owns. Also listens for <see cref="SettingChangedMessage"/>
/// so a value saved elsewhere (another client) is reflected here.
/// </summary>
public sealed partial class PokemonSettingsViewModel : ObservableObject, IRecipient<SettingChangedMessage>
{
    public const string ArtworkKey = "pokemon.artwork";
    public const string PreferShinyKey = "pokemon.shiny";
    public const string OfficialArtwork = "official";
    public const string SpriteArtwork = "sprite";

    private readonly ISettingsStore _store;
    private bool _syncing;

    public PokemonSettingsViewModel(ISettingsStore store, IMessenger messenger)
    {
        _store = store;
        Sync();
        messenger.Register(this);
    }

    public IReadOnlyList<ArtworkChoice> ArtworkChoices { get; } =
    [
        new(OfficialArtwork, "Official artwork", "Large illustrated art from PokeAPI"),
        new(SpriteArtwork, "Pixel sprite", "The classic in-game front sprite"),
    ];

    [ObservableProperty]
    public partial ArtworkChoice? Artwork { get; set; }

    [ObservableProperty]
    public partial bool PreferShiny { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    partial void OnArtworkChanged(ArtworkChoice? value)
    {
        if (value is not null && !_syncing)
        {
            _ = SaveAsync(ArtworkKey, value.Key);
        }
    }

    partial void OnPreferShinyChanged(bool value)
    {
        if (!_syncing)
        {
            _ = SaveAsync(PreferShinyKey, value ? "true" : "false");
        }
    }

    public void Receive(SettingChangedMessage message)
    {
        if (message.Key.StartsWith("pokemon.", StringComparison.OrdinalIgnoreCase))
        {
            Sync();
        }
    }

    private void Sync()
    {
        _syncing = true;
        var artworkKey = _store.GetString(ArtworkKey, OfficialArtwork);
        Artwork = ArtworkChoices.FirstOrDefault(choice => string.Equals(choice.Key, artworkKey, StringComparison.OrdinalIgnoreCase)) ?? ArtworkChoices[0];
        PreferShiny = _store.GetBool(PreferShinyKey, fallback: false);
        _syncing = false;
    }

    private async Task SaveAsync(string key, string value)
    {
        try
        {
            await _store.SetAsync(key, value, CancellationToken.None);
            StatusText = "Saved — new cards use this; existing ones keep their look.";
        }
        catch (Exception ex)
        {
            StatusText = $"Could not save: {ex.Message}";
        }
    }
}