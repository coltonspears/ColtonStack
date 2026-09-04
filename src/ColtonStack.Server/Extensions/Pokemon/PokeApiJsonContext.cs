using System.Text.Json.Serialization;

namespace ColtonStack.Server.Extensions.Pokemon;

/// <summary>Source-generated serializer metadata for the PokeAPI payloads (snake_case on the wire).</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(PokeApiModels.ListResponse))]
[JsonSerializable(typeof(PokeApiModels.Pokemon))]
[JsonSerializable(typeof(PokeApiModels.Species))]
public sealed partial class PokeApiJsonContext : JsonSerializerContext;
