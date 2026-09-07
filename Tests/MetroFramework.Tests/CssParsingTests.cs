using System;
using System.Linq;
using System.Text.RegularExpressions;
using MetroFramework.Drawing.Html;

namespace MetroFramework.Tests;

public class CssBlockTests
{
    [Fact]
    public void ReadsPropertyNamesAndValues()
    {
        var block = new CssBlock("color: red; font-size: 12px;");

        Assert.Equal("red", block.Properties["color"]);
        Assert.Equal("12px", block.Properties["font-size"]);
    }

    [Fact]
    public void KeepsTheOriginalSource()
    {
        const string source = "color: red;";

        Assert.Equal(source, new CssBlock(source).BlockSource);
    }

    [Fact]
    public void TrimsWhitespaceAroundNamesAndValues()
    {
        var block = new CssBlock("  color  :   red  ;");

        Assert.Equal("red", block.Properties["color"]);
    }

    [Fact]
    public void RecognisesPropertiesThatMapOntoCssBox()
    {
        var block = new CssBlock("color: red;");

        Assert.Contains(block.PropertyValues, pair => pair.Key.Name == "Color" && pair.Value == "red");
    }

    [Fact]
    public void IgnoresPropertiesThatDoNotMapOntoCssBox()
    {
        var block = new CssBlock("-moz-invented-thing: 4;");

        Assert.True(block.Properties.ContainsKey("-moz-invented-thing"));
        Assert.Empty(block.PropertyValues);
    }

    /// <summary>
    /// CSS 2.1 cascading order: within one declaration block the last declaration of a
    /// property wins. A repeated property is legal input, not an error.
    /// </summary>
    [Fact]
    public void LastDeclarationOfARepeatedPropertyWins()
    {
        var block = new CssBlock("color: red; color: blue;");

        Assert.Equal("blue", block.Properties["color"]);
    }

    [Fact]
    public void RepeatedPropertyAlsoWinsInThePropertyValuesMap()
    {
        var block = new CssBlock("color: red; color: blue;");

        Assert.Equal("blue", block.PropertyValues.Single(pair => pair.Key.Name == "Color").Value);
    }

    [Fact]
    public void EmptyBlockHasNoProperties()
    {
        Assert.Empty(new CssBlock(string.Empty).Properties);
    }

    [Fact]
    public void AssignTo_WritesTheValuesOntoABox()
    {
        var block = new CssBlock("color: red;");
        var box = new CssBox(null);

        block.AssignTo(box);

        Assert.Equal("red", box.Color);
    }

    [Fact]
    public void UpdatePropertyValues_RebuildsTheMapFromProperties()
    {
        var block = new CssBlock("color: red;");
        block.Properties["color"] = "green";

        block.UpdatePropertyValues();

        Assert.Equal("green", block.PropertyValues.Single(pair => pair.Key.Name == "Color").Value);
    }
}

/// <summary>
/// The regular expressions in <see cref="Parser"/> are the front door of the CSS
/// reader. Each one is exercised against the examples in its own documentation
/// comment.
/// </summary>
public class ParserTests
{
    private static bool FullyMatches(string pattern, string input)
    {
        Match match = Regex.Match(input, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success && match.Value == input;
    }

    [Theory]
    [InlineData("9px")]
    [InlineData("3pt")]
    [InlineData(".89em")]
    [InlineData("10ex")]
    [InlineData("1in")]
    [InlineData("2cm")]
    [InlineData("3mm")]
    [InlineData("4pc")]
    public void CssLength_MatchesTheDocumentedExamples(string input)
    {
        Assert.True(FullyMatches(Parser.CssLength, input));
    }

    [Theory]
    [InlineData("9")]
    [InlineData("9zz")]
    [InlineData("px")]
    public void CssLength_RejectsNonLengths(string input)
    {
        Assert.False(FullyMatches(Parser.CssLength, input));
    }

    [Theory]
    [InlineData("100%")]
    [InlineData(".5%")]
    [InlineData("5.4%")]
    public void CssPercentage_MatchesTheDocumentedExamples(string input)
    {
        Assert.True(FullyMatches(Parser.CssPercentage, input));
    }

    /// <summary>
    /// The documentation comment promises "5, 6, 7.5, 0.9". Braces are a repetition
    /// operator in a regular expression, not a grouping construct, so a number pattern
    /// written with braces would match literal brace characters instead.
    /// </summary>
    [Theory]
    [InlineData("5")]
    [InlineData("6")]
    [InlineData("7.5")]
    [InlineData("0.9")]
    [InlineData(".9")]
    public void CssNumber_MatchesTheDocumentedExamples(string input)
    {
        Assert.True(FullyMatches(Parser.CssNumber, input));
    }

    [Theory]
    [InlineData("{5}")]
    [InlineData("abc")]
    public void CssNumber_RejectsNonNumbers(string input)
    {
        Assert.False(FullyMatches(Parser.CssNumber, input));
    }

    [Theory]
    [InlineData("normal")]
    [InlineData("1.5")]
    [InlineData("12px")]
    [InlineData("120%")]
    public void CssLineHeight_AcceptsEveryFormItDocuments(string input)
    {
        Assert.True(FullyMatches(Parser.CssLineHeight, input));
    }

    [Theory]
    [InlineData("black")]
    [InlineData("white")]
    [InlineData("#fff")]
    [InlineData("#fe98cd")]
    [InlineData("rgb(5,5,5)")]
    [InlineData("rgb(45%, 0, 0)")]
    public void CssColors_MatchesTheDocumentedExamples(string input)
    {
        Assert.True(FullyMatches(Parser.CssColors, input));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("dotted")]
    [InlineData("solid")]
    [InlineData("double")]
    public void CssBorderStyle_MatchesTheDocumentedExamples(string input)
    {
        Assert.True(FullyMatches(Parser.CssBorderStyle, input));
    }

    [Theory]
    [InlineData("1px")]
    [InlineData("thin")]
    [InlineData("medium")]
    [InlineData("thick")]
    [InlineData("3em")]
    public void CssBorderWidth_MatchesTheDocumentedExamples(string input)
    {
        Assert.True(FullyMatches(Parser.CssBorderWidth, input));
    }

    [Theory]
    [InlineData("xx-small")]
    [InlineData("larger")]
    [InlineData("small")]
    [InlineData("34pt")]
    [InlineData("30%")]
    [InlineData("2em")]
    public void CssFontSize_MatchesTheDocumentedExamples(string input)
    {
        Assert.True(FullyMatches(Parser.CssFontSize, input));
    }

    [Theory]
    [InlineData("normal")]
    [InlineData("italic")]
    [InlineData("oblique")]
    public void CssFontStyle_MatchesTheDocumentedExamples(string input)
    {
        Assert.True(FullyMatches(Parser.CssFontStyle, input));
    }

    [Theory]
    [InlineData("normal")]
    [InlineData("bold")]
    [InlineData("bolder")]
    [InlineData("lighter")]
    [InlineData("700")]
    public void CssFontWeight_MatchesTheDocumentedExamples(string input)
    {
        Assert.True(FullyMatches(Parser.CssFontWeight, input));
    }

    [Fact]
    public void CssComments_MatchesABlockComment()
    {
        Assert.True(FullyMatches(Parser.CssComments, "/* comment */"));
    }

    [Fact]
    public void HtmlTag_MatchesATag()
    {
        Assert.True(FullyMatches(Parser.HtmlTag, "<p class=\"x\">"));
        Assert.True(FullyMatches(Parser.HtmlTag, "</p>"));
    }

    [Fact]
    public void Match_FindsEveryOccurrence()
    {
        MatchCollection matches = Parser.Match(Parser.CssLength, "margin: 3px 4px 5px 6px");

        Assert.Equal(4, matches.Count);
    }

    [Fact]
    public void Search_ReturnsTheFirstMatchAndItsPosition()
    {
        string found = Parser.Search(Parser.CssLength, "margin: 3px 4px", out int position);

        Assert.Equal("3px", found);
        Assert.Equal(8, position);
    }

    [Fact]
    public void Search_ReportsNoMatchWithANegativePosition()
    {
        string found = Parser.Search(Parser.CssLength, "margin: inherit", out int position);

        Assert.Null(found);
        Assert.Equal(-1, position);
    }
}

public class HtmlTagTests
{
    [Fact]
    public void ReadsTheTagName()
    {
        Assert.Equal("p", new HtmlTag("<p>").TagName);
    }

    [Fact]
    public void LowercasesTheTagName()
    {
        Assert.Equal("div", new HtmlTag("<DIV>").TagName);
    }

    [Fact]
    public void RecognisesAClosingTag()
    {
        var tag = new HtmlTag("</p>");

        Assert.True(tag.IsClosing);
        Assert.Equal("p", tag.TagName);
    }

    [Fact]
    public void AnOpeningTagIsNotClosing()
    {
        Assert.False(new HtmlTag("<p>").IsClosing);
    }

    [Fact]
    public void ReadsQuotedAttributes()
    {
        var tag = new HtmlTag("<td class=\"header cell\" width=\"50%\">");

        Assert.Equal("header cell", tag.Attributes["class"]);
        Assert.Equal("50%", tag.Attributes["width"]);
    }

    [Fact]
    public void ReadsUnquotedAttributes()
    {
        var tag = new HtmlTag("<table border=0 cellspacing=5>");

        Assert.Equal("0", tag.Attributes["border"]);
        Assert.Equal("5", tag.Attributes["cellspacing"]);
    }

    [Fact]
    public void IsSingle_IsTrueForVoidElements()
    {
        Assert.True(new HtmlTag("<br>").IsSingle);
        Assert.True(new HtmlTag("<hr>").IsSingle);
        Assert.True(new HtmlTag("<img src=x>").IsSingle);
    }

    [Fact]
    public void IsSingle_IsFalseForContainerElements()
    {
        Assert.False(new HtmlTag("<p>").IsSingle);
        Assert.False(new HtmlTag("<div>").IsSingle);
    }
}

public class CssRectangleTests
{
    [Fact]
    public void RightAndBottomAreDerivedFromTheOrigin()
    {
        var rectangle = new CssRectangle { Left = 10, Top = 20, Width = 30, Height = 40 };

        Assert.Equal(40f, rectangle.Right);
        Assert.Equal(60f, rectangle.Bottom);
    }

    [Fact]
    public void SettingRightOnlyChangesTheWidth()
    {
        var rectangle = new CssRectangle { Left = 10, Width = 30 };

        rectangle.Right = 100;

        Assert.Equal(10f, rectangle.Left);
        Assert.Equal(90f, rectangle.Width);
    }

    [Fact]
    public void SettingBottomOnlyChangesTheHeight()
    {
        var rectangle = new CssRectangle { Top = 10, Height = 30 };

        rectangle.Bottom = 100;

        Assert.Equal(10f, rectangle.Top);
        Assert.Equal(90f, rectangle.Height);
    }

    [Fact]
    public void BoundsRoundTrips()
    {
        var rectangle = new CssRectangle
        {
            Bounds = new System.Drawing.RectangleF(1, 2, 3, 4)
        };

        Assert.Equal(new System.Drawing.RectangleF(1, 2, 3, 4), rectangle.Bounds);
        Assert.Equal(new System.Drawing.PointF(1, 2), rectangle.Location);
        Assert.Equal(new System.Drawing.SizeF(3, 4), rectangle.Size);
    }

    [Fact]
    public void LocationAndSizeWriteThroughToBounds()
    {
        var rectangle = new CssRectangle
        {
            Location = new System.Drawing.PointF(5, 6),
            Size = new System.Drawing.SizeF(7, 8)
        };

        Assert.Equal(new System.Drawing.RectangleF(5, 6, 7, 8), rectangle.Bounds);
    }
}
