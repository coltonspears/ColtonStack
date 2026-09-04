using System.Text.Json;
using ColtonStack.Contracts;
using ColtonStack.Server.Infrastructure;
using ColtonStack.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace ColtonStack.Server.Extensions.Pokemon;

/// <summary>
/// The Pokémon feature's server half: a resilient PokeAPI client, an in-memory name index, a
/// SQLite card cache (its own table, via <see cref="ISchemaContributor"/>) and three endpoints.
/// Posting a card reuses the core message pipeline through <see cref="IMessageService"/> with an
/// attachment — audit, SignalR and webhooks all happen without this extension knowing how.
/// </summary>
public sealed class PokemonExtension : IServerStartup
{
    public void ConfigureServices(IServiceCollection services, ConfigurationManager configuration)
    {
        services.Configure<PokemonOptions>(configuration.GetSection(PokemonOptions.SectionName));

        // Typed client + Microsoft's standard resilience handler: retry with backoff, circuit
        // breaker, attempt and total timeouts — the same shape the WPF client uses inbound.
        services
            .AddHttpClient<PokeApiClient>((provider, client) =>
            {
                client.BaseAddress = new Uri(provider.GetRequiredService<IOptions<PokemonOptions>>().Value.BaseUrl);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("ColtonStack/1.0 (+https://github.com/coltonspears/coltonstack)");
            })
            .AddStandardResilienceHandler();

        services.AddSingleton<IPokemonService, PokemonService>();
        services.AddSingleton<ISchemaContributor, PokemonSchema>();
    }

    public void ConfigureApp(WebApplication app)
    {
        var api = app.MapGroup("/api/pokemon");

        api.MapGet("/search", async Task<IResult> (IPokemonService pokemon, string q = "", int limit = 8, CancellationToken cancellationToken = default) =>
        {
            var matches = await pokemon.SearchAsync(q, Math.Clamp(limit, 1, 25), cancellationToken);
            return TypedResults.Ok(matches);
        });

        api.MapGet("/{name}", async Task<IResult> (string name, IPokemonService pokemon, CancellationToken cancellationToken) =>
        {
            var card = await pokemon.GetCardAsync(name, cancellationToken);
            return card is null
                ? TypedResults.NotFound(new { error = $"No Pokémon named '{name}'." })
                : TypedResults.Ok(card);
        });

        // Share a card into a channel: look it up (cache or PokeAPI), then hand the core pipeline
        // a message with a typed attachment. The card JSON is the same shape the client parses.
        app.MapPost("/api/channels/{channelId:long}/pokemon/{name}", async Task<IResult> (
            long channelId,
            string name,
            IPokemonService pokemon,
            IMessageService messages,
            CancellationToken cancellationToken) =>
        {
            var card = await pokemon.GetCardAsync(name, cancellationToken);
            if (card is null)
            {
                return TypedResults.NotFound(new { error = $"No Pokémon named '{name}'." });
            }

            try
            {
                var attachment = new MessageAttachmentDto(
                    PokemonCardDto.AttachmentKind,
                    JsonSerializer.Serialize(card, ColtonStackJsonContext.Default.PokemonCardDto));
                var message = await messages.SendAsync(channelId, $"shared a Pokémon card: {card.Name} #{card.Id}", attachment, cancellationToken);
                return TypedResults.Created($"/api/channels/{channelId}/messages/{message.Id}", message);
            }
            catch (ChannelNotFoundException ex)
            {
                return TypedResults.NotFound(new { error = ex.Message });
            }
        });
    }
}
