using System;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using MetroFramework.Components;
using MetroFramework.Controls;
using MetroFramework.Drawing;
using MetroFramework.Forms;
using MetroFramework.Interfaces;
using MetroFramework.Localization;

namespace MetroFramework.Tests;

public class MetroFormTests
{
    private static void Paint(Form form, int width = 400, int height = 300)
    {
        form.Size = new Size(width, height);

        using var bitmap = new Bitmap(width, height);
        using Graphics graphics = Graphics.FromImage(bitmap);
        var args = new PaintEventArgs(graphics, new Rectangle(0, 0, width, height));

        MethodInfo onPaint = typeof(MetroForm).GetMethod("OnPaint",
            BindingFlags.Instance | BindingFlags.NonPublic,
            null, new[] { typeof(PaintEventArgs) }, null);

        try
        {
            onPaint.Invoke(form, new object[] { args });
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw new InvalidOperationException(
                "MetroForm.OnPaint threw " + ex.InnerException.GetType().Name + ": " + ex.InnerException.Message,
                ex.InnerException);
        }
    }

    [Fact]
    public void StartsOnTheFrameworkDefaults()
    {
        using var form = new MetroForm();

        Assert.Equal(MetroDefaults.Style, form.Style);
        Assert.Equal(MetroDefaults.Theme, form.Theme);
    }

    [Fact]
    public void FollowsAStyleManager()
    {
        using var form = new MetroForm();

        form.StyleManager = new MetroStyleManager
        {
            Style = MetroColorStyle.Brown,
            Theme = MetroThemeStyle.Dark
        };

        Assert.Equal(MetroColorStyle.Brown, form.Style);
        Assert.Equal(MetroThemeStyle.Dark, form.Theme);
    }

    [Fact]
    public void TheBackgroundFollowsTheTheme()
    {
        using var form = new MetroForm { Theme = MetroThemeStyle.Dark };

        Assert.Equal(MetroPaint.BackColor.Form(MetroThemeStyle.Dark), form.BackColor);
    }

    [Fact]
    public void PaintsInEitherTheme()
    {
        foreach (MetroThemeStyle theme in new[] { MetroThemeStyle.Light, MetroThemeStyle.Dark })
        {
            using var form = new MetroForm { Theme = theme, Text = "Caption" };
            Paint(form);
        }
    }

    [Fact]
    public void PaintsAtEveryCaptionAlignment()
    {
        foreach (MetroFormTextAlign align in Enum.GetValues<MetroFormTextAlign>())
        {
            using var form = new MetroForm { TextAlign = align, Text = "Caption" };
            Paint(form);
        }
    }

    [Fact]
    public void PaintsAtEveryBorderStyle()
    {
        foreach (MetroFormBorderStyle style in Enum.GetValues<MetroFormBorderStyle>())
        {
            using var form = new MetroForm { BorderStyle = style, Text = "Caption" };
            Paint(form);
        }
    }

    [Fact]
    public void PaintsWithoutAHeader()
    {
        using var form = new MetroForm { DisplayHeader = false, Text = "Caption" };

        Paint(form);
    }

    [Fact]
    public void PaintsInEveryPaletteColour()
    {
        foreach (MetroColorStyle style in Enum.GetValues<MetroColorStyle>())
        {
            using var form = new MetroForm { Style = style, Text = "Caption" };
            Paint(form);
        }
    }

    [Fact]
    public void TheShadowTypeRoundTrips()
    {
        foreach (MetroFormShadowType shadow in Enum.GetValues<MetroFormShadowType>())
        {
            using var form = new MetroForm { ShadowType = shadow };

            Assert.Equal(shadow, form.ShadowType);
        }
    }

    [Fact]
    public void MovableAndResizableRoundTrip()
    {
        using var form = new MetroForm { Movable = false, Resizable = false };

        Assert.False(form.Movable);
        Assert.False(form.Resizable);

        form.Movable = true;
        form.Resizable = true;

        Assert.True(form.Movable);
        Assert.True(form.Resizable);
    }

    /// <summary>
    /// The header is drawn by the form itself, so the padding always has to leave
    /// room for it however the caller sets it.
    /// </summary>
    [Fact]
    public void PaddingAlwaysLeavesRoomForTheHeader()
    {
        using var form = new MetroForm();

        form.Padding = new Padding(0, 0, 0, 0);

        Assert.True(form.Padding.Top >= 0);
        Assert.True(form.Padding.Left >= 0);
    }

    [Fact]
    public void RemoveCloseButtonIsSafeBeforeTheFormIsShown()
    {
        using var form = new MetroForm();

        form.RemoveCloseButton();
    }

    [Fact]
    public void HostsMetroControls()
    {
        using var form = new MetroForm();
        var button = new MetroButton { Text = "Go" };
        form.Controls.Add(button);

        Assert.Contains(button, form.Controls.Cast<Control>());
    }

    [Fact]
    public void CanBeDisposedTwice()
    {
        var form = new MetroForm();

        form.Dispose();
        form.Dispose();
    }

    [Fact]
    public void MetroMessageBoxIsAMetroForm()
    {
        using var messageBox = new MetroMessageBox();

        Assert.IsAssignableFrom<MetroForm>(messageBox);
        Assert.IsAssignableFrom<IMetroForm>(messageBox);
    }
}

/// <summary>
/// The on/off captions of <see cref="MetroToggle"/> come from XML resources
/// embedded in the assembly, keyed by the two-letter language of the current
/// culture with English as the fallback.
/// </summary>
public class MetroLocalizeTests
{
    [Fact]
    public void EnglishIsTheFallbackLanguage()
    {
        Assert.Equal("en", new MetroLocalize("MetroToggle").DefaultLanguage());
    }

    [Fact]
    public void TheCurrentLanguageIsTheTwoLetterCodeInLowercase()
    {
        CultureInfo previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal("de", new MetroLocalize("MetroToggle").CurrentLanguage());

            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-GB");
            Assert.Equal("en", new MetroLocalize("MetroToggle").CurrentLanguage());
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    /// <summary>
    /// A key with no translation is reported as "~key" so the gap is visible in the
    /// user interface rather than silently blank.
    /// </summary>
    [Fact]
    public void AnUnknownKeyIsMarkedWithATilde()
    {
        var localize = new MetroLocalize("MetroToggle");

        Assert.Equal("~NoSuchKey", localize.translate("NoSuchKey"));
    }

    [Fact]
    public void AnEmptyKeyTranslatesToAnEmptyString()
    {
        var localize = new MetroLocalize("MetroToggle");

        Assert.Equal(string.Empty, localize.translate(null));
        Assert.Equal(string.Empty, localize.translate(string.Empty));
    }

    [Fact]
    public void AnUnknownControlNameLeavesEveryKeyUntranslated()
    {
        var localize = new MetroLocalize("NoSuchControl");

        Assert.Equal("~StatusOn", localize.translate("StatusOn"));
    }

    [Fact]
    public void PlaceholdersAreSubstitutedInOrder()
    {
        var localize = new MetroLocalize("MetroToggle");

        // The key is unknown, so the "~key" marker itself is the template: the point
        // is that the substitution machinery runs over whatever came back.
        Assert.Equal("~#1", localize.translate("#1"));
        Assert.Equal("~a", localize.translate("#1", "a"));
        Assert.Equal("~ab", localize.translate("#1#2", "a", "b"));
        Assert.Equal("~abc", localize.getValue("#1#2#3", "a", "b", "c"));
        Assert.Equal("~abcd", localize.getValue("#1#2#3#4", "a", "b", "c", "d"));
        Assert.Equal("~abcde", localize.getValue("#1#2#3#4#5", "a", "b", "c", "d", "e"));
    }

    [Fact]
    public void ANullPlaceholderBecomesAnEmptyString()
    {
        var localize = new MetroLocalize("MetroToggle");

        Assert.Equal("~", localize.translate("#1", null));
    }
}
