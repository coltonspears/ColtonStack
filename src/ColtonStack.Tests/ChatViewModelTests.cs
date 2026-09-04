using System.Net.Http;
using ColtonStack.Client.Extensions.Attachments;
using ColtonStack.Client.Extensions.Commands;
using ColtonStack.Client.Messages;
using ColtonStack.Client.Services;
using ColtonStack.Client.ViewModels;
using ColtonStack.Contracts;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace ColtonStack.Tests;

/// <summary>
/// The conversation view model with every dependency substituted: HTTP, SignalR, attachments,
/// commands. No dispatcher is involved because the class contains no threading code — the
/// messenger decorator handles that at the app boundary, not here.
/// </summary>
public sealed class ChatViewModelTests : IDisposable
{
    private static readonly ChannelSummaryDto General = new(1, "general", "Chat", 0, 0, DateTimeOffset.UtcNow, string.Empty);

    private readonly IColtonStackApiClient _api = Substitute.For<IColtonStackApiClient>();
    private readonly IChatConnection _hub = Substitute.For<IChatConnection>();
    private readonly CommandRegistry _commands = new();
    private readonly AttachmentRegistry _attachments = new();
    private readonly WeakReferenceMessenger _messenger = new();
    private readonly ServiceProvider _provider = new ServiceCollection().BuildServiceProvider();
    private readonly ChatViewModel _vm;

    public ChatViewModelTests()
    {
        _api.GetMessagesAsync(Arg.Any<long>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<MessageDto>>([]));
        _commands.Attach(_provider);
        _attachments.Attach(_provider);

        _vm = new ChatViewModel(_api, _hub, _commands, _attachments, _messenger, NullLogger<ChatViewModel>.Instance);
        _messenger.RegisterAll(_vm);
    }

    public void Dispose()
    {
        _messenger.UnregisterAll(_vm);
        _vm.Dispose();
        _provider.Dispose();
    }

    private static MessageDto Message(long id, string author, string text, DateTimeOffset at, MessageAttachmentDto? attachment = null) =>
        new(id, General.Id, UserId: 1, author, "#000", text, at) { Attachment = attachment };

    [Fact]
    public void InitialState_NoChannel_CannotSend()
    {
        Assert.False(_vm.HasChannel);
        Assert.Equal("No channel selected", _vm.ChannelTitle);
        Assert.False(_vm.SendMessageCommand.CanExecute(null));
    }

    [Fact]
    public void Receive_ChannelSelected_LoadsHistory_AndGroupsMessages()
    {
        var t0 = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        _api.GetMessagesAsync(General.Id, 0, Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<MessageDto>>(
        [
            Message(1, "Ann", "hi", t0),
            Message(2, "Ann", "again", t0.AddMinutes(1)),      // same author, within the gap → grouped
            Message(3, "Ann", "much later", t0.AddMinutes(30)), // gap exceeded → new group
            Message(4, "Bob", "hello", t0.AddMinutes(31)),      // author changed → new group
            Message(5, "Bob", "tomorrow", t0.AddDays(1)),       // new day → divider + new group
        ]));

        _messenger.Send(new ChannelSelectedMessage(General));

        Assert.True(_vm.HasChannel);
        Assert.Equal("# general", _vm.ChannelTitle);
        Assert.Equal([true, false, true, true, true], _vm.Messages.Select(m => m.IsFirstOfGroup));
        Assert.Equal([true, false, false, false, true], _vm.Messages.Select(m => m.IsFirstOfDay));
        Assert.False(_vm.IsLoadingHistory);
    }

    [Fact]
    public void Receive_MessagePosted_AppendsOnlyForCurrentChannel_AndDedupesById()
    {
        _messenger.Send(new ChannelSelectedMessage(General));
        var now = DateTimeOffset.UtcNow;

        _messenger.Send(new MessagePostedMessage(Message(10, "Ann", "a", now)));
        _messenger.Send(new MessagePostedMessage(Message(10, "Ann", "a", now)));           // hub echo of the same id
        _messenger.Send(new MessagePostedMessage(Message(11, "Ann", "b", now) with { ChannelId = 99 }));

        Assert.Equal([10L], _vm.Messages.Select(m => m.Id));
    }

    [Fact]
    public async Task Send_PlainText_PostsThroughTheApi_AndClearsTheDraft()
    {
        _messenger.Send(new ChannelSelectedMessage(General));
        _api.SendMessageAsync(General.Id, "hello", Arg.Any<CancellationToken>())
            .Returns(Message(42, "Me", "hello", DateTimeOffset.UtcNow));

        _vm.Draft = "  hello ";
        Assert.True(_vm.SendMessageCommand.CanExecute(null));
        await _vm.SendMessageCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, _vm.Draft);
        Assert.Equal([42L], _vm.Messages.Select(m => m.Id));
    }

    [Fact]
    public async Task Send_WhenTheApiFails_RestoresTheDraft_AndReportsOnTheStatusBar()
    {
        _messenger.Send(new ChannelSelectedMessage(General));
        _api.SendMessageAsync(General.Id, "hello", Arg.Any<CancellationToken>())
            .Returns<MessageDto>(_ => throw new HttpRequestException("down"));
        HttpRetryMessage? reported = null;
        _messenger.Register<HttpRetryMessage>(this, (_, m) => reported = m);

        _vm.Draft = "hello";
        await _vm.SendMessageCommand.ExecuteAsync(null);

        Assert.Equal("hello", _vm.Draft);
        Assert.NotNull(reported);
        Assert.Contains("down", reported.Detail, StringComparison.Ordinal);
        Assert.Empty(_vm.Messages);
    }

    [Fact]
    public async Task Send_SlashCommand_RunsTheRegisteredCommand_WithArgumentAndChannel()
    {
        CommandInvocation? invocation = null;
        _commands.Register(new CommandDefinition(
            "chat.shrug", "Shrug", "d", "\uE76E", "Chat",
            executeAsync: (_, inv) => { invocation = inv; return Task.CompletedTask; },
            slashName: "shrug"));
        _messenger.Send(new ChannelSelectedMessage(General));

        _vm.Draft = "/shrug well then";
        await _vm.SendMessageCommand.ExecuteAsync(null);

        Assert.NotNull(invocation);
        Assert.Equal("well then", invocation.Argument);
        Assert.Equal(General.Id, invocation.ChannelId);
        Assert.Equal(string.Empty, _vm.Draft);
        await _api.DidNotReceive().SendMessageAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_UnknownSlashCommand_KeepsTheDraft_AndReports()
    {
        _messenger.Send(new ChannelSelectedMessage(General));
        HttpRetryMessage? reported = null;
        _messenger.Register<HttpRetryMessage>(this, (_, m) => reported = m);

        _vm.Draft = "/dance";
        await _vm.SendMessageCommand.ExecuteAsync(null);

        Assert.Equal("/dance", _vm.Draft);
        Assert.NotNull(reported);
        Assert.Contains("/dance", reported.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Send_FailingSlashCommand_RestoresTheDraft()
    {
        _commands.Register(new CommandDefinition(
            "boom", "Boom", "d", "\uE76E", "Chat",
            executeAsync: (_, _) => throw new InvalidOperationException("nope"),
            slashName: "boom"));
        _messenger.Send(new ChannelSelectedMessage(General));

        _vm.Draft = "/boom now";
        await _vm.SendMessageCommand.ExecuteAsync(null);

        Assert.Equal("/boom now", _vm.Draft);
    }

    [Fact]
    public void Attachments_AreMaterializedThroughTheRegistry()
    {
        _attachments.Register("pokemon", (_, json) => $"card:{json}");
        _messenger.Send(new ChannelSelectedMessage(General));
        var now = DateTimeOffset.UtcNow;

        _messenger.Send(new MessagePostedMessage(Message(1, "Ann", "look", now, new MessageAttachmentDto("pokemon", "{}"))));
        _messenger.Send(new MessagePostedMessage(Message(2, "Ann", "plain", now)));
        _messenger.Send(new MessagePostedMessage(Message(3, "Ann", "?", now, new MessageAttachmentDto("mystery", "{}"))));

        Assert.Equal("card:{}", _vm.Messages[0].Attachment);
        Assert.Null(_vm.Messages[1].Attachment);
        Assert.IsType<UnknownAttachmentViewModel>(_vm.Messages[2].Attachment);
        Assert.True(_vm.Messages[1].IsFirstOfGroup); // a card always breaks the visual group
    }

    [Fact]
    public void ClosingSearch_ClearsTheFilter()
    {
        _vm.IsSearchActive = true;
        _vm.Search.Filter = "x";

        _vm.IsSearchActive = false;

        Assert.Equal(string.Empty, _vm.Search.Filter);
        Assert.Equal(string.Empty, _vm.SearchText);
    }

    [Fact]
    public void Typing_NotifiesTheHub_ButNotForSlashCommands()
    {
        _messenger.Send(new ChannelSelectedMessage(General));

        _vm.Draft = "/pok";
        _ = _hub.DidNotReceive().NotifyTypingAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());

        _vm.Draft = "hello";
        _ = _hub.Received(1).NotifyTypingAsync(General.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Receive_UserTyping_ShowsIndicator_ForCurrentChannelOnly()
    {
        _messenger.Send(new ChannelSelectedMessage(General));

        _messenger.Send(new UserTypingMessage(99, "Ghost"));
        Assert.Equal(string.Empty, _vm.TypingIndicator);

        _messenger.Send(new UserTypingMessage(General.Id, "Ann"));
        Assert.Equal("Ann is typing…", _vm.TypingIndicator);
    }
}
