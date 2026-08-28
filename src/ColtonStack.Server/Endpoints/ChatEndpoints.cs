using ColtonStack.Contracts;
using ColtonStack.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ColtonStack.Server.Endpoints;

/// <summary>Channel and message endpoints — the load/save surface the WPF client talks to.</summary>
public static class ChatEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/channels", async Task<IResult> (IChannelService channels, CancellationToken cancellationToken) =>
        {
            var summaries = await channels.GetSummariesAsync(cancellationToken);
            return TypedResults.Ok(summaries);
        });

        api.MapPost("/channels", async Task<IResult> (CreateChannelRequest request, IChannelService channels, CancellationToken cancellationToken) =>
        {
            try
            {
                var channel = await channels.CreateAsync(request, cancellationToken);
                return TypedResults.Created($"/api/channels/{channel.Id}", channel);
            }
            catch (DuplicateChannelException ex)
            {
                return TypedResults.Conflict(new { error = ex.Message });
            }
        });

        api.MapGet("/channels/{channelId:long}/messages", async Task<IResult> (
            IMessageService messages,
            long channelId,
            long afterId = 0,
            int limit = 200,
            CancellationToken cancellationToken = default) =>
        {
            try
            {
                var result = await messages.GetRecentAsync(channelId, afterId, limit, cancellationToken);
                return TypedResults.Ok(result);
            }
            catch (ChannelNotFoundException ex)
            {
                return TypedResults.NotFound(new { error = ex.Message });
            }
        });

        api.MapPost("/channels/{channelId:long}/messages", async Task<IResult> (
            long channelId,
            SendMessageRequest request,
            IMessageService messages,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var message = await messages.SendAsync(channelId, request, cancellationToken);
                return TypedResults.Created($"/api/channels/{channelId}/messages/{message.Id}", message);
            }
            catch (ChannelNotFoundException ex)
            {
                return TypedResults.NotFound(new { error = ex.Message });
            }
        });
    }
}
