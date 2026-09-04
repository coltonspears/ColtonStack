using ColtonStack.Client.Messages;
using ColtonStack.Client.Services;
using ColtonStack.Client.ViewModels;
using ColtonStack.Contracts;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace ColtonStack.Tests;

/// <summary>
/// ChannelListViewModel receives messages through the messenger from both the SignalR hub
/// and the local HTTP client. These tests verify that channel creation and message posts
/// update the sidebar correctly — without a server, without SignalR, and on any thread.
/// </summary>
public sealed class ChannelListViewModelTests : IDisposable
{
    private readonly IMessenger _messenger = new WeakReferenceMessenger();
    private readonly ChannelListViewModel _vm;
    private readonly IColtonStackApiClient _api = Substitute.For<IColtonStackApiClient>();

    private static ChannelSummaryDto General => new(
        Id: 1, Name: "general", Topic: "Chat",
        MessageCount: 5, LastMessageId: 10,
        LastMessageAtUtc: new DateTimeOffset(2025, 6, 15, 14, 0, 0, TimeSpan.Zero),
        LastMessagePreview: "Hello");

    private static ChannelSummaryDto DevTalk => new(
        Id: 2, Name: "dev-talk", Topic: "Dev",
        MessageCount: 3, LastMessageId: 7,
        LastMessageAtUtc: new DateTimeOffset(2025, 6, 15, 13, 0, 0, TimeSpan.Zero),
        LastMessagePreview: "Bug fix");

    public ChannelListViewModelTests()
    {
        // The view model sees only the two seams — no HttpClient, no SignalR connection, no server.
        _vm = new ChannelListViewModel(_api, Substitute.For<IChatConnection>(), _messenger, NullLogger<ChannelListViewModel>.Instance);
        _messenger.RegisterAll(_vm);
    }

    public void Dispose()
    {
        _messenger.UnregisterAll(_vm);
    }

    [Fact]
    public void InitialState_EmptyChannels()
    {
        Assert.Empty(_vm.Channels);
        Assert.Null(_vm.SelectedChannel);
        Assert.False(_vm.IsLoading);
        Assert.False(_vm.IsAddingChannel);
    }

    [Fact]
    public void Receive_ChannelCreated_AddsToChannels()
    {
        _messenger.Send(new ChannelCreatedMessage(General));

        Assert.Single(_vm.Channels);
        Assert.Equal("general", _vm.Channels[0].Name);
    }

    [Fact]
    public void Receive_ChannelCreated_DedupesById()
    {
        _messenger.Send(new ChannelCreatedMessage(General));
        _messenger.Send(new ChannelCreatedMessage(General));

        // InsertIfMissing finds the existing row and calls UpdateFrom instead of adding
        Assert.Single(_vm.Channels);
    }

    [Fact]
    public void Receive_ChannelCreated_MultipleChannels()
    {
        _messenger.Send(new ChannelCreatedMessage(General));
        _messenger.Send(new ChannelCreatedMessage(DevTalk));

        Assert.Equal(2, _vm.Channels.Count);
    }

    [Fact]
    public void Receive_MessagePosted_UpdatesChannelPreview()
    {
        _messenger.Send(new ChannelCreatedMessage(General));

        var message = new MessageDto(
            Id: 11, ChannelId: 1, UserId: 1,
            AuthorName: "Colton", AuthorColor: "#E01E5A",
            Text: "New message here", CreatedAtUtc: DateTimeOffset.UtcNow);

        _messenger.Send(new MessagePostedMessage(message));

        var item = _vm.Channels[0];
        Assert.Equal("New message here", item.Preview);
        Assert.Equal(1, item.UnreadCount); // nothing is selected, so activity in #general is unread
    }

    [Fact]
    public void Receive_MessagePosted_IncrementsMessageCount()
    {
        _messenger.Send(new ChannelCreatedMessage(General));

        var before = _vm.Channels[0].Summary.MessageCount;

        var message = new MessageDto(
            Id: 11, ChannelId: 1, UserId: 1,
            AuthorName: "Colton", AuthorColor: "#E01E5A",
            Text: "Yet another", CreatedAtUtc: DateTimeOffset.UtcNow);

        _messenger.Send(new MessagePostedMessage(message));

        Assert.Equal(before + 1, _vm.Channels[0].Summary.MessageCount);
    }

    [Fact]
    public void Receive_MessagePosted_IgnoresWrongChannel()
    {
        _messenger.Send(new ChannelCreatedMessage(General));

        var message = new MessageDto(
            Id: 11, ChannelId: 999, UserId: 1,
            AuthorName: "Colton", AuthorColor: "#E01E5A",
            Text: "Ghost", CreatedAtUtc: DateTimeOffset.UtcNow);

        // Should not throw and should not add anything (channel 999 doesn't exist)
        _messenger.Send(new MessagePostedMessage(message));

        Assert.Single(_vm.Channels);
    }

    [Fact]
    public void ToggleAddChannel_FlipsFlag()
    {
        Assert.False(_vm.IsAddingChannel);

        _vm.ToggleAddChannelCommand.Execute(null);
        Assert.True(_vm.IsAddingChannel);

        _vm.ToggleAddChannelCommand.Execute(null);
        Assert.False(_vm.IsAddingChannel);
    }
}