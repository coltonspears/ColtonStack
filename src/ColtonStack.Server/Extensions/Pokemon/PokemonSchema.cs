using ColtonStack.Server.Infrastructure;

namespace ColtonStack.Server.Extensions.Pokemon;

/// <summary>DDL for <see cref="PokemonCardRow"/> — registered by the extension, executed by the core initializer.</summary>
public sealed class PokemonSchema : ISchemaContributor
{
    public string Name => "Pokémon card cache";

    public string Schema => """
        CREATE TABLE IF NOT EXISTS PokemonCards (
            Id           INTEGER PRIMARY KEY,
            Name         TEXT NOT NULL UNIQUE,
            CardJson     TEXT NOT NULL,
            FetchedAtUtc TEXT NOT NULL);
        """;
}
