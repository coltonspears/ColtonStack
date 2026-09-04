using System.Text.Json.Serialization;

namespace ColtonStack.Server.Extensions.Pokemon;

/// <summary>
/// The slice of PokeAPI's shape this extension reads, nested in one static class so the file
/// reads as the API's schema. Positional records + a source-generated JSON context: the
/// third-party payload is parsed with the same zero-reflection pipeline as our own DTOs, and
/// only the fields we use exist here.
/// </summary>
public static class PokeApiModels
{
    public sealed record NamedResource(string Name, string Url);

    public sealed record ListResponse(int Count, IReadOnlyList<NamedResource> Results);

    public sealed record TypeSlot(int Slot, NamedResource Type);

    public sealed record AbilitySlot(NamedResource Ability, bool IsHidden);

    public sealed record MoveSlot(NamedResource Move);

    public sealed record StatSlot(int BaseStat, NamedResource Stat);

    public sealed record Artwork(string? FrontDefault, string? FrontShiny);

    public sealed record OtherSprites([property: JsonPropertyName("official-artwork")] Artwork? OfficialArtwork);

    public sealed record Sprites(string? FrontDefault, string? FrontShiny, OtherSprites? Other);

    public sealed record Pokemon(
        int Id,
        string Name,
        int Height,
        int Weight,
        IReadOnlyList<TypeSlot> Types,
        IReadOnlyList<AbilitySlot> Abilities,
        IReadOnlyList<MoveSlot> Moves,
        IReadOnlyList<StatSlot> Stats,
        Sprites? Sprites);

    public sealed record FlavorTextEntry(string FlavorText, NamedResource Language, NamedResource Version);

    public sealed record GenusEntry(string Genus, NamedResource Language);

    public sealed record Species(IReadOnlyList<FlavorTextEntry> FlavorTextEntries, IReadOnlyList<GenusEntry> Genera);
}
