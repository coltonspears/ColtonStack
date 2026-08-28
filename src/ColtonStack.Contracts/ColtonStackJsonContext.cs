using System.Text.Json.Serialization;

namespace ColtonStack.Contracts;

/// <summary>
/// System.Text.Json *source generation*: all serializer metadata is emitted at compile time.
/// Serialization uses zero runtime reflection — faster startup, no per-type reflection setup,
/// and the trimming/analysis toolchain can see every serialized shape.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(MessageDto))]
[JsonSerializable(typeof(ChannelSummaryDto))]
[JsonSerializable(typeof(UserDto))]
[JsonSerializable(typeof(CreateChannelRequest))]
[JsonSerializable(typeof(SendMessageRequest))]
[JsonSerializable(typeof(WebhookRegistrationDto))]
[JsonSerializable(typeof(RegisterWebhookRequest))]
[JsonSerializable(typeof(WebhookPayload))]
[JsonSerializable(typeof(AuditEntryDto))]
[JsonSerializable(typeof(SimulationStateDto))]
[JsonSerializable(typeof(IReadOnlyList<MessageDto>))]
[JsonSerializable(typeof(IReadOnlyList<ChannelSummaryDto>))]
[JsonSerializable(typeof(IReadOnlyList<WebhookRegistrationDto>))]
[JsonSerializable(typeof(IReadOnlyList<AuditEntryDto>))]
public sealed partial class ColtonStackJsonContext : JsonSerializerContext;
