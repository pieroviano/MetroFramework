using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using MetroFramework.Controls;
using MetroFramework.Interfaces;

namespace MetroFramework.Tests;

/// <summary>
/// <see cref="DefaultValueAttribute"/> is what the Windows Forms designer uses to
/// decide whether a property needs to be written into InitializeComponent. If the
/// attribute disagrees with the value a fresh control actually has, the designer
/// either drops a setting that was needed or writes one that was not.
/// </summary>
public class MetroDesignerDefaultValueTests
{
    /// <summary>
    /// Style, Theme and UseSelectable are excluded: their stored value is
    /// <c>Default</c>, which the getter deliberately resolves against the style
    /// manager, so the getter never reports the stored default. That resolution is
    /// covered by <see cref="MetroControlContractTests"/> instead.
    /// </summary>
    private static readonly string[] ResolvedProperties = { "Style", "Theme", "UseSelectable" };

    public static IEnumerable<object[]> PropertiesWithADefaultValue()
    {
        Assembly metroFramework = typeof(MetroButton).Assembly;

        foreach (Type type in MetroControls.ControlTypes)
        {
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length > 0) continue;
                if (!property.CanRead) continue;

                // Only properties this library declares; a base class in
                // System.Windows.Forms is not ours to keep consistent.
                if (property.DeclaringType.Assembly != metroFramework) continue;
                if (Array.IndexOf(ResolvedProperties, property.Name) >= 0) continue;

                var attribute = property.GetCustomAttribute<DefaultValueAttribute>(inherit: false);
                if (attribute == null) continue;

                yield return new object[] { type, property.Name, attribute.Value };
            }
        }
    }

    [Fact]
    public void TheScanFindsPropertiesToCheck()
    {
        Assert.True(PropertiesWithADefaultValue().Count() > 40);
    }

    [Theory]
    [MemberData(nameof(PropertiesWithADefaultValue))]
    public void AFreshControlMatchesItsDeclaredDefault(Type type, string propertyName, object declaredDefault)
    {
        using Control control = MetroControls.Create(type);
        PropertyInfo property = type.GetProperty(propertyName);

        object actual = property.GetValue(control);

        Assert.Equal(declaredDefault, actual);
    }
}

public class MetroScrollBarTests
{
    [Fact]
    public void StartsVerticalWithTheDocumentedRange()
    {
        using var scrollBar = new MetroScrollBar();

        Assert.Equal(MetroScrollOrientation.Vertical, scrollBar.Orientation);
        Assert.Equal(0, scrollBar.Minimum);
        Assert.Equal(100, scrollBar.Maximum);
        Assert.Equal(0, scrollBar.Value);
    }

    [Fact]
    public void TheOrientationConstructorSelectsHorizontal()
    {
        using var scrollBar = new MetroScrollBar(MetroScrollOrientation.Horizontal);

        Assert.Equal(MetroScrollOrientation.Horizontal, scrollBar.Orientation);
    }

    [Fact]
    public void ChangingOrientationSwapsTheExtents()
    {
        using var scrollBar = new MetroScrollBar { Width = 10, Height = 200 };

        scrollBar.Orientation = MetroScrollOrientation.Horizontal;

        Assert.Equal(200, scrollBar.Width);
        Assert.Equal(10, scrollBar.Height);
    }

    /// <summary>
    /// ScrollbarSize is the thickness of the bar, which lies on whichever axis the
    /// scroll bar does not run along.
    /// </summary>
    [Fact]
    public void ScrollbarSizeIsTheThicknessForEitherOrientation()
    {
        using var vertical = new MetroScrollBar(MetroScrollOrientation.Vertical);
        vertical.ScrollbarSize = 17;
        Assert.Equal(17, vertical.Width);
        Assert.Equal(17, vertical.ScrollbarSize);

        using var horizontal = new MetroScrollBar(MetroScrollOrientation.Horizontal);
        horizontal.ScrollbarSize = 17;
        Assert.Equal(17, horizontal.Height);
        Assert.Equal(17, horizontal.ScrollbarSize);
    }

    [Fact]
    public void ValueIsClampedToTheRange()
    {
        using var scrollBar = new MetroScrollBar { Minimum = 10, Maximum = 50 };

        scrollBar.Value = 30;
        Assert.Equal(30, scrollBar.Value);

        scrollBar.Value = 999;      // ignored
        Assert.Equal(30, scrollBar.Value);

        scrollBar.Value = -5;       // ignored
        Assert.Equal(30, scrollBar.Value);
    }

    [Fact]
    public void RaisingTheMinimumPullsTheValueUp()
    {
        using var scrollBar = new MetroScrollBar { Value = 5 };

        scrollBar.Minimum = 20;

        Assert.Equal(20, scrollBar.Minimum);
        Assert.True(scrollBar.Value >= 20);
    }

    [Fact]
    public void LoweringTheMaximumPullsTheValueDown()
    {
        using var scrollBar = new MetroScrollBar { Maximum = 1000, Value = 900 };

        scrollBar.Maximum = 100;

        Assert.Equal(100, scrollBar.Maximum);
        Assert.True(scrollBar.Value <= 100);
    }

    [Fact]
    public void AnInvertedRangeIsRejected()
    {
        using var scrollBar = new MetroScrollBar { Minimum = 10, Maximum = 50 };

        scrollBar.Maximum = 5;      // below the minimum
        scrollBar.Minimum = 80;     // above the maximum

        Assert.Equal(50, scrollBar.Maximum);
        Assert.Equal(10, scrollBar.Minimum);
    }

    [Fact]
    public void LargeChangeIsCappedByTheRange()
    {
        using var scrollBar = new MetroScrollBar { Minimum = 0, Maximum = 20 };

        scrollBar.LargeChange = 500;

        Assert.Equal(20, scrollBar.LargeChange);
    }

    [Fact]
    public void MouseWheelBarPartitionsMustBePositive()
    {
        using var scrollBar = new MetroScrollBar();

        Assert.Throws<ArgumentOutOfRangeException>(() => scrollBar.MouseWheelBarPartitions = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => scrollBar.MouseWheelBarPartitions = -1);

        scrollBar.MouseWheelBarPartitions = 4;
        Assert.Equal(4, scrollBar.MouseWheelBarPartitions);
    }

    [Fact]
    public void ScrollIsRaisedWhenTheValueMoves()
    {
        using var scrollBar = new MetroScrollBar();
        ScrollEventArgs seen = null;
        scrollBar.Scroll += (_, e) => seen = e;

        scrollBar.Value = 42;

        Assert.NotNull(seen);
        Assert.Equal(42, seen.NewValue);
        Assert.Equal(ScrollOrientation.VerticalScroll, seen.ScrollOrientation);
    }

    [Fact]
    public void HitTestIsFalseWellOutsideTheThumb()
    {
        using var scrollBar = new MetroScrollBar();

        Assert.False(scrollBar.HitTest(new Point(-100, -100)));
    }
}

public class MetroTrackBarTests
{
    [Fact]
    public void DefaultsToZeroToOneHundredAtTheMidpoint()
    {
        using var trackBar = new MetroTrackBar();

        Assert.Equal(0, trackBar.Minimum);
        Assert.Equal(100, trackBar.Maximum);
        Assert.Equal(50, trackBar.Value);
    }

    /// <summary>
    /// The three argument constructor has to accept any well-formed range, including
    /// one that does not overlap the default 0..100.
    /// </summary>
    [Theory]
    [InlineData(0, 100, 50)]
    [InlineData(200, 300, 250)]     // entirely above the default range
    [InlineData(-100, -10, -50)]    // entirely below it
    [InlineData(-50, 50, 0)]
    public void TheRangeConstructorAcceptsAnyWellFormedRange(int min, int max, int value)
    {
        using var trackBar = new MetroTrackBar(min, max, value);

        Assert.Equal(min, trackBar.Minimum);
        Assert.Equal(max, trackBar.Maximum);
        Assert.Equal(value, trackBar.Value);
    }

    [Fact]
    public void TheRangeConstructorRejectsAnInvertedRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MetroTrackBar(100, 10, 50));
    }

    [Fact]
    public void TheRangeConstructorRejectsAValueOutsideTheRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MetroTrackBar(0, 10, 50));
    }

    [Fact]
    public void ValueOutsideTheRangeIsRejected()
    {
        using var trackBar = new MetroTrackBar();

        Assert.Throws<ArgumentOutOfRangeException>(() => trackBar.Value = 101);
        Assert.Throws<ArgumentOutOfRangeException>(() => trackBar.Value = -1);
    }

    [Fact]
    public void ValueChangedIsRaisedOnAssignment()
    {
        using var trackBar = new MetroTrackBar();
        int raised = 0;
        trackBar.ValueChanged += (_, _) => raised++;

        trackBar.Value = 60;

        Assert.Equal(1, raised);
        Assert.Equal(60, trackBar.Value);
    }

    [Fact]
    public void RaisingTheMinimumPastTheValuePullsTheValueUp()
    {
        using var trackBar = new MetroTrackBar { Value = 10 };

        trackBar.Minimum = 40;

        Assert.Equal(40, trackBar.Value);
    }

    [Fact]
    public void LoweringTheMaximumPastTheValuePullsTheValueDown()
    {
        using var trackBar = new MetroTrackBar { Value = 90 };

        trackBar.Maximum = 60;

        Assert.Equal(60, trackBar.Value);
    }

    [Fact]
    public void AnInvertedRangeIsRejected()
    {
        using var trackBar = new MetroTrackBar();

        Assert.Throws<ArgumentOutOfRangeException>(() => trackBar.Minimum = 200);
        Assert.Throws<ArgumentOutOfRangeException>(() => trackBar.Maximum = -1);
    }

    [Fact]
    public void MouseWheelBarPartitionsMustBePositive()
    {
        using var trackBar = new MetroTrackBar();

        Assert.Throws<ArgumentOutOfRangeException>(() => trackBar.MouseWheelBarPartitions = 0);
    }
}

public class MetroProgressBarTests
{
    [Fact]
    public void ProgressIsReportedAsAPercentageOfTheRange()
    {
        using var progressBar = new MetroProgressBar { Minimum = 0, Maximum = 200, Value = 50 };

        Assert.Equal(25d, progressBar.ProgressTotalPercent, 6);
        Assert.Equal(0.25d, progressBar.ProgressTotalValue, 6);
        Assert.Equal("25%", progressBar.ProgressPercentText);
    }

    [Fact]
    public void ProgressIsZeroAtTheMinimumAndFullAtTheMaximum()
    {
        using var progressBar = new MetroProgressBar { Minimum = 0, Maximum = 100 };

        progressBar.Value = 0;
        Assert.Equal(0d, progressBar.ProgressTotalPercent, 6);

        progressBar.Value = 100;
        Assert.Equal(100d, progressBar.ProgressTotalPercent, 6);
    }

    [Fact]
    public void ValueAboveTheMaximumIsIgnored()
    {
        using var progressBar = new MetroProgressBar { Maximum = 100, Value = 40 };

        progressBar.Value = 500;

        Assert.Equal(40, progressBar.Value);
    }

    [Fact]
    public void PaintsAtEveryProgressBarStyle()
    {
        foreach (ProgressBarStyle style in Enum.GetValues<ProgressBarStyle>())
        {
            using var progressBar = new MetroProgressBar { ProgressBarStyle = style, Value = 42 };
            progressBar.HideProgressText = false;

            MetroControls.PaintOffscreen(progressBar);
        }
    }
}

public class MetroToggleTests
{
    /// <summary>
    /// The on/off caption comes from the embedded localisation resource. A missing
    /// resource is reported as "~key", so a literal tilde means the lookup failed.
    /// </summary>
    [Fact]
    public void UsesTheLocalisedCaptionWhenNoOverrideIsSet()
    {
        using var toggle = new MetroToggle();

        toggle.Checked = false;
        Assert.Equal("Off", toggle.Text);

        toggle.Checked = true;
        Assert.Equal("On", toggle.Text);
    }

    [Fact]
    public void AnExplicitCaptionOverridesTheLocalisedOne()
    {
        using var toggle = new MetroToggle { OnText = "Si", OffText = "No" };

        toggle.Checked = true;
        Assert.Equal("Si", toggle.Text);

        toggle.Checked = false;
        Assert.Equal("No", toggle.Text);
    }

    [Fact]
    public void CheckedChangedIsRaised()
    {
        using var toggle = new MetroToggle();
        int raised = 0;
        toggle.CheckedChanged += (_, _) => raised++;

        toggle.Checked = true;

        Assert.Equal(1, raised);
    }

    [Fact]
    public void PaintsWithAndWithoutTheStatusCaption()
    {
        foreach (bool displayStatus in new[] { true, false })
        {
            using var toggle = new MetroToggle { DisplayStatus = displayStatus, Checked = displayStatus };
            MetroControls.PaintOffscreen(toggle);
        }
    }
}

public class MetroTileTests
{
    [Fact]
    public void TileCountRoundTrips()
    {
        using var tile = new MetroTile { TileCount = 7 };

        Assert.Equal(7, tile.TileCount);
    }

    [Fact]
    public void PaintsTheCountBadgeOnlyWhenAsked()
    {
        foreach (bool paintCount in new[] { true, false })
        {
            using var tile = new MetroTile { TileCount = 12, PaintTileCount = paintCount };
            MetroControls.PaintOffscreen(tile);
        }
    }

    [Fact]
    public void PaintsWithATileImageAtEveryAlignment()
    {
        using var image = new Bitmap(16, 16);

        foreach (ContentAlignment alignment in Enum.GetValues<ContentAlignment>())
        {
            using var tile = new MetroTile
            {
                TileImage = image,
                UseTileImage = true,
                TileImageAlign = alignment
            };

            MetroControls.PaintOffscreen(tile);
        }
    }

    [Fact]
    public void ActivateControlAcceptsAHostedChild()
    {
        using var tile = new MetroTile();
        using var child = new MetroLabel();
        tile.Controls.Add(child);

        Assert.True(tile.ActivateControl(child));
        Assert.Same(child, tile.ActiveControl);
    }
}

public class MetroTabControlTests
{
    [Fact]
    public void PaintsWithSeveralPages()
    {
        using var tabControl = new MetroTabControl();
        tabControl.TabPages.Add(new MetroTabPage { Text = "One" });
        tabControl.TabPages.Add(new MetroTabPage { Text = "Two" });

        MetroControls.PaintOffscreen(tabControl, 300, 200);
    }

    [Fact]
    public void PaintsWithNoPages()
    {
        using var tabControl = new MetroTabControl();

        MetroControls.PaintOffscreen(tabControl, 300, 200);
    }

    [Fact]
    public void APageInheritsTheStyleManagerOfItsControl()
    {
        using var tabControl = new MetroTabControl();
        var page = new MetroTabPage();
        tabControl.TabPages.Add(page);

        var manager = new MetroFramework.Components.MetroStyleManager { Theme = MetroThemeStyle.Dark };
        tabControl.StyleManager = manager;
        page.StyleManager = manager;

        Assert.Equal(MetroThemeStyle.Dark, page.Theme);
    }
}

public class MetroTextBoxTests
{
    [Fact]
    public void TextRoundTrips()
    {
        using var textBox = new MetroTextBox();

        textBox.Text = "hello";

        Assert.Equal("hello", textBox.Text);
    }

    [Fact]
    public void PaintsWithAPromptText()
    {
        using var textBox = new MetroTextBox { PromptText = "Search", Text = string.Empty };

        MetroControls.PaintOffscreen(textBox);
    }

    [Fact]
    public void SelectAllAndClearAreSafeBeforeTheHandleExists()
    {
        using var textBox = new MetroTextBox { Text = "hello" };

        textBox.SelectAll();
        textBox.Clear();

        Assert.Equal(string.Empty, textBox.Text);
    }
}

public class MetroLabelAndLinkTests
{
    [Fact]
    public void LabelPaintsAtEveryFontSizeAndWeight()
    {
        foreach (MetroLabelSize size in Enum.GetValues<MetroLabelSize>())
        {
            foreach (MetroLabelWeight weight in Enum.GetValues<MetroLabelWeight>())
            {
                using var label = new MetroLabel { FontSize = size, FontWeight = weight, Text = "Metro" };
                MetroControls.PaintOffscreen(label);
            }
        }
    }

    [Fact]
    public void LabelPaintsAtEveryTextAlignment()
    {
        foreach (ContentAlignment alignment in Enum.GetValues<ContentAlignment>())
        {
            using var label = new MetroLabel { TextAlign = alignment, Text = "Metro" };
            MetroControls.PaintOffscreen(label);
        }
    }

    [Fact]
    public void LinkPaintsAtEveryFontSizeAndWeight()
    {
        foreach (MetroLinkSize size in Enum.GetValues<MetroLinkSize>())
        {
            foreach (MetroLinkWeight weight in Enum.GetValues<MetroLinkWeight>())
            {
                using var link = new MetroLink { FontSize = size, FontWeight = weight, Text = "Metro" };
                MetroControls.PaintOffscreen(link);
            }
        }
    }

    [Fact]
    public void ADisabledLabelStillReportsAPreferredSize()
    {
        using var label = new MetroLabel { Text = "Metro", Enabled = false };

        Size size = label.GetPreferredSize(Size.Empty);

        Assert.True(size.Width > 0);
        Assert.True(size.Height > 0);
    }
}

public class MetroCheckBoxAndRadioButtonTests
{
    [Fact]
    public void CheckBoxRaisesCheckedChanged()
    {
        using var checkBox = new MetroCheckBox();
        int raised = 0;
        checkBox.CheckedChanged += (_, _) => raised++;

        checkBox.Checked = true;

        Assert.Equal(1, raised);
        Assert.True(checkBox.Checked);
    }

    [Fact]
    public void CheckBoxPaintsInEveryCheckState()
    {
        foreach (CheckState state in Enum.GetValues<CheckState>())
        {
            using var checkBox = new MetroCheckBox { ThreeState = true, CheckState = state, Text = "Option" };
            MetroControls.PaintOffscreen(checkBox);
        }
    }

    [Fact]
    public void RadioButtonRaisesCheckedChanged()
    {
        using var radioButton = new MetroRadioButton();
        int raised = 0;
        radioButton.CheckedChanged += (_, _) => raised++;

        radioButton.Checked = true;

        Assert.Equal(1, raised);
    }

    [Fact]
    public void RadioButtonsInOneContainerAreMutuallyExclusive()
    {
        using var panel = new MetroPanel();
        var first = new MetroRadioButton();
        var second = new MetroRadioButton();
        panel.Controls.Add(first);
        panel.Controls.Add(second);

        first.Checked = true;
        second.Checked = true;

        Assert.False(first.Checked);
        Assert.True(second.Checked);
    }
}

public class MetroComboBoxTests
{
    [Fact]
    public void ItemsAndSelectionRoundTrip()
    {
        using var comboBox = new MetroComboBox();
        comboBox.Items.AddRange(new object[] { "one", "two", "three" });

        comboBox.SelectedIndex = 1;

        Assert.Equal(3, comboBox.Items.Count);
        Assert.Equal("two", comboBox.SelectedItem);
    }

    [Fact]
    public void PaintsWithItemsAndAPromptText()
    {
        using var comboBox = new MetroComboBox { PromptText = "Pick one" };
        comboBox.Items.Add("one");

        MetroControls.PaintOffscreen(comboBox);
    }
}

public class MetroProgressSpinnerTests
{
    [Fact]
    public void ValueIsClampedToTheRange()
    {
        using var spinner = new MetroProgressSpinner { Minimum = 0, Maximum = 100 };

        spinner.Value = 50;
        Assert.Equal(50, spinner.Value);
    }

    [Fact]
    public void ValueOutsideTheRangeIsRejected()
    {
        using var spinner = new MetroProgressSpinner { Minimum = 0, Maximum = 100 };

        Assert.Throws<ArgumentOutOfRangeException>(() => spinner.Value = 200);
    }

    [Fact]
    public void PaintsWhileSpinningAndWhileDeterminate()
    {
        using var indeterminate = new MetroProgressSpinner { Spinning = true, Value = -1 };
        MetroControls.PaintOffscreen(indeterminate, 64, 64);

        using var determinate = new MetroProgressSpinner { Spinning = false, Value = 40 };
        MetroControls.PaintOffscreen(determinate, 64, 64);
    }
}

public class MetroPanelAndUserControlTests
{
    [Fact]
    public void PanelExposesItsScrollbarSettings()
    {
        using var panel = new MetroPanel
        {
            HorizontalScrollbar = true,
            VerticalScrollbar = true,
            HorizontalScrollbarSize = 12,
            VerticalScrollbarSize = 12,
            HorizontalScrollbarBarColor = true,
            VerticalScrollbarBarColor = true,
            HorizontalScrollbarHighlightOnWheel = true,
            VerticalScrollbarHighlightOnWheel = true
        };

        Assert.True(panel.HorizontalScrollbar);
        Assert.True(panel.VerticalScrollbar);
        Assert.Equal(12, panel.HorizontalScrollbarSize);
        Assert.Equal(12, panel.VerticalScrollbarSize);
        Assert.True(panel.HorizontalScrollbarBarColor);
        Assert.True(panel.VerticalScrollbarBarColor);
        Assert.True(panel.HorizontalScrollbarHighlightOnWheel);
        Assert.True(panel.VerticalScrollbarHighlightOnWheel);
    }

    [Fact]
    public void PanelPaintsWithChildren()
    {
        using var panel = new MetroPanel();
        panel.Controls.Add(new MetroLabel { Text = "child" });

        MetroControls.PaintOffscreen(panel, 300, 200);
    }

    [Fact]
    public void UserControlPaintsWithChildren()
    {
        using var userControl = new MetroUserControl();
        userControl.Controls.Add(new MetroButton { Text = "child" });

        MetroControls.PaintOffscreen(userControl, 300, 200);
    }
}
