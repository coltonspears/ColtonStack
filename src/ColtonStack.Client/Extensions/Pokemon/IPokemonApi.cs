using ColtonStack.Contracts;

namespace ColtonStack.Client.Extensions.Pokemon;

/// <summary>The extension's slice of the server API. Its own interface, so the core client never grows Pokémon methods.</summary>
public interface IPokemonApi
{
    Task<IReadOnlyList<PokemonSummaryDto>> SearchAsync(string query, CancellationToken cancellationToken);

    /// <summary>Asks the server to look up the card and post it into the channel as an attachment.</summary>
    Task<MessageDto> ShareAsync(long channelId, string nameOrId, CancellationToken cancellationToken);
}
