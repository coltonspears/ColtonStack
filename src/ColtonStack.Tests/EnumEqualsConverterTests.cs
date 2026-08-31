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
    private enum SampleEnum { Channels, People, Settings }

    private readonly EnumEqualsConverter _converter = new();

    [Theory]
    [InlineData(SampleEnum.Channels, "Channels", true)]
    [InlineData(SampleEnum.People, "Channels", false)]
    [InlineData(SampleEnum.People, "People", true)]
    [InlineData(SampleEnum.Channels, "People", false)]
    public void Convert_ComparesEnumToParameter(SampleEnum value, string parameter, bool expected)
    {
        var result = _converter.Convert(value, typeof(bool), parameter, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Convert_ReturnsFalse_WhenParameterIsNull()
    {
        var result = _converter.Convert(SampleEnum.Channels, typeof(bool), null, CultureInfo.InvariantCulture);
        Assert.False((bool)result);
    }

    [Fact]
    public void Convert_ReturnsFalse_WhenParameterIsEmpty()
    {
        var result = _converter.Convert(SampleEnum.Channels, typeof(bool), "", CultureInfo.InvariantCulture);
        Assert.False((bool)result);
    }

    [Fact]
    public void ConvertBack_WhenTrue_WritesParameterEnum()
    {
        var result = _converter.ConvertBack(true, typeof(SampleEnum), "People", CultureInfo.InvariantCulture);
        Assert.Equal(SampleEnum.People, result);
    }

    [Fact]
    public void ConvertBack_WhenFalse_ReturnsDoNothing()
    {
        var result = _converter.ConvertBack(false, typeof(SampleEnum), "People", CultureInfo.InvariantCulture);
        Assert.Equal(System.Windows.Data.Binding.DoNothing, result);
    }

    [Fact]
    public void ConvertBack_WithInvalidEnumName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            _converter.ConvertBack(true, typeof(SampleEnum), "NonExistent", CultureInfo.InvariantCulture));
    }
}