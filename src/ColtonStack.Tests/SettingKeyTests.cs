using ColtonStack.Contracts;
using Xunit;

namespace ColtonStack.Tests;

/// <summary>One key grammar, validated identically on the client (before the request) and the server (before the row).</summary>
public sealed class SettingKeyTests
{
    [Theory]
    [InlineData("pokemon.artwork")]
    [InlineData("audit.pagesize")]
    [InlineData("a")]
    [InlineData("a1.b2.c3")]
    public void IsValid_AcceptsDottedLowerCaseSegments(string key) =>
        Assert.True(SettingKey.IsValid(key));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("pokemon.")]
    [InlineData(".artwork")]
    [InlineData("pokemon..artwork")]
    [InlineData("Pokemon.artwork")]
    [InlineData("pokemon.art work")]
    [InlineData("1pokemon")]
    [InlineData("pokemon-artwork")]
    [InlineData("pokémon.artwork")]
    public void IsValid_RejectsEverythingElse(string? key) =>
        Assert.False(SettingKey.IsValid(key));

    [Fact]
    public void IsValid_RejectsOverlongKeys()
    {
        var atLimit = new string('a', SettingKey.MaxLength);
        Assert.True(SettingKey.IsValid(atLimit));
        Assert.False(SettingKey.IsValid(atLimit + "a"));
    }
}
