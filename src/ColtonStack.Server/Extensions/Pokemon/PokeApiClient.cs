using System.Net;
using System.Net.Http.Json;

namespace ColtonStack.Server.Extensions.Pokemon;

/// <summary>
/// Typed HttpClient for PokeAPI. Its <see cref="HttpClient"/> carries the base address and the
/// standard resilience handler (retry, circuit breaker, timeouts) from the extension's
/// registration; this class only knows the three resources it needs. 404 becomes null.
/// </summary>
public sealed class PokeApiClient(HttpClient httpClient)
{
    public async Task<PokeApiModels.ListResponse?> GetIndexAsync(int limit, CancellationToken cancellationToken) =>
        await httpClient
            .GetFromJsonAsync($"pokemon?limit={limit}&offset=0", PokeApiJsonContext.Default.ListResponse, cancellationToken)
            .ConfigureAwait(false);

    public async Task<PokeApiModels.Pokemon?> GetPokemonAsync(string nameOrId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(new Uri($"pokemon/{Uri.EscapeDataString(nameOrId)}", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(PokeApiJsonContext.Default.Pokemon, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PokeApiModels.Species?> GetSpeciesAsync(int id, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(new Uri($"pokemon-species/{id}", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(PokeApiJsonContext.Default.Species, cancellationToken).ConfigureAwait(false);
    }
}
