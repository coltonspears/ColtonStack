using Dapper.Contrib.Extensions;

namespace ColtonStack.Server.Extensions.Pokemon;

/// <summary>
/// The extension's own table: a card cache keyed by PokeAPI id. The card is stored as the same
/// JSON the client receives, so a cache hit is a single row read and no re-mapping.
/// </summary>
[Table("PokemonCards")]
public sealed class PokemonCardRow
{
    [ExplicitKey]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string CardJson { get; set; } = string.Empty;

    public DateTimeOffset FetchedAtUtc { get; set; }
}
