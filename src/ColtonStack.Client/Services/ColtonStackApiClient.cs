using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using ColtonStack.Client.Configuration;
using ColtonStack.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ColtonStack.Client.Services;

/// <summary>
/// Typed HTTP client for the ColtonStack server. Every call is asynchronous, cancellation-aware,
/// and wrapped by the resilience pipeline configured in App.xaml.cs (retry + circuit breaker +
/// timeout) — this class itself contains zero retry/timeout code.
/// JSON uses the source-generated <see cref="ColtonStackJsonContext"/> — no reflection serializer.
/// </summary>
public sealed partial class ColtonStackApiClient(
    HttpClient httpClient,
    ILogger<ColtonStackApiClient> logger)
{
    public async Task<IReadOnlyList<ChannelSummaryDto>> GetChannelsAsync(CancellationToken cancellationToken)
    {
        var channels = await httpClient
            .GetFromJsonAsync("api/channels", ColtonStackJsonContext.Default.IReadOnlyListChannelSummaryDto, cancellationToken)
            .ConfigureAwait(false);
        return channels ?? [];
    }

    public async Task<IReadOnlyList<MessageDto>> GetMessagesAsync(long channelId, long afterId, CancellationToken cancellationToken)
    {
        var messages = await httpClient
            .GetFromJsonAsync($"api/channels/{channelId}/messages?afterId={afterId}", ColtonStackJsonContext.Default.IReadOnlyListMessageDto, cancellationToken)
            .ConfigureAwait(false);
        return messages ?? [];
    }

    public async Task<MessageDto> SendMessageAsync(long channelId, string text, CancellationToken cancellationToken)
    {
        using var response = await httpClient
            .PostAsJsonAsync($"api/channels/{channelId}/messages", new SendMessageRequest(text), ColtonStackJsonContext.Default.SendMessageRequest, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            SendMessageRejected((int)response.StatusCode, channelId);
            throw new HttpRequestException($"Sending the message failed ({(int)response.StatusCode} {response.StatusCode}).");
        }

        var message = await response.Content
            .ReadFromJsonAsync(ColtonStackJsonContext.Default.MessageDto, cancellationToken)
            .ConfigureAwait(false);
        return message ?? throw new InvalidOperationException("The server returned an empty message.");
    }

    public async Task<ChannelSummaryDto> CreateChannelAsync(string name, string topic, CancellationToken cancellationToken)
    {
        using var response = await httpClient
            .PostAsJsonAsync("api/channels", new CreateChannelRequest(name, topic), ColtonStackJsonContext.Default.CreateChannelRequest, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            CreateChannelRejected((int)response.StatusCode, name);
            throw new HttpRequestException($"Creating the channel failed ({(int)response.StatusCode} {response.StatusCode}).");
        }

        var channel = await response.Content
            .ReadFromJsonAsync(ColtonStackJsonContext.Default.ChannelSummaryDto, cancellationToken)
            .ConfigureAwait(false);
        return channel ?? throw new InvalidOperationException("The server returned an empty channel.");
    }

    public async Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken cancellationToken)
    {
        var users = await httpClient
            .GetFromJsonAsync("api/users", ColtonStackJsonContext.Default.IReadOnlyListUserDto, cancellationToken)
            .ConfigureAwait(false);
        return users ?? [];
    }

    public async Task<UserDto> UpdateProfileAsync(string displayName, string avatarColor, CancellationToken cancellationToken)
    {
        using var response = await httpClient
            .PutAsJsonAsync("api/users/me", new UpdateProfileRequest(displayName, avatarColor), ColtonStackJsonContext.Default.UpdateProfileRequest, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            UpdateProfileRejected((int)response.StatusCode);
            throw new HttpRequestException($"Saving the profile failed ({(int)response.StatusCode} {response.StatusCode}).");
        }

        var user = await response.Content
            .ReadFromJsonAsync(ColtonStackJsonContext.Default.UserDto, cancellationToken)
            .ConfigureAwait(false);
        return user ?? throw new InvalidOperationException("The server returned an empty profile.");
    }

    public async Task SetChaosAsync(bool enabled, CancellationToken cancellationToken)
    {
        using var response = await httpClient
            .PostAsync($"api/chaos/{enabled}", content: null, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> GetSimulationAsync(CancellationToken cancellationToken)
    {
        var state = await httpClient
            .GetFromJsonAsync("api/simulation", ColtonStackJsonContext.Default.SimulationStateDto, cancellationToken)
            .ConfigureAwait(false);
        return state?.Enabled ?? false;
    }

    public async Task SetSimulationAsync(bool enabled, CancellationToken cancellationToken)
    {
        using var response = await httpClient
            .PostAsync($"api/simulation/{enabled}", content: null, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Server rejected a message send for channel {ChannelId} with status {StatusCode}")]
    private partial void SendMessageRejected(int statusCode, long channelId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Server rejected channel creation for '{Name}' with status {StatusCode}")]
    private partial void CreateChannelRejected(int statusCode, string name);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Server rejected the profile update with status {StatusCode}")]
    private partial void UpdateProfileRejected(int statusCode);
}
