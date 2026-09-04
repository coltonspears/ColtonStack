using System.Globalization;
using ColtonStack.Contracts;

namespace ColtonStack.Server.Extensions.Pokemon;

/// <summary>
/// Pure function from two PokeAPI resources to the card the client renders. No I/O, no state —
/// the unit tests feed it fixtures and assert the flattening rules.
/// </summary>
public static class PokemonCardMapper
{
    private const int MaxMoves = 8;

    public static PokemonCardDto Map(PokeApiModels.Pokemon pokemon, PokeApiModels.Species? species)
    {
        var artwork = pokemon.Sprites?.Other?.OfficialArtwork;
        var flavor = species?.FlavorTextEntries
            .Where(entry => string.Equals(entry.Language.Name, "en", StringComparison.Ordinal))
            .Select(entry => entry.FlavorText)
            .LastOrDefault() ?? string.Empty;
        var genus = species?.Genera
            .FirstOrDefault(entry => string.Equals(entry.Language.Name, "en", StringComparison.Ordinal))
            ?.Genus ?? string.Empty;

        return new PokemonCardDto(
            Id: pokemon.Id,
            Name: DisplayName(pokemon.Name),
            Genus: genus,
            FlavorText: CleanFlavorText(flavor),
            ArtworkUrl: artwork?.FrontDefault ?? pokemon.Sprites?.FrontDefault ?? string.Empty,
            ShinyArtworkUrl: artwork?.FrontShiny ?? pokemon.Sprites?.FrontShiny ?? string.Empty,
            SpriteUrl: pokemon.Sprites?.FrontDefault ?? string.Empty,
            ShinySpriteUrl: pokemon.Sprites?.FrontShiny ?? string.Empty,
            Types: [.. pokemon.Types.OrderBy(slot => slot.Slot).Select(slot => DisplayName(slot.Type.Name))],
            Abilities: [.. pokemon.Abilities.Select(slot => slot.IsHidden ? $"{DisplayName(slot.Ability.Name)} (hidden)" : DisplayName(slot.Ability.Name))],
            Moves: [.. pokemon.Moves.Select(slot => DisplayName(slot.Move.Name)).Order(StringComparer.OrdinalIgnoreCase).Take(MaxMoves)],
            Stats: [.. pokemon.Stats.Select(stat => new PokemonStatDto(StatLabel(stat.Stat.Name), stat.BaseStat))],
            HeightMeters: pokemon.Height / 10.0,
            WeightKilograms: pokemon.Weight / 10.0);
    }

    /// <summary>"mr-mime" → "Mr Mime", "charizard" → "Charizard".</summary>
    public static string DisplayName(string apiName)
    {
        var words = apiName.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', words.Select(word => char.ToUpper(word[0], CultureInfo.InvariantCulture) + word[1..]));
    }

    /// <summary>PokeAPI flavor text carries Game Boy line breaks (\n, \f) and soft hyphens.</summary>
    public static string CleanFlavorText(string text) =>
        string.Join(' ', text
            .Replace('\f', ' ')
            .Replace('\n', ' ')
            .Replace("\u00AD", string.Empty, StringComparison.Ordinal)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static string StatLabel(string apiName) => apiName switch
    {
        "hp" => "HP",
        "attack" => "Attack",
        "defense" => "Defense",
        "special-attack" => "Sp. Atk",
        "special-defense" => "Sp. Def",
        "speed" => "Speed",
        _ => DisplayName(apiName),
    };
}
