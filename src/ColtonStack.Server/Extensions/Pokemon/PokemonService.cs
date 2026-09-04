using System.Globalization;
using System.Text.Json;
using ColtonStack.Contracts;
using ColtonStack.Server.Infrastructure;
using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.Extensions.Options;

namespace ColtonStack.Server.Extensions.Pokemon;

/// <summary>
/// The passthrough-with-cache the client talks to. Two caches, two reasons: the name index is
/// tiny and hot (every keystroke), so it lives in memory; cards are bigger and rarely repeat, so
/// they live in SQLite and survive restarts. PokeAPI is only ever called on a miss.
/// </summary>
public sealed partial class PokemonService(
    PokeApiClient pokeApi,
    IDbConnectionFactory connectionFactory,
    IOptions<PokemonOptions> options,
    ILogger<PokemonService> logger) : IPokemonService
{
    private Task<IReadOnlyList<PokemonSummaryDto>>? _index;

    public async Task<IReadOnlyList<PokemonSummaryDto>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        var index = await GetIndexAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        var needle = Normalize(query);
        if (needle.Length == 0)
        {
            return [.. index.Take(limit)];
        }

        return [.. index
            .Where(entry => entry.Name.Contains(needle, StringComparison.Ordinal))
            .OrderByDescending(entry => entry.Name.StartsWith(needle, StringComparison.Ordinal))
            .ThenBy(entry => entry.Id)
            .Take(limit)];
    }

    public async Task<PokemonCardDto?> GetCardAsync(string nameOrId, CancellationToken cancellationToken)
    {
        var key = Normalize(nameOrId);
        if (key.Length == 0)
        {
            return null;
        }

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var cached = int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            ? await connection.GetAsync<PokemonCardRow>(id).ConfigureAwait(false)
            : await connection.QuerySingleOrDefaultAsync<PokemonCardRow>(
                "SELECT Id, Name, CardJson, FetchedAtUtc FROM PokemonCards WHERE Name = @name", new { name = key }).ConfigureAwait(false);

        if (cached is not null && DateTimeOffset.UtcNow - cached.FetchedAtUtc < TimeSpan.FromDays(options.Value.CacheDays))
        {
            CacheHit(cached.Name);
            return JsonSerializer.Deserialize(cached.CardJson, ColtonStackJsonContext.Default.PokemonCardDto);
        }

        var pokemon = await pokeApi.GetPokemonAsync(key, cancellationToken).ConfigureAwait(false);
        if (pokemon is null)
        {
            Unknown(key);
            return null;
        }

        // Species is best-effort: a card without a Pokédex entry beats no card.
        PokeApiModels.Species? species = null;
        try
        {
            species = await pokeApi.GetSpeciesAsync(pokemon.Id, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            SpeciesUnavailable(ex, pokemon.Id);
        }

        var card = PokemonCardMapper.Map(pokemon, species);
        var row = new PokemonCardRow
        {
            Id = card.Id,
            Name = pokemon.Name,
            CardJson = JsonSerializer.Serialize(card, ColtonStackJsonContext.Default.PokemonCardDto),
            FetchedAtUtc = DateTimeOffset.UtcNow,
        };

        if (cached is null)
        {
            await connection.InsertAsync(row).ConfigureAwait(false);
        }
        else
        {
            await connection.UpdateAsync(row).ConfigureAwait(false);
        }

        Fetched(card.Name, card.Id);
        return card;
    }

    /// <summary>One shared load; a failed load is forgotten so the next request retries instead of caching the failure.</summary>
    private Task<IReadOnlyList<PokemonSummaryDto>> GetIndexAsync()
    {
        var current = _index;
        if (current is { IsFaulted: false, IsCanceled: false })
        {
            return current;
        }

        var fresh = LoadIndexAsync();
        _index = fresh;
        return fresh;
    }

    private async Task<IReadOnlyList<PokemonSummaryDto>> LoadIndexAsync()
    {
        var response = await pokeApi.GetIndexAsync(options.Value.NameIndexLimit, CancellationToken.None).ConfigureAwait(false)
            ?? throw new InvalidOperationException("PokeAPI returned an empty index.");

        IReadOnlyList<PokemonSummaryDto> index = [.. response.Results
            .Select(entry => new PokemonSummaryDto(IdFromUrl(entry.Url), entry.Name))
            .Where(entry => entry.Id > 0)
            .OrderBy(entry => entry.Id)];
        IndexLoaded(index.Count);
        return index;
    }

    /// <summary>PokeAPI list entries carry no id, only ".../pokemon/25/" — the id is the last path segment.</summary>
    public static int IdFromUrl(string url)
    {
        var segments = url.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0 && int.TryParse(segments[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0;
    }

    private static string Normalize(string value) =>
        value.Trim().ToLowerInvariant().Replace(' ', '-');

    [LoggerMessage(Level = LogLevel.Information, Message = "Pokémon name index loaded with {Count} entries")]
    private partial void IndexLoaded(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Pokémon card cache hit for {Name}")]
    private partial void CacheHit(string name);

    [LoggerMessage(Level = LogLevel.Information, Message = "Fetched and cached Pokémon card {Name} (#{Id}) from PokeAPI")]
    private partial void Fetched(string name, int id);

    [LoggerMessage(Level = LogLevel.Information, Message = "PokeAPI has no Pokémon named {Name}")]
    private partial void Unknown(string name);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Species data for Pokémon #{Id} unavailable — card ships without a Pokédex entry")]
    private partial void SpeciesUnavailable(Exception exception, int id);
}
