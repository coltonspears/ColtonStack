namespace ColtonStack.Server.Extensions.Pokemon;

/// <summary>Bound from <c>ColtonStack:Pokemon</c>. Extension-owned configuration.</summary>
public sealed class PokemonOptions
{
    public const string SectionName = "ColtonStack:Pokemon";

    /// <summary>PokeAPI root; trailing slash matters for relative requests.</summary>
    public string BaseUrl { get; set; } = "https://pokeapi.co/api/v2/";

    /// <summary>How many names the autocomplete index pulls (PokeAPI has ~1300 species; forms push the total higher).</summary>
    public int NameIndexLimit { get; set; } = 1400;

    /// <summary>Cached cards older than this are refreshed from PokeAPI on next request.</summary>
    public int CacheDays { get; set; } = 30;
}
