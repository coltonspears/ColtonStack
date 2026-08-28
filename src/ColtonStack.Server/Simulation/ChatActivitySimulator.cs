using ColtonStack.Contracts;
using ColtonStack.Server.Data;
using ColtonStack.Server.Hubs;
using ColtonStack.Server.Infrastructure;
using ColtonStack.Server.Services;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ColtonStack.Server.Simulation;

/// <summary>
/// A configurable background service that simulates teammates chatting: every few seconds a
/// random user types (pushed to the channel group) and then posts a message through the same
/// save pipeline as real traffic — audit, SignalR broadcast, webhook outbox and all.
/// Controlled by <c>ColtonStack:Simulation</c> in appsettings.json and the
/// <c>/api/simulation</c> endpoints.
/// </summary>
public sealed partial class ChatActivitySimulator(
    SimulationState state,
    IOptions<SimulationOptions> options,
    IMessageService messages,
    IDbConnectionFactory connectionFactory,
    IHubContext<ChatHub, IChatHubClient> hubContext,
    ILogger<ChatActivitySimulator> logger) : BackgroundService
{
    private static readonly string[] Phrases =
    [
        "just pushed a fix for the flaky ordering test 🤞",
        "standup notes are up in the doc, add yours",
        "anyone else seeing the new build artifacts? they look great",
        "lunch order thread → 🌮",
        "the pipeline went green on the second try, classic",
        "reviewing PR #482 now, mostly nits",
        "heads up: deploying to staging in ~10",
        "TIL you can bind the same property two ways and only one of them is right",
        "whoever added dark mode tokens: thank you, my eyes thank you",
        "benchmark numbers updated in the doc, source-gen is still winning",
        "quick sanity check — is #incidents quiet because everything is fine, or…?",
        "reminder: retro at 3, bring one thing that went well",
        "the retry jitter change made the graph look so much calmer",
        "streaming the release build if anyone wants to watch logs scroll",
        "found the bug. it was dns. it is always dns.",
        "new starter kit is in the wiki — docs pass tomorrow",
        "coffee count today: 4. productivity: unclear.",
        "pairing on the composer polish after lunch if anyone's in",
        "the simulated pigeon has left the building 🐦",
        "does anyone actually read these? (hi mom)",
        "cache hit rate is at 97% after the index change, very satisfying",
        "office tip: the good meeting room is free until 2",
        "typed 'git push --force' in my sleep last night. woke up sweating.",
        "typo in the changelog fixed, please stop screenshotting it",
        "load test scheduled for 16:00, expect some chaos (the intentional kind)",
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        SimulatorStarted(state.Enabled, options.Value.MinIntervalSeconds, options.Value.MaxIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!state.Enabled)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);
                    continue;
                }

                var min = Math.Max(1, options.Value.MinIntervalSeconds);
                var max = Math.Max(min, options.Value.MaxIntervalSeconds);
                await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(min, max + 1)), stoppingToken).ConfigureAwait(false);

                if (state.Enabled)
                {
                    await SimulateOneAsync(stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                SimulationFailed(ex);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        SimulatorStopped();
    }

    private async Task SimulateOneAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        // Both tables hold a handful of demo rows, so Dapper.Contrib's GetAll + LINQ replaces SQL.
        var teammates = (await connection.GetAllAsync<UserRow>().ConfigureAwait(false))
            .Where(user => !user.IsSelf)
            .ToList();
        var channels = (await connection.GetAllAsync<ChannelRow>().ConfigureAwait(false)).ToList();

        if (teammates.Count == 0 || channels.Count == 0)
        {
            return;
        }

        var author = teammates[Random.Shared.Next(teammates.Count)];
        var channelId = channels[Random.Shared.Next(channels.Count)].Id;
        var text = Phrases[Random.Shared.Next(Phrases.Length)];

        if (options.Value.SimulateTyping)
        {
            await hubContext.Clients
                .Group(ChatHub.GroupNameFor(channelId))
                .UserTypingAsync(channelId, author.DisplayName)
                .ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(1, 3)), cancellationToken).ConfigureAwait(false);
        }

        await messages.SendAsUserAsync(channelId, author.Id, text, cancellationToken).ConfigureAwait(false);
        SimulatedMessage(author.DisplayName, channelId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Chat simulator started (enabled: {Enabled}, interval {Min}-{Max}s)")]
    private partial void SimulatorStarted(bool enabled, int min, int max);

    [LoggerMessage(Level = LogLevel.Information, Message = "Chat simulator stopped")]
    private partial void SimulatorStopped();

    [LoggerMessage(Level = LogLevel.Debug, Message = "{Author} simulated a message in channel {ChannelId}")]
    private partial void SimulatedMessage(string author, long channelId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Chat simulation step failed — will retry")]
    private partial void SimulationFailed(Exception exception);
}
