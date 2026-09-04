using ColtonStack.Client.ViewModels;
using ColtonStack.Contracts;
using Xunit;

namespace ColtonStack.Tests;

/// <summary>
/// ChannelListItemViewModel is a small observable model for one sidebar row.
/// Most properties are immutable (Id, Name, Topic); only Preview and UnreadCount change.
/// These tests exercise both the immutable and mutable surfaces.
/// </summary>
public sealed class ChannelListItemViewModelTests
{
    private static ChannelSummaryDto Summary => new(
        Id: 1, Name: "general", Topic: "Company-wide chat",
        MessageCount: 42, LastMessageId: 99,
        LastMessageAtUtc: new DateTimeOffset(2025, 6, 15, 14, 30, 0, TimeSpan.Zero),
        LastMessagePreview: "Hello everyone");

    [Fact]
    public void Constructor_SetsPropertiesFromSummary()
    {
        var item = new ChannelListItemViewModel(Summary);

        Assert.Equal(1, item.Id);
        Assert.Equal("general", item.Name);
        Assert.Equal("Company-wide chat", item.Topic);
        Assert.Equal("Hello everyone", item.Preview);
        Assert.Equal(0, item.UnreadCount);
        Assert.False(item.HasUnread);
    }

    [Fact]
    public void DisplayName_PrependsHash()
    {
        var item = new ChannelListItemViewModel(Summary);
        Assert.Equal("# general", item.DisplayName);
    }

    [Fact]
    public void Summary_ReturnsConstructorSummary()
    {
        var summary = Summary;
        var item = new ChannelListItemViewModel(summary);
        Assert.Same(summary, item.Summary);
    }

    [Fact]
    public void UpdateFrom_ReplacesSummaryAndPreview()
    {
        var item = new ChannelListItemViewModel(Summary);

        var updatedSummary = Summary with
        {
            LastMessagePreview = "Updated message",
            LastMessageId = 100,
            MessageCount = 43,
        };
        item.UpdateFrom(updatedSummary);

        // Summary reference is updated
        Assert.Same(updatedSummary, item.Summary);

        // Preview reflects the new value
        Assert.Equal("Updated message", item.Preview);
    }

    [Fact]
    public void UpdateFrom_NullPreview_UsesFallback()
    {
        var item = new ChannelListItemViewModel(Summary);

        var updatedSummary = Summary with { LastMessagePreview = null };
        item.UpdateFrom(updatedSummary);

        Assert.Equal("No messages yet", item.Preview);
    }

    [Fact]
    public void UnreadCount_SetsAndClampsToZero()
    {
        var item = new ChannelListItemViewModel(Summary);

        item.UnreadCount = 5;
        Assert.Equal(5, item.UnreadCount);
        Assert.True(item.HasUnread);

        item.UnreadCount = -3;
        Assert.Equal(0, item.UnreadCount);
        Assert.False(item.HasUnread);
    }

    [Fact]
    public void HasUnread_ReflectsUnreadCount()
    {
        var item = new ChannelListItemViewModel(Summary);

        Assert.False(item.HasUnread); // default 0

        item.UnreadCount = 1;
        Assert.True(item.HasUnread);

        item.UnreadCount = 0;
        Assert.False(item.HasUnread);
    }
}