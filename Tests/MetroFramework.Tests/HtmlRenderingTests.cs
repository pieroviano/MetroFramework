using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using MetroFramework.Drawing.Html;

namespace MetroFramework.Tests;

/// <summary>
/// End to end exercises of the bundled HTML/CSS renderer: parse a fragment, lay it
/// out and paint it onto an offscreen surface.
/// </summary>
public class HtmlRendererTests
{
    private static void Render(string html, float width = 300f, bool clip = false)
    {
        using var bitmap = new Bitmap(400, 300);
        using Graphics graphics = Graphics.FromImage(bitmap);

        HtmlRenderer.Render(graphics, html, new RectangleF(0, 0, width, 300f), clip);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("<p>hello</p>")]
    [InlineData("<b>bold</b> and <i>italic</i> and <u>underlined</u>")]
    [InlineData("<h1>Heading</h1><p>Body text</p>")]
    [InlineData("<ul><li>one</li><li>two</li></ul>")]
    [InlineData("<ol><li>one</li><li>two</li></ol>")]
    [InlineData("<table border=1><tr><td>a</td><td>b</td></tr><tr><td>c</td><td>d</td></tr></table>")]
    [InlineData("<div style=\"color: red; font-size: 14px\">styled</div>")]
    [InlineData("<span style=\"background-color: #eee\">shaded</span>")]
    [InlineData("line one<br>line two")]
    [InlineData("<hr>")]
    [InlineData("<a href=\"http://example.com\">a link</a>")]
    [InlineData("<p style=\"text-align: center\">centred</p>")]
    [InlineData("<p style=\"margin: 10px; padding: 5px; border: 1px solid black\">boxed</p>")]
    public void RendersACommonFragment(string html)
    {
        Render(html);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<p>unclosed")]
    [InlineData("</p>")]
    [InlineData("<<>>")]
    [InlineData("<p style=\"color\">missing value</p>")]
    [InlineData("<p style=\"color: notacolour\">bad colour</p>")]
    [InlineData("<p style=\"font-size: notasize\">bad size</p>")]
    [InlineData("<table><tr><td colspan=abc>bad colspan</td></tr></table>")]
    [InlineData("<img src=\"does-not-exist.png\">")]
    public void MalformedMarkupStillRenders(string html)
    {
        Render(html);
    }

    [Fact]
    public void RendersWithClipping()
    {
        Render("<p>clipped content that is long enough to overflow the area</p>", 100f, clip: true);
    }

    [Fact]
    public void RendersAtAVeryNarrowWidth()
    {
        Render("<p>wrap this text please</p>", 1f);
    }

    [Fact]
    public void TheRendererKnowsAboutItsOwnAssembly()
    {
        Assert.Contains(typeof(HtmlRenderer).Assembly, HtmlRenderer.References);
    }
}

public class InitialContainerTests
{
    private static InitialContainer Measured(string html, float width = 300f)
    {
        var container = new InitialContainer(html);
        container.SetBounds(new RectangleF(0, 0, width, 0));

        using var bitmap = new Bitmap(1, 1);
        using Graphics graphics = Graphics.FromImage(bitmap);
        container.MeasureBounds(graphics);

        return container;
    }

    [Fact]
    public void MeasuringGivesTheContentANonZeroExtent()
    {
        InitialContainer container = Measured("<p>some text</p>");

        Assert.True(container.MaximumSize.Height > 0f);
        Assert.True(container.MaximumSize.Width > 0f);
    }

    [Fact]
    public void MoreTextIsTaller()
    {
        float oneLine = Measured("<p>one</p>").MaximumSize.Height;
        float manyLines = Measured("<p>one</p><p>two</p><p>three</p><p>four</p>").MaximumSize.Height;

        Assert.True(manyLines > oneLine,
            "four paragraphs (" + manyLines + ") were not taller than one (" + oneLine + ")");
    }

    [Fact]
    public void NarrowerContentWrapsAndGrowsTaller()
    {
        const string html = "<p>a rather long sentence that will need to wrap onto several lines</p>";

        float wide = Measured(html, 400f).MaximumSize.Height;
        float narrow = Measured(html, 60f).MaximumSize.Height;

        Assert.True(narrow > wide,
            "narrow (" + narrow + ") was not taller than wide (" + wide + ")");
    }

    [Fact]
    public void AStyleSheetCanBeFedIn()
    {
        var container = new InitialContainer("<p>text</p>");

        container.FeedStyleSheet("p { color: red; }");

        Assert.True(container.MediaBlocks["all"].ContainsKey("p"));
        Assert.Equal("red", container.MediaBlocks["all"]["p"].Properties["color"]);
    }

    [Fact]
    public void AnAtMediaRuleGetsItsOwnBucket()
    {
        var container = new InitialContainer("<p>text</p>");

        container.FeedStyleSheet("@media print { p { color: green; } }");

        Assert.True(container.MediaBlocks.ContainsKey("print"));
        Assert.Equal("green", container.MediaBlocks["print"]["p"].Properties["color"]);
    }

    [Fact]
    public void CommentsAreStrippedFromAStyleSheet()
    {
        var container = new InitialContainer("<p>text</p>");

        container.FeedStyleSheet("/* a comment */ p { color: blue; }");

        Assert.Equal("blue", container.MediaBlocks["all"]["p"].Properties["color"]);
    }

    [Fact]
    public void AnEmbeddedStyleElementIsPickedUp()
    {
        var container = new InitialContainer("<style>p { color: purple; }</style><p>text</p>");

        Assert.Equal("purple", container.MediaBlocks["all"]["p"].Properties["color"]);
    }

    [Fact]
    public void ScrollOffsetAndAntialiasFlagsRoundTrip()
    {
        var container = new InitialContainer("<p>text</p>")
        {
            ScrollOffset = new PointF(3, 4),
            AvoidGeometryAntialias = true,
            AvoidTextAntialias = true
        };

        Assert.Equal(new PointF(3, 4), container.ScrollOffset);
        Assert.True(container.AvoidGeometryAntialias);
        Assert.True(container.AvoidTextAntialias);
    }

    [Fact]
    public void TheDocumentTreeIsBuilt()
    {
        var container = new InitialContainer("<p>one</p><p>two</p>");

        Assert.NotEmpty(container.Boxes);
    }
}

public class CssBoxWordSplitterTests
{
    private static CssBoxWordSplitter Split(string text, string whiteSpace = null)
    {
        var box = new CssBox(null);
        if (whiteSpace != null) box.WhiteSpace = whiteSpace;

        var splitter = new CssBoxWordSplitter(box, text);
        splitter.SplitWords();
        return splitter;
    }

    [Fact]
    public void SplitsOnSpaces()
    {
        CssBoxWordSplitter splitter = Split("one two three");

        Assert.Equal(new[] { "one", " ", "two", " ", "three" },
            splitter.Words.Select(w => w.Text).ToArray());
    }

    /// <summary>
    /// The splitter groups a run of spaces into a single word; whether that run is
    /// then collapsed to one space is the layout engine's decision, driven by
    /// <see cref="CssBoxWordSplitter.CollapsesWhiteSpaces"/>.
    /// </summary>
    [Fact]
    public void ARunOfSpacesBecomesOneWord()
    {
        CssBoxWordSplitter splitter = Split("one     two");

        Assert.Equal(new[] { "one", "     ", "two" }, splitter.Words.Select(w => w.Text).ToArray());
        Assert.Equal(3, splitter.Words.Count);
    }

    [Fact]
    public void CarriageReturnsAreDroppedButLineFeedsAreKept()
    {
        CssBoxWordSplitter splitter = Split("one\r\ntwo");

        Assert.DoesNotContain(splitter.Words, w => w.Text.Contains('\r'));
        Assert.Contains(splitter.Words, w => w.Text == "\n");
    }

    [Fact]
    public void TabsBecomeTheirOwnWord()
    {
        CssBoxWordSplitter splitter = Split("one\ttwo");

        Assert.Contains(splitter.Words, w => w.Text == "\t");
    }

    [Fact]
    public void EmptyTextProducesNoWords()
    {
        Assert.Empty(Split(string.Empty).Words);
        Assert.Empty(Split(null == null ? "" : "").Words);
    }

    [Fact]
    public void LeadingAndTrailingSpacesBecomeWords()
    {
        CssBoxWordSplitter splitter = Split(" one ");

        Assert.Equal(new[] { " ", "one", " " }, splitter.Words.Select(w => w.Text).ToArray());
    }

    [Fact]
    public void TheSplitterRemembersWhatItWasGiven()
    {
        var box = new CssBox(null);
        var splitter = new CssBoxWordSplitter(box, "text");

        Assert.Same(box, splitter.Box);
        Assert.Equal("text", splitter.Text);
    }

    [Theory]
    [InlineData(CssConstants.Normal, true)]
    [InlineData(CssConstants.Nowrap, true)]
    [InlineData(CssConstants.PreLine, true)]
    [InlineData(CssConstants.Pre, false)]
    [InlineData(CssConstants.PreWrap, false)]
    public void CollapsesWhiteSpacesFollowsTheWhiteSpaceProperty(string whiteSpace, bool collapses)
    {
        var box = new CssBox(null) { WhiteSpace = whiteSpace };

        Assert.Equal(collapses, CssBoxWordSplitter.CollapsesWhiteSpaces(box));
    }

    [Theory]
    [InlineData(CssConstants.Normal, true)]
    [InlineData(CssConstants.Nowrap, true)]
    [InlineData(CssConstants.Pre, false)]
    [InlineData(CssConstants.PreLine, false)]
    public void EliminatesLineBreaksFollowsTheWhiteSpaceProperty(string whiteSpace, bool eliminates)
    {
        var box = new CssBox(null) { WhiteSpace = whiteSpace };

        Assert.Equal(eliminates, CssBoxWordSplitter.EliminatesLineBreaks(box));
    }
}

public class HtmlControlTests
{
    private static void Paint(Control control, int width = 240, int height = 120)
    {
        control.Size = new Size(width, height);

        using var bitmap = new Bitmap(width, height);
        using Graphics graphics = Graphics.FromImage(bitmap);
        var args = new PaintEventArgs(graphics, new Rectangle(0, 0, width, height));

        MethodInfo onPaint = control.GetType().GetMethod("OnPaint",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy,
            null, new[] { typeof(PaintEventArgs) }, null);

        try
        {
            onPaint.Invoke(control, new object[] { args });
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw new InvalidOperationException(
                control.GetType().Name + ".OnPaint threw " + ex.InnerException.GetType().Name +
                ": " + ex.InnerException.Message, ex.InnerException);
        }
    }

    [Fact]
    public void HtmlLabelRendersItsText()
    {
        using var label = new HtmlLabel { Text = "<b>bold</b> text" };

        Assert.NotNull(label.HtmlContainer);
        Paint(label);
    }

    [Fact]
    public void HtmlLabelWithAutoSizeAdoptsTheContentHeight()
    {
        using var label = new HtmlLabel { AutoSize = true };

        label.Text = "<p>one</p><p>two</p><p>three</p>";
        label.MeasureBounds();

        Assert.True(label.Height > 0);
    }

    [Fact]
    public void HtmlPanelRendersItsText()
    {
        using var panel = new HtmlPanel { Text = "<p>panel content</p>" };

        Assert.NotNull(panel.HtmlContainer);
        Paint(panel);
    }

    [Fact]
    public void HtmlPanelHandlesEmptyText()
    {
        using var panel = new HtmlPanel { Text = string.Empty };

        Paint(panel);
    }

    [Fact]
    public void HtmlToolTipCanBeConstructed()
    {
        using var toolTip = new HtmlToolTip();

        Assert.True(toolTip.OwnerDraw);
    }
}
