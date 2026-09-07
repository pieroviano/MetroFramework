using MetroFramework.Drawing.Html;

namespace MetroFramework.Tests;

/// <summary>
/// The <c>font</c> shorthand is the one place where nearly every pattern in
/// <see cref="Parser"/> is used in anger
/// (http://www.w3.org/TR/CSS21/fonts.html#font-shorthand). These tests drive it
/// end to end so that a regression in the regular expressions shows up as a wrong
/// style rather than as a silently dropped component.
/// </summary>
public class CssBoxFontShorthandTests
{
    private static CssBox Parse(string shorthand)
    {
        var box = new CssBox(null);
        box.Font = shorthand;
        return box;
    }

    [Fact]
    public void SizeAndFamily()
    {
        CssBox box = Parse("12px arial");

        Assert.Equal("12px", box.FontSize);
        Assert.Equal("arial", box.FontFamily);
    }

    [Fact]
    public void SizeAndLineHeightAreSplitOnTheSlash()
    {
        CssBox box = Parse("12px/14px arial");

        Assert.Equal("12px", box.FontSize);
        Assert.Equal("14px", box.LineHeight);
        Assert.Equal("arial", box.FontFamily);
    }

    /// <summary>
    /// A unitless line-height is a multiplier, and the commonest way to write one.
    /// </summary>
    [Fact]
    public void UnitlessLineHeightIsKept()
    {
        CssBox box = Parse("12px/1.5 arial");

        Assert.Equal("12px", box.FontSize);
        Assert.Equal("1.5", box.LineHeight);
    }

    [Fact]
    public void PercentageLineHeightIsKept()
    {
        CssBox box = Parse("12px/150% arial");

        Assert.Equal("150%", box.LineHeight);
    }

    [Theory]
    [InlineData("normal")]
    [InlineData("bold")]
    [InlineData("bolder")]
    [InlineData("lighter")]
    [InlineData("700")]
    public void WeightIsReadWholeFromTheLeftHandSide(string weight)
    {
        CssBox box = Parse(weight + " 12px arial");

        Assert.Equal(weight, box.FontWeight);
    }

    [Theory]
    [InlineData("italic")]
    [InlineData("oblique")]
    public void StyleIsReadFromTheLeftHandSide(string style)
    {
        CssBox box = Parse(style + " 12px arial");

        Assert.Equal(style, box.FontStyle);
    }

    [Fact]
    public void VariantIsReadFromTheLeftHandSide()
    {
        Assert.Equal("small-caps", Parse("small-caps 12px arial").FontVariant);
    }

    [Fact]
    public void StyleVariantAndWeightCanAllAppear()
    {
        CssBox box = Parse("italic small-caps bold 12px/1.5 arial");

        Assert.Equal("italic", box.FontStyle);
        Assert.Equal("small-caps", box.FontVariant);
        Assert.Equal("bold", box.FontWeight);
        Assert.Equal("12px", box.FontSize);
        Assert.Equal("1.5", box.LineHeight);
        Assert.Equal("arial", box.FontFamily);
    }

    [Theory]
    [InlineData("xx-small")]
    [InlineData("x-small")]
    [InlineData("small")]
    [InlineData("medium")]
    [InlineData("large")]
    [InlineData("x-large")]
    [InlineData("xx-large")]
    [InlineData("larger")]
    [InlineData("smaller")]
    public void KeywordSizesAreReadWhole(string size)
    {
        CssBox box = Parse(size + " arial");

        Assert.Equal(size, box.FontSize);
    }

    [Fact]
    public void AnUnrecognisedShorthandLeavesTheDefaultsAlone()
    {
        var box = new CssBox(null);
        string defaultSize = box.FontSize;
        string defaultFamily = box.FontFamily;

        box.Font = "caption";

        Assert.Equal(defaultSize, box.FontSize);
        Assert.Equal(defaultFamily, box.FontFamily);
    }
}

/// <summary>
/// A style sheet is read into <see cref="CssBlock"/>s and cascaded onto boxes;
/// these cover that path with the whitespace and repetition real style sheets use.
/// </summary>
public class CssCascadeTests
{
    [Fact]
    public void WhitespaceAroundTheColonIsAllowed()
    {
        var box = new CssBox(null);

        new CssBlock("color : red ; font-size : 14px ;").AssignTo(box);

        Assert.Equal("red", box.Color);
        Assert.Equal("14px", box.FontSize);
    }

    [Fact]
    public void ARepeatedDeclarationOverridesTheEarlierOne()
    {
        var box = new CssBox(null);

        new CssBlock("color: red; color: blue;").AssignTo(box);

        Assert.Equal("blue", box.Color);
    }

    [Fact]
    public void InheritTakesTheValueFromTheParentBox()
    {
        var parent = new CssBox(null);
        new CssBlock("color: green;").AssignTo(parent);

        var child = new CssBox(parent);
        new CssBlock("color: inherit;").AssignTo(child);

        Assert.Equal("green", child.Color);
    }

    [Fact]
    public void ADocumentPicksUpTheDefaultStyleSheet()
    {
        var container = new InitialContainer("<p>hello</p>");

        Assert.True(container.MediaBlocks.ContainsKey("all"));
        Assert.NotEmpty(container.MediaBlocks["all"]);
    }

    [Fact]
    public void ADocumentKeepsItsSource()
    {
        const string html = "<p>hello</p>";

        Assert.Equal(html, new InitialContainer(html).DocumentSource);
    }
}
