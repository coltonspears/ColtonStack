using ColtonStack.Client.ViewModels;
using Xunit;

namespace ColtonStack.Tests;

/// <summary>
/// NameInitials is the simplest possible unit test subject: no DI, no state, no base class.
/// Every test constructs nothing — just calls a static method and asserts the result.
/// </summary>
public sealed class NameInitialsTests
{
    [Theory]
    [InlineData("Devon Park", "DP")]
    [InlineData("Maya Chen", "MC")]
    [InlineData("colton", "C")]
    [InlineData("  Riley   Fox  ", "RF")]
    [InlineData("Jean-Claude Van Damme", "JV")]
    [InlineData("", "")]
    [InlineData("A", "A")]
    public void From_ExtractsInitials(string displayName, string expected) =>
        Assert.Equal(expected, NameInitials.From(displayName));

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void From_BlankName_ReturnsEmpty(string? displayName)
    {
        // Split on whitespace with RemoveEmptyEntries → empty sequence → string.Concat("") → ""
        Assert.Equal("", NameInitials.From(displayName ?? ""));
    }
}