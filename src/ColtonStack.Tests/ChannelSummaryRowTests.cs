using ColtonStack.Contracts;
using ColtonStack.Server.Services;
using Xunit;

namespace ColtonStack.Tests;

/// <summary>
/// ChannelSummaryRow lives at the boundary between Dapper (raw SQLite types) and the clean
/// application DTO. This mapping is explicit — no reflection conventions, no magic — and
/// therefore fully testable in isolation.
/// </summary>
public sealed class ChannelSummaryRowTests
{
    [Fact]
    public void ToDto_HappyPath()
    {
        var row = new ChannelSummaryRow(
            Id: 1,
            Name: "general",
            Topic: "Company-wide chat",
            MessageCount: 42,
            LastMessageId: 99,
            LastMessageAtUtc: "2025-06-15T14:30:00.0000000+00:00",
            LastMessagePreview: "Hello everyone");

        var dto = row.ToDto();

        Assert.Equal(1, dto.Id);
        Assert.Equal("general", dto.Name);
        Assert.Equal("Company-wide chat", dto.Topic);
        Assert.Equal(42, dto.MessageCount);
        Assert.Equal(99, dto.LastMessageId);
        Assert.Equal("Hello everyone", dto.LastMessagePreview);
        Assert.NotNull(dto.LastMessageAtUtc);
        Assert.Equal(2025, dto.LastMessageAtUtc!.Value.Year);
        Assert.Equal(6, dto.LastMessageAtUtc!.Value.Month);
    }

    [Fact]
    public void ToDto_NullLastMessageIdAndPreview()
    {
        var row = new ChannelSummaryRow(
            Id: 2,
            Name: "empty-channel",
            Topic: "No messages yet",
            MessageCount: 0,
            LastMessageId: 0,
            LastMessageAtUtc: "",
            LastMessagePreview: null);

        var dto = row.ToDto();

        Assert.Equal(2, dto.Id);
        Assert.Equal(0, dto.MessageCount);
        Assert.Null(dto.LastMessageId);
        Assert.Null(dto.LastMessageAtUtc);
        Assert.Null(dto.LastMessagePreview);
    }

    [Fact]
    public void ToDto_LargeMessageCount()
    {
        var row = new ChannelSummaryRow(
            Id: 3, Name: "busy", Topic: "",
            MessageCount: int.MaxValue,
            LastMessageId: 999999,
            LastMessageAtUtc: "2025-06-15T14:30:00.0000000+00:00",
            LastMessagePreview: "last message");

        // checked((int)MessageCount) should succeed since int.MaxValue fits in int.
        var dto = row.ToDto();
        Assert.Equal(int.MaxValue, dto.MessageCount);
    }

    [Fact]
    public void ToDto_EmptyLastMessageAtUtc_ReturnsNull()
    {
        var row = new ChannelSummaryRow(
            Id: 4, Name: "x", Topic: "",
            MessageCount: 0, LastMessageId: 0,
            LastMessageAtUtc: "", LastMessagePreview: null);

        var dto = row.ToDto();
        Assert.Null(dto.LastMessageAtUtc);
    }
}