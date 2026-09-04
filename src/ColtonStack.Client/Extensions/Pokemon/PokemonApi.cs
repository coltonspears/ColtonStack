using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using ColtonStack.Contracts;

namespace ColtonStack.Client.Extensions.Pokemon;

/// <summary>
/// Typed HttpClient registered through <c>AddColtonStackHttpClient</c>, so it gets the same base
/// address and resilience pipeline as the core API client without repeating either.
/// </summary>
public sealed class PokemonApi(HttpClient httpClient) : IPokemonApi
{
    public async Task<IReadOnlyList<PokemonSummaryDto>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var matches = await httpClient
            .GetFromJsonAsync($"api/pokemon/search?q={Uri.EscapeDataString(query)}&limit=8", ColtonStackJsonContext.Default.IReadOnlyListPokemonSummaryDto, cancellationToken)
            .ConfigureAwait(false);
        return matches ?? [];
    }

    public async Task<MessageDto> ShareAsync(long channelId, string nameOrId, CancellationToken cancellationToken)
    {
        using var response = await httpClient
            .PostAsync(new Uri($"api/channels/{channelId}/pokemon/{Uri.EscapeDataString(nameOrId.Trim())}", UriKind.Relative), content: null, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException($"No Pokémon named '{nameOrId}'.");
        }

        response.EnsureSuccessStatusCode();
        var message = await response.Content.ReadFromJsonAsync(ColtonStackJsonContext.Default.MessageDto, cancellationToken).ConfigureAwait(false);
        return message ?? throw new InvalidOperationException("The server returned an empty message.");
    }
}
