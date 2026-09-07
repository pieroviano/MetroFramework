using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using MetroFramework.Drawing.Html;

namespace MetroFramework.Tests;

public class CssValueParseNumberTests
{
    [Theory]
    [InlineData("5", 5f)]
    [InlineData("7.5", 7.5f)]
    [InlineData("0", 0f)]
    [InlineData("-3", -3f)]
    public void PlainNumbersAreReturnedAsIs(string input, float expected)
    {
        Assert.Equal(expected, CssValue.ParseNumber(input, 100f));
    }

    [Theory]
    [InlineData("50%", 200f, 100f)]
    [InlineData("100%", 200f, 200f)]
    [InlineData("0%", 200f, 0f)]
    [InlineData("12.5%", 800f, 100f)]
    public void PercentagesAreResolvedAgainstTheHundredPercentValue(string input, float hundred, float expected)
    {
        Assert.Equal(expected, CssValue.ParseNumber(input, hundred));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("not-a-number")]
    [InlineData("12px")]
    public void UnparseableInputIsZero(string input)
    {
        Assert.Equal(0f, CssValue.ParseNumber(input, 100f));
    }

    [Fact]
    public void ParsingIsCultureInvariant()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("it-IT");
            Assert.Equal(7.5f, CssValue.ParseNumber("7.5", 100f));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}

public class CssValueGetActualColorTests
{
    [Theory]
    [InlineData("#ff0000", 255, 0, 0)]
    [InlineData("#00FF00", 0, 255, 0)]
    [InlineData("#0000ff", 0, 0, 255)]
    [InlineData("#123456", 0x12, 0x34, 0x56)]
    public void ParsesSixDigitHex(string input, int r, int g, int b)
    {
        Assert.Equal(Color.FromArgb(r, g, b), CssValue.GetActualColor(input));
    }

    [Theory]
    [InlineData("#f00", 255, 0, 0)]
    [InlineData("#0f0", 0, 255, 0)]
    [InlineData("#abc", 0xaa, 0xbb, 0xcc)]
    public void ParsesThreeDigitHexByDoublingEachDigit(string input, int r, int g, int b)
    {
        Assert.Equal(Color.FromArgb(r, g, b), CssValue.GetActualColor(input));
    }

    [Theory]
    [InlineData("rgb(255,0,0)", 255, 0, 0)]
    [InlineData("rgb( 10 , 20 , 30 )", 10, 20, 30)]
    [InlineData("rgb(100%, 0, 0)", 255, 0, 0)]
    [InlineData("rgb(0%, 50%, 100%)", 0, 128, 255)]
    public void ParsesRgbFunctionalNotation(string input, int r, int g, int b)
    {
        Color color = CssValue.GetActualColor(input);

        Assert.Equal(r, color.R);
        Assert.Equal(g, color.G);
        Assert.Equal(b, color.B);
    }

    [Theory]
    [InlineData("red", 0xff, 0x00, 0x00)]
    [InlineData("RED", 0xff, 0x00, 0x00)]
    [InlineData("  white  ", 0xff, 0xff, 0xff)]
    [InlineData("black", 0, 0, 0)]
    [InlineData("maroon", 0x80, 0x00, 0x00)]
    [InlineData("olive", 0x80, 0x80, 0x00)]
    [InlineData("purple", 0x80, 0x00, 0x80)]
    [InlineData("fuchsia", 0xff, 0x00, 0xff)]
    [InlineData("lime", 0x00, 0xff, 0x00)]
    [InlineData("green", 0x00, 0x80, 0x00)]
    [InlineData("navy", 0x00, 0x00, 0x80)]
    [InlineData("blue", 0x00, 0x00, 0xff)]
    [InlineData("aqua", 0x00, 0xff, 0xff)]
    [InlineData("teal", 0x00, 0x80, 0x80)]
    [InlineData("silver", 0xc0, 0xc0, 0xc0)]
    [InlineData("gray", 0x80, 0x80, 0x80)]
    [InlineData("yellow", 0xff, 0xff, 0x00)]
    [InlineData("orange", 0xff, 0xa5, 0x00)]
    public void ParsesTheCssColorKeywordsCaseInsensitively(string input, int r, int g, int b)
    {
        Assert.Equal(Color.FromArgb(r, g, b), CssValue.GetActualColor(input));
    }

    /// <summary>
    /// A stylesheet is untrusted input. Anything the parser does not understand has to
    /// come back as <see cref="Color.Empty"/> so the caller can fall back to its own
    /// default; it must never take the whole render down.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("notacolour")]
    [InlineData("#12345")]          // wrong digit count
    [InlineData("#1234567")]
    [InlineData("#zzzzzz")]         // right shape, not hexadecimal
    [InlineData("#xyz")]
    [InlineData("rgb(1,2)")]        // too few components
    [InlineData("rgb(1,2,3,4)")]    // too many components
    public void MalformedInputReturnsColorEmptyRatherThanThrowing(string input)
    {
        Assert.Equal(Color.Empty, CssValue.GetActualColor(input));
    }

    /// <summary>
    /// CSS 2.1 requires out-of-range rgb() components to be clipped to the device gamut.
    /// </summary>
    [Theory]
    [InlineData("rgb(300,0,0)", 255, 0, 0)]
    [InlineData("rgb(-20,0,0)", 0, 0, 0)]
    [InlineData("rgb(0,0,999)", 0, 0, 255)]
    [InlineData("rgb(150%, 0, 0)", 255, 0, 0)]
    public void OutOfRangeRgbComponentsAreClamped(string input, int r, int g, int b)
    {
        Color color = CssValue.GetActualColor(input);

        Assert.Equal(r, color.R);
        Assert.Equal(g, color.G);
        Assert.Equal(b, color.B);
    }
}

public class CssValueSplitValuesTests
{
    [Fact]
    public void SplitsOnSpacesAndTrims()
    {
        Assert.Equal(new[] { "5", "4", "3", "inherit" }, CssValue.SplitValues("5  4   3 inherit"));
    }

    [Fact]
    public void SplitsOnAnExplicitSeparator()
    {
        Assert.Equal(new[] { "Arial", "Helvetica" }, CssValue.SplitValues("Arial , Helvetica", ','));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void EmptyInputYieldsAnEmptyArray(string input)
    {
        Assert.Empty(CssValue.SplitValues(input));
    }
}

public class CssValueResourceLoadingTests
{
    /// <summary>
    /// The path forms accepted by GetImage / GetStyleSheet ("method:", "property:",
    /// a URI, or a file) all come from stylesheet text, so an unresolvable one has to
    /// degrade rather than throw.
    /// </summary>
    [Theory]
    [InlineData("method:MetroFramework.Drawing.Html.HtmlRenderer.NoSuchMethod")]
    [InlineData("property:MetroFramework.Drawing.Html.HtmlRenderer.NoSuchProperty")]
    [InlineData("method:NoSuch.Type.Member")]
    [InlineData("property:NoSuch.Type.Member")]
    public void GetImage_ReturnsNullForAnUnresolvableSource(string path)
    {
        Assert.Null(CssValue.GetImage(path));
    }

    [Fact]
    public void GetImage_ReturnsNullForAMissingFile()
    {
        string missing = Path.Combine(Path.GetTempPath(), "metroframework-missing-" + Guid.NewGuid() + ".png");

        Assert.Null(CssValue.GetImage(missing));
    }

    /// <summary>
    /// A relative reference such as <c>&lt;img src="logo.png"&gt;</c> is the ordinary
    /// way to write an image path. It is a well formed *relative* URI, which is not
    /// something a Uri instance can be built from, so it has to be read as a file path.
    /// </summary>
    [Theory]
    [InlineData("logo.png")]
    [InlineData("images/logo.png")]
    [InlineData("./logo.png")]
    [InlineData("../logo.png")]
    public void GetImage_TreatsARelativeReferenceAsAFilePath(string path)
    {
        Assert.Null(CssValue.GetImage(path));
    }

    [Fact]
    public void GetImage_LoadsAnImageFromAFile()
    {
        string file = Path.Combine(Path.GetTempPath(), "metroframework-test-" + Guid.NewGuid() + ".png");
        using (var source = new System.Drawing.Bitmap(8, 6))
        {
            source.Save(file, System.Drawing.Imaging.ImageFormat.Png);
        }

        try
        {
            using System.Drawing.Image loaded = CssValue.GetImage(file);

            Assert.NotNull(loaded);
            Assert.Equal(8, loaded.Width);
            Assert.Equal(6, loaded.Height);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Theory]
    [InlineData("logo.png")]
    [InlineData("images/site.css")]
    public void GetStyleSheet_TreatsARelativeReferenceAsAFilePath(string path)
    {
        Assert.Null(CssValue.GetStyleSheet(path));
    }

    [Theory]
    [InlineData("method:MetroFramework.Drawing.Html.HtmlRenderer.NoSuchMethod")]
    [InlineData("property:MetroFramework.Drawing.Html.HtmlRenderer.NoSuchProperty")]
    public void GetStyleSheet_ReturnsNoContentForAnUnresolvableSource(string path)
    {
        Assert.True(string.IsNullOrEmpty(CssValue.GetStyleSheet(path)));
    }

    [Fact]
    public void GetStyleSheet_ReadsAFileFromDisk()
    {
        string file = Path.Combine(Path.GetTempPath(), "metroframework-test-" + Guid.NewGuid() + ".css");
        File.WriteAllText(file, "p { color: red; }");
        try
        {
            Assert.Equal("p { color: red; }", CssValue.GetStyleSheet(file));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void GetStyleSheet_ReturnsNullForAMissingFile()
    {
        string missing = Path.Combine(Path.GetTempPath(), "metroframework-missing-" + Guid.NewGuid() + ".css");

        Assert.Null(CssValue.GetStyleSheet(missing));
    }
}
