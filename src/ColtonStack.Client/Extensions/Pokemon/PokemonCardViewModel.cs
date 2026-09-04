using ColtonStack.Client.Services;
using ColtonStack.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ColtonStack.Client.Extensions.Pokemon;

/// <summary>
/// Presentation of one Pokémon card attachment. The DTO is immutable; the only live state is the
/// shiny toggle, so that is the only observable property. Artwork style and the default for
/// shiny come from the extension's own settings keys.
/// </summary>
public sealed partial class PokemonCardViewModel : ObservableObject
{
    private readonly PokemonCardDto _card;
    private readonly bool _useSprite;

    public PokemonCardViewModel(PokemonCardDto card, ISettingsStore settings)
    {
        _card = card;
        _useSprite = string.Equals(settings.GetString(PokemonSettingsViewModel.ArtworkKey, PokemonSettingsViewModel.OfficialArtwork), PokemonSettingsViewModel.SpriteArtwork, StringComparison.OrdinalIgnoreCase);
        IsShiny = settings.GetBool(PokemonSettingsViewModel.PreferShinyKey, fallback: false);
        Stats = [.. card.Stats.Select(stat => new PokemonStatViewModel(stat.Name, stat.Value))];
    }

    public int Id => _card.Id;

    public string Name => _card.Name;

    public string Number => $"#{_card.Id:000}";

    public string Genus => _card.Genus;

    public string FlavorText => _card.FlavorText;

    public IReadOnlyList<string> Types => _card.Types;

    public string PrimaryType => _card.Types.Count > 0 ? _card.Types[0] : "Normal";

    public IReadOnlyList<string> Abilities => _card.Abilities;

    public string AbilitiesText => string.Join(", ", _card.Abilities);

    public IReadOnlyList<string> Moves => _card.Moves;

    public string MovesText => string.Join("  ·  ", _card.Moves);

    public IReadOnlyList<PokemonStatViewModel> Stats { get; }

    public int TotalStats => _card.Stats.Sum(stat => stat.Value);

    public string SizeText => $"{_card.HeightMeters:0.0} m  ·  {_card.WeightKilograms:0.0} kg";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImageUrl))]
    public partial bool IsShiny { get; set; }

    public string ImageUrl => (_useSprite, IsShiny) switch
    {
        (true, true) => FirstNonEmpty(_card.ShinySpriteUrl, _card.SpriteUrl, _card.ArtworkUrl),
        (true, false) => FirstNonEmpty(_card.SpriteUrl, _card.ArtworkUrl),
        (false, true) => FirstNonEmpty(_card.ShinyArtworkUrl, _card.ArtworkUrl, _card.SpriteUrl),
        (false, false) => FirstNonEmpty(_card.ArtworkUrl, _card.SpriteUrl),
    };

    [RelayCommand]
    private void ToggleShiny() => IsShiny = !IsShiny;

    private static string FirstNonEmpty(params string[] candidates) =>
        candidates.FirstOrDefault(candidate => candidate.Length > 0) ?? string.Empty;
}