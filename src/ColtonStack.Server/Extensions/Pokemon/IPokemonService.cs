using ColtonStack.Contracts;

namespace ColtonStack.Server.Extensions.Pokemon;

/// <summary>The extension's application service: name autocomplete and cached card lookups.</summary>
public interface IPokemonService
{
    /// <summary>Names matching <paramref name="query"/> (prefix matches first), from the in-memory index.</summary>
    Task<IReadOnlyList<PokemonSummaryDto>> SearchAsync(string query, int limit, CancellationToken cancellationToken);

    /// <summary>The card for a name or id: from SQLite when fresh, otherwise fetched from PokeAPI and cached. Null when unknown.</summary>
    Task<PokemonCardDto?> GetCardAsync(string nameOrId, CancellationToken cancellationToken);
}
