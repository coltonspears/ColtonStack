using System.Globalization;
using ColtonStack.Client.ViewModels;
using ColtonStack.Contracts;
using Xunit;

namespace ColtonStack.Tests;

/// <summary>
/// MessageViewModel wraps a DTO in an immutable presentation layer. Every field is a pure
/// projection — no state, no notifications, no base class. These tests prove it by asserting
/// every property value without any setup beyond constructing the DTO.
/// </summary>
public sealed class MessageViewModelTests
{
    private static readonly DateTimeOffset SampleUtc = new(2025, 6, 15, 14, 30, 0, TimeSpan.Zero);

    private static MessageDto SampleDto => new(
        Id: 42,
        ChannelId: 7,
        UserId: 3,
        AuthorName: "Devon Park",
        AuthorColor: "#2EB67D",
        Text: "Hello *world*",
        CreatedAtUtc: SampleUtc);

    [Fact]
    public void Constructor_ProjectsDtoProperties()
    {
        var vm = new MessageViewModel(SampleDto, isFirstOfGroup: true);

        Assert.Equal(42, vm.Id);
        Assert.Equal("Devon Park", vm.AuthorName);
        Assert.Equal("#2EB67D", vm.AvatarColor);
        Assert.Equal("Hello *world*", vm.Text);
        Assert.Equal(SampleUtc, vm.CreatedAtUtc);
    }

    [Fact]
    public void Constructor_IsFirstOfGroup_Stored()
    {
        var first = new MessageViewModel(SampleDto, isFirstOfGroup: true);
        var notFirst = new MessageViewModel(SampleDto, isFirstOfGroup: false);

        Assert.True(first.IsFirstOfGroup);
        Assert.False(notFirst.IsFirstOfGroup);
    }

    [Fact]
    public void Constructor_Initials_ExtractsFromAuthorName()
    {
        var vm = new MessageViewModel(SampleDto, isFirstOfGroup: true);
        Assert.Equal("DP", vm.Initials);
    }

    [Fact]
    public void Constructor_TimeText_LocalTime()
    {
        var vm = new MessageViewModel(SampleDto, isFirstOfGroup: true);
        var local = SampleUtc.ToLocalTime();
        var expected = local.ToString("t", CultureInfo.CurrentCulture);
        Assert.Equal(expected, vm.TimeText);
    }

    [Fact]
    public void Constructor_IsImmutable()
    {
        // All properties are { get; } — once constructed, they never change.
        // This test proves there is no settable property (by checking the type shape).
        var vm = new MessageViewModel(SampleDto, isFirstOfGroup: true);
        var type = vm.GetType();

        Assert.All(type.GetProperties(), prop =>
        {
            // The C# compiler emits no setter for init-only auto-props on { get; }
            Assert.Null(prop.SetMethod);
        });
    }

    [Fact]
    public void ViewModel_IsNotObservableObject()
    {
        // Contrast with legacy: MessageViewModel has zero change notification baggage.
        // It's a plain object that projects once and is discarded.
        var vm = new MessageViewModel(SampleDto, isFirstOfGroup: true);
        Assert.False(vm is System.ComponentModel.INotifyPropertyChanged);
    }
}