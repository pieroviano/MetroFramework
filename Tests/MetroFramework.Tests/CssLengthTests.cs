using System;
using System.Globalization;
using MetroFramework.Drawing.Html;

namespace MetroFramework.Tests;

/// <summary>
/// <see cref="CssLength"/> parses the CSS length grammar
/// (http://www.w3.org/TR/CSS21/syndata.html#length-units).
/// </summary>
public class CssLengthTests
{
    [Theory]
    [InlineData("10px", 10f, CssLength.CssUnit.Pixels, true)]
    [InlineData("3.5em", 3.5f, CssLength.CssUnit.Ems, true)]
    [InlineData("2ex", 2f, CssLength.CssUnit.Ex, true)]
    [InlineData("12pt", 12f, CssLength.CssUnit.Points, false)]
    [InlineData("1in", 1f, CssLength.CssUnit.Inches, false)]
    [InlineData("5cm", 5f, CssLength.CssUnit.Centimeters, false)]
    [InlineData("40mm", 40f, CssLength.CssUnit.Milimeters, false)]
    [InlineData("6pc", 6f, CssLength.CssUnit.Picas, false)]
    public void ParsesNumberAndUnit(string input, float number, CssLength.CssUnit unit, bool relative)
    {
        var length = new CssLength(input);

        Assert.False(length.HasError);
        Assert.Equal(number, length.Number);
        Assert.Equal(unit, length.Unit);
        Assert.Equal(relative, length.IsRelative);
        Assert.False(length.IsPercentage);
        Assert.Equal(input, length.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("0")]
    public void EmptyOrZero_IsAValidZeroLength(string input)
    {
        var length = new CssLength(input);

        Assert.False(length.HasError);
        Assert.Equal(0f, length.Number);
        Assert.Equal(CssLength.CssUnit.None, length.Unit);
    }

    [Theory]
    [InlineData("10")]      // no unit
    [InlineData("10zz")]    // unknown unit
    [InlineData("abpx")]    // unit present, number is not a number
    public void UnparseableInput_SetsHasError(string input)
    {
        Assert.True(new CssLength(input).HasError);
    }

    [Fact]
    public void ErroredLength_RendersAsEmptyString()
    {
        Assert.Equal(string.Empty, new CssLength("10zz").ToString());
    }

    [Theory]
    [InlineData("50%", 50f)]
    [InlineData("100%", 100f)]
    [InlineData("0.5%", 0.5f)]
    [InlineData("12.5%", 12.5f)]
    public void Percentage_ExposesTheNumberAsWritten(string input, float expected)
    {
        var length = new CssLength(input);

        Assert.True(length.IsPercentage);
        Assert.False(length.HasError);
        Assert.Equal(expected, length.Number);
    }

    /// <summary>
    /// Callers such as CssTable gate on <c>Number &gt; 0</c> before deciding whether a
    /// width was specified at all, so a non-zero percentage must never read as zero.
    /// </summary>
    [Theory]
    [InlineData("1%")]
    [InlineData("0.5%")]
    [InlineData("100%")]
    public void NonZeroPercentage_ReadsAsSpecified(string input)
    {
        Assert.True(new CssLength(input).Number > 0f);
    }

    [Theory]
    [InlineData("10px")]
    [InlineData("3.5em")]
    [InlineData("12pt")]
    [InlineData("50%")]
    [InlineData("0.5%")]
    public void ToString_RoundTripsThroughTheParser(string input)
    {
        var original = new CssLength(input);
        var reparsed = new CssLength(original.ToString());

        Assert.Equal(original.Number, reparsed.Number);
        Assert.Equal(original.Unit, reparsed.Unit);
        Assert.Equal(original.IsPercentage, reparsed.IsPercentage);
    }

    [Fact]
    public void ToString_IsCultureInvariant()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            // A culture that writes decimals with a comma would otherwise emit "3,5em".
            CultureInfo.CurrentCulture = new CultureInfo("it-IT");
            Assert.Equal("3.5em", new CssLength("3.5em").ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Parsing_IsCultureInvariant()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("it-IT");
            var length = new CssLength("3.5em");

            Assert.False(length.HasError);
            Assert.Equal(3.5f, length.Number);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void ConvertEmToPoints_ScalesByTheEmSize()
    {
        CssLength points = new CssLength("2em").ConvertEmToPoints(10f);

        Assert.Equal(CssLength.CssUnit.Points, points.Unit);
        Assert.Equal(20f, points.Number);
    }

    [Fact]
    public void ConvertEmToPixels_ScalesByThePixelFactor()
    {
        CssLength pixels = new CssLength("2em").ConvertEmToPixels(8f);

        Assert.Equal(CssLength.CssUnit.Pixels, pixels.Unit);
        Assert.Equal(16f, pixels.Number);
    }

    [Fact]
    public void ConvertEm_RejectsALengthThatIsNotInEms()
    {
        Assert.Throws<InvalidOperationException>(() => new CssLength("10px").ConvertEmToPoints(2f));
        Assert.Throws<InvalidOperationException>(() => new CssLength("10px").ConvertEmToPixels(2f));
    }

    [Fact]
    public void ConvertEm_RejectsAnErroredLength()
    {
        Assert.Throws<InvalidOperationException>(() => new CssLength("10zz").ConvertEmToPoints(2f));
    }
}
