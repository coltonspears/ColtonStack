using System.Text.Json;
using ColtonStack.Client.Extensions.Commands;
using ColtonStack.Client.Extensions.Settings;
using ColtonStack.Client.Services;
using ColtonStack.Client.ViewModels;
using ColtonStack.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace ColtonStack.Client.Extensions.Pokemon;

/// <summary>
/// The Pokémon feature's client half, and the reference example of every extension seam at once:
/// a typed API client on the shared resilience pipeline, a <c>/pokemon</c> slash command with
/// server-backed autocomplete, a palette command, an attachment renderer plus its XAML, and a
/// settings section. Nothing in the core mentions Pokémon; delete this file and the app still
/// builds and runs (cards already in the database render as "unknown attachment").
/// </summary>
public sealed class PokemonExtension : IClientStartup
{
    public void Configure(IClientStartupContext context)
    {
        context.Services.AddColtonStackHttpClient<PokemonApi>("coltonstack-pokemon");
        context.Services.AddSingleton<IPokemonApi>(sp => sp.GetRequiredService<PokemonApi>());
        context.Services.AddSingleton<PokemonSettingsViewModel>();

        // Attachment renderer: JSON → view model; the DataTemplate in PokemonCardTemplates.xaml does the rest.
        context.Attachments.Register(PokemonCardDto.AttachmentKind, (services, json) =>
        {
            var card = JsonSerializer.Deserialize(json, ColtonStackJsonContext.Default.PokemonCardDto)
                ?? throw new InvalidOperationException("Empty Pokémon card payload.");
            return new PokemonCardViewModel(card, services.GetRequiredService<ISettingsStore>());
        });

        // /pokemon <name> — autocomplete comes from the server's cached name index.
        context.Commands.Register(new CommandDefinition(
            id: "pokemon.share",
            title: "Share a Pokémon card",
            description: "Post a Pokédex card for a Pokémon into the current channel",
            iconGlyph: "\uE8B9",
            category: "Pokémon",
            keywords: ["pokedex", "card", "pikachu"],
            slashName: "pokemon",
            argumentHint: "name — start typing for suggestions",
            suggestAsync: async (services, argument, cancellationToken) =>
            {
                var matches = await services.GetRequiredService<IPokemonApi>().SearchAsync(argument, cancellationToken);
                return [.. matches.Select(match => new CommandSuggestion(Capitalize(match.Name), match.Name, $"#{match.Id:000}"))];
            },
            executeAsync: async (services, invocation) =>
            {
                var channelId = invocation.ChannelId ?? services.GetRequiredService<ChatViewModel>().CurrentChannel?.Id
                    ?? throw new InvalidOperationException("Select a channel first.");
                var name = invocation.Argument.Length > 0 ? invocation.Argument : RandomKantoId();
                await services.GetRequiredService<IPokemonApi>().ShareAsync(channelId, name, invocation.CancellationToken);
            }));

        context.Settings.Register(new SettingsSectionDefinition(
            id: "pokemon",
            title: "Pokémon cards",
            description: "Artwork style and shiny default",
            iconGlyph: "\uE8B9",
            order: 20,
            contentFactory: services => services.GetRequiredService<PokemonSettingsViewModel>()));

        context.AddResourceDictionary(
            "pack://application:,,,/ColtonStack.Client;component/Extensions/Pokemon/PokemonCardTemplates.xaml");
    }

    private static string Capitalize(string apiName) =>
        string.Join(' ', apiName.Split('-', StringSplitOptions.RemoveEmptyEntries).Select(word => char.ToUpperInvariant(word[0]) + word[1..]));

    private static string RandomKantoId() =>
        Random.Shared.Next(1, 152).ToString(System.Globalization.CultureInfo.InvariantCulture);
}
