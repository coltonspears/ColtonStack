using System.Globalization;
using ColtonStack.Client.Converters;
using Xunit;

namespace ColtonStack.Tests;

/// <summary>
/// Tests for the two-value EnumEqualsConverter used to bind RadioButton to an enum property.
/// Pure conversion logic — instantiate once, assert both directions.
/// </summary>
public sealed class EnumEqualsConverterTests
{
    public enum SampleKind { Channels, People, Settings }

    private readonly EnumEqualsConverter _converter = new();

    [Theory]
    [InlineData(SampleKind.Channels, "Channels", true)]
    [InlineData(SampleKind.People, "Channels", false)]
    [InlineData(SampleKind.People, "People", true)]
    [InlineData(SampleKind.Channels, "People", false)]
    public void Convert_ComparesEnumToParameter(SampleKind value, string parameter, bool expected)
    {
        var result = _converter.Convert(value, typeof(bool), parameter, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Convert_ReturnsFalse_WhenParameterIsNull()
    {
        var result = _converter.Convert(SampleKind.Channels, typeof(bool), null, CultureInfo.InvariantCulture);
        Assert.False((bool)result);
    }

    [Fact]
    public void Convert_ReturnsFalse_WhenParameterIsEmpty()
    {
        var result = _converter.Convert(SampleKind.Channels, typeof(bool), "", CultureInfo.InvariantCulture);
        Assert.False((bool)result);
    }

    [Fact]
    public void ConvertBack_WhenTrue_WritesParameterEnum()
    {
        var result = _converter.ConvertBack(true, typeof(SampleKind), "People", CultureInfo.InvariantCulture);
        Assert.Equal(SampleKind.People, result);
    }

    [Fact]
    public void ConvertBack_WhenFalse_ReturnsDoNothing()
    {
        var result = _converter.ConvertBack(false, typeof(SampleKind), "People", CultureInfo.InvariantCulture);
        Assert.Equal(System.Windows.Data.Binding.DoNothing, result);
    }

    [Fact]
    public void ConvertBack_WithInvalidEnumName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            _converter.ConvertBack(true, typeof(SampleKind), "NonExistent", CultureInfo.InvariantCulture));
    }
}