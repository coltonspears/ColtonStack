using ColtonStack.Contracts;
using ColtonStack.Server.Services;
using FluentValidation;
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

        api.MapGet("/channels", GetChannelsAsync);
        api.MapPost("/channels", CreateChannelAsync);
        api.MapGet("/channels/{channelId:long}/messages", GetMessagesAsync);
        api.MapPost("/channels/{channelId:long}/messages", SendMessageAsync);
    }

    private static async Task<IResult> GetChannelsAsync(IChannelService channels, CancellationToken cancellationToken)
    {
        var summaries = await channels.GetSummariesAsync(cancellationToken);
        return TypedResults.Ok(summaries);
    }

    private static async Task<IResult> CreateChannelAsync(
        CreateChannelRequest request,
        IValidator<CreateChannelRequest> validator,
        IChannelService channels,
        CancellationToken cancellationToken)
    {
        if (await EndpointValidation.ValidateAsync(validator, request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        try
        {
            var channel = await channels.CreateAsync(request, cancellationToken);
            return TypedResults.Created($"/api/channels/{channel.Id}", channel);
        }
        catch (DuplicateChannelException ex)
        {
            return TypedResults.Conflict(new { error = ex.Message });
        }
    }

    private static async Task<IResult> GetMessagesAsync(
        IMessageService messages,
        long channelId,
        long afterId = 0,
        int limit = 200,
        CancellationToken cancellationToken = default)
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
    }

    private static async Task<IResult> SendMessageAsync(
        long channelId,
        SendMessageRequest request,
        IValidator<SendMessageRequest> validator,
        IMessageService messages,
        CancellationToken cancellationToken)
    {
        if (await EndpointValidation.ValidateAsync(validator, request, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        try
        {
            var message = await messages.SendAsync(channelId, request.Text, attachment: null, cancellationToken);
            return TypedResults.Created($"/api/channels/{channelId}/messages/{message.Id}", message);
        }
        catch (ChannelNotFoundException ex)
        {
            return TypedResults.NotFound(new { error = ex.Message });
        }
    }
}
