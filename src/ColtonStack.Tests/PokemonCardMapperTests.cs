using ColtonStack.Server.Extensions.Pokemon;
using Xunit;
using static ColtonStack.Server.Extensions.Pokemon.PokeApiModels;

namespace ColtonStack.Tests;

/// <summary>
/// The server-side flattening of two PokeAPI resources into one card. Pure function, so the
/// fixture is a handful of records — no HTTP, no cache, no database.
/// </summary>
public sealed class PokemonCardMapperTests
{
    private static NamedResource Named(string name) => new(name, $"https://pokeapi.co/api/v2/x/{name}/");

    private static Pokemon Bulbasaur() => new(
        Id: 1,
        Name: "bulbasaur",
        Height: 7,
        Weight: 69,
        Types: [new TypeSlot(2, Named("poison")), new TypeSlot(1, Named("grass"))],
        Abilities: [new AbilitySlot(Named("overgrow"), IsHidden: false), new AbilitySlot(Named("chlorophyll"), IsHidden: true)],
        Moves: [.. Enumerable.Range(0, 12).Select(i => new MoveSlot(Named($"move-{(char)('z' - i)}")))],
        Stats: [new StatSlot(45, Named("hp")), new StatSlot(49, Named("attack")), new StatSlot(65, Named("special-attack"))],
        Sprites: new Sprites(
            FrontDefault: "https://img/sprite.png",
            FrontShiny: "https://img/sprite-shiny.png",
            Other: new OtherSprites(new Artwork("https://img/art.png", "https://img/art-shiny.png"))));

    private static Species BulbasaurSpecies() => new(
        FlavorTextEntries:
        [
            new FlavorTextEntry("Une graine.", Named("fr"), Named("x")),
            new FlavorTextEntry("A strange seed was\nplanted on its back.\fIt grows.", Named("en"), Named("red")),
            new FlavorTextEntry("Bulbasaur can be seen napping in\u00ADbright sunlight.", Named("en"), Named("yellow")),
        ],
        Genera: [new GenusEntry("Pokémon Graine", Named("fr")), new GenusEntry("Seed Pokémon", Named("en"))]);

    [Fact]
    public void Map_FlattensNamesTypesAndArtwork()
    {
        var card = PokemonCardMapper.Map(Bulbasaur(), BulbasaurSpecies());

        Assert.Equal(1, card.Id);
        Assert.Equal("Bulbasaur", card.Name);
        Assert.Equal("Seed Pokémon", card.Genus);
        Assert.Equal(["Grass", "Poison"], card.Types); // ordered by slot, not by wire order
        Assert.Equal("https://img/art.png", card.ArtworkUrl);
        Assert.Equal("https://img/art-shiny.png", card.ShinyArtworkUrl);
        Assert.Equal("https://img/sprite.png", card.SpriteUrl);
        Assert.Equal(0.7, card.HeightMeters, precision: 3);
        Assert.Equal(6.9, card.WeightKilograms, precision: 3);
    }

    [Fact]
    public void Map_UsesTheLatestEnglishFlavorText_Cleaned()
    {
        var card = PokemonCardMapper.Map(Bulbasaur(), BulbasaurSpecies());

        Assert.Equal("Bulbasaur can be seen napping inbright sunlight.", card.FlavorText);
    }

    [Fact]
    public void Map_MarksHiddenAbilities_AndCapsSortedMoves()
    {
        var card = PokemonCardMapper.Map(Bulbasaur(), BulbasaurSpecies());

        Assert.Equal(["Overgrow", "Chlorophyll (hidden)"], card.Abilities);
        Assert.Equal(8, card.Moves.Count);
        Assert.Equal(card.Moves.Order(StringComparer.OrdinalIgnoreCase), card.Moves);
    }

    [Fact]
    public void Map_LabelsStats()
    {
        var card = PokemonCardMapper.Map(Bulbasaur(), BulbasaurSpecies());

        Assert.Equal(["HP", "Attack", "Sp. Atk"], card.Stats.Select(s => s.Name));
        Assert.Equal([45, 49, 65], card.Stats.Select(s => s.Value));
    }

    [Fact]
    public void Map_WithoutSpecies_LeavesTextEmpty()
    {
        var card = PokemonCardMapper.Map(Bulbasaur(), species: null);

        Assert.Equal(string.Empty, card.Genus);
        Assert.Equal(string.Empty, card.FlavorText);
    }

    [Fact]
    public void Map_WithoutOfficialArtwork_FallsBackToSprites()
    {
        var pokemon = Bulbasaur() with { Sprites = new Sprites("https://img/s.png", null, Other: null) };

        var card = PokemonCardMapper.Map(pokemon, species: null);

        Assert.Equal("https://img/s.png", card.ArtworkUrl);
        Assert.Equal(string.Empty, card.ShinyArtworkUrl);
    }

    [Theory]
    [InlineData("charizard", "Charizard")]
    [InlineData("mr-mime", "Mr Mime")]
    [InlineData("ho-oh", "Ho Oh")]
    [InlineData("special-defense", "Special Defense")]
    public void DisplayName_CapitalisesEachDashSeparatedWord(string api, string expected) =>
        Assert.Equal(expected, PokemonCardMapper.DisplayName(api));

    [Fact]
    public void CleanFlavorText_CollapsesWhitespaceAndControlCharacters() =>
        Assert.Equal("a b c", PokemonCardMapper.CleanFlavorText("a\nb\f  c"));
}
