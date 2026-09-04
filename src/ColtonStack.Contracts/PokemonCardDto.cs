namespace ColtonStack.Contracts;

/// <summary>
/// The Pokémon extension's attachment payload — everything the card renders, already flattened
/// from two PokeAPI resources by the server so the client never talks to PokeAPI. Lives in
/// Contracts for the demo; in the full product each extension ships its own contracts assembly.
/// </summary>
public sealed record PokemonCardDto(
    int Id,
    string Name,
    string Genus,
    string FlavorText,
    string ArtworkUrl,
    string ShinyArtworkUrl,
    string SpriteUrl,
    string ShinySpriteUrl,
    IReadOnlyList<string> Types,
    IReadOnlyList<string> Abilities,
    IReadOnlyList<string> Moves,
    IReadOnlyList<PokemonStatDto> Stats,
    double HeightMeters,
    double WeightKilograms)
{
    /// <summary>The attachment <c>Kind</c> the client registers a renderer for.</summary>
    public const string AttachmentKind = "pokemon";
}
