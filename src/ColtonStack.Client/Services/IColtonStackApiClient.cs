using ColtonStack.Contracts;

namespace ColtonStack.Client.Services;

/// <summary>
/// The client's view of the ColtonStack HTTP API. View models depend on this seam, not on the
/// typed <see cref="ColtonStackApiClient"/> — so a unit test substitutes it with one line and
/// never spins up an <c>HttpClient</c>, a resilience pipeline or a server.
/// </summary>
public interface IColtonStackApiClient
{
    Task<IReadOnlyList<ChannelSummaryDto>> GetChannelsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<MessageDto>> GetMessagesAsync(long channelId, long afterId, CancellationToken cancellationToken);

    Task<MessageDto> SendMessageAsync(long channelId, string text, CancellationToken cancellationToken);

    Task<ChannelSummaryDto> CreateChannelAsync(string name, string topic, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken cancellationToken);

    Task<UserDto> UpdateProfileAsync(string displayName, string avatarColor, CancellationToken cancellationToken);

    Task SetChaosAsync(bool enabled, CancellationToken cancellationToken);

    Task<bool> GetSimulationAsync(CancellationToken cancellationToken);

    Task SetSimulationAsync(bool enabled, CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditEntryDto>> GetAuditAsync(int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<SettingDto>> GetSettingsAsync(CancellationToken cancellationToken);

    Task<SettingDto> PutSettingAsync(string key, string value, CancellationToken cancellationToken);
}
