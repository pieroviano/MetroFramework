using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using MetroFramework.Components;
using MetroFramework.Drawing;
using MetroFramework.Controls;
using MetroFramework.Forms;
using MetroFramework.Interfaces;

namespace MetroFramework.Tests;

public class MetroStyleManagerTests
{
    [Fact]
    public void StartsOnTheFrameworkDefaults()
    {
        using var manager = new MetroStyleManager();

        Assert.Equal(MetroDefaults.Style, manager.Style);
        Assert.Equal(MetroDefaults.Theme, manager.Theme);
    }

    [Fact]
    public void AssigningDefaultResolvesToTheFrameworkDefault()
    {
        using var manager = new MetroStyleManager
        {
            Style = MetroColorStyle.Default,
            Theme = MetroThemeStyle.Default
        };

        Assert.Equal(MetroDefaults.Style, manager.Style);
        Assert.Equal(MetroDefaults.Theme, manager.Theme);
    }

    [Fact]
    public void CloneCopiesTheStyleAndTheme()
    {
        using var manager = new MetroStyleManager
        {
            Style = MetroColorStyle.Purple,
            Theme = MetroThemeStyle.Dark
        };

        var clone = (MetroStyleManager)manager.Clone();

        Assert.NotSame(manager, clone);
        Assert.Equal(MetroColorStyle.Purple, clone.Style);
        Assert.Equal(MetroThemeStyle.Dark, clone.Theme);
    }

    [Fact]
    public void CloneDoesNotInheritTheOwner()
    {
        using var owner = new MetroForm();
        using var manager = new MetroStyleManager { Owner = owner };

        var clone = (MetroStyleManager)manager.Clone();

        Assert.Null(clone.Owner);
    }

    [Fact]
    public void CloneWithAnOwnerAdoptsAMetroForm()
    {
        using var owner = new MetroForm();
        using var manager = new MetroStyleManager { Theme = MetroThemeStyle.Dark };

        var clone = (MetroStyleManager)manager.Clone(owner);

        Assert.Same(owner, clone.Owner);
        Assert.Equal(MetroThemeStyle.Dark, clone.Theme);
    }

    [Fact]
    public void CloneWithAPlainContainerDoesNotAdoptIt()
    {
        using var owner = new UserControl();
        using var manager = new MetroStyleManager();

        var clone = (MetroStyleManager)manager.Clone(owner);

        Assert.Null(clone.Owner);
    }

    [Fact]
    public void TakingOwnershipPushesTheThemeOntoExistingChildren()
    {
        using var form = new MetroForm();
        var button = new MetroButton();
        var label = new MetroLabel();
        form.Controls.Add(button);
        form.Controls.Add(label);

        using var manager = new MetroStyleManager
        {
            Style = MetroColorStyle.Orange,
            Theme = MetroThemeStyle.Dark
        };
        manager.Owner = form;

        Assert.Same(manager, button.StyleManager);
        Assert.Same(manager, label.StyleManager);
        Assert.Equal(MetroColorStyle.Orange, button.Style);
        Assert.Equal(MetroThemeStyle.Dark, label.Theme);
    }

    [Fact]
    public void AControlAddedLaterPicksUpTheStyleManager()
    {
        using var form = new MetroForm();
        using var manager = new MetroStyleManager { Owner = form, Theme = MetroThemeStyle.Dark };

        var button = new MetroButton();
        form.Controls.Add(button);

        Assert.Same(manager, button.StyleManager);
        Assert.Equal(MetroThemeStyle.Dark, button.Theme);
    }

    [Fact]
    public void TheThemeReachesNestedChildren()
    {
        using var form = new MetroForm();
        var panel = new MetroPanel();
        var nested = new MetroButton();
        panel.Controls.Add(nested);
        form.Controls.Add(panel);

        using var manager = new MetroStyleManager { Theme = MetroThemeStyle.Dark };
        manager.Owner = form;

        Assert.Same(manager, nested.StyleManager);
        Assert.Equal(MetroThemeStyle.Dark, nested.Theme);
    }

    [Fact]
    public void TheThemeReachesTabPages()
    {
        using var form = new MetroForm();
        var tabControl = new MetroTabControl();
        var page = new MetroTabPage();
        tabControl.TabPages.Add(page);
        form.Controls.Add(tabControl);

        using var manager = new MetroStyleManager { Theme = MetroThemeStyle.Dark };
        manager.Owner = form;

        Assert.Equal(MetroThemeStyle.Dark, page.Theme);
    }

    [Fact]
    public void ReplacingTheOwnerDetachesFromTheOldOne()
    {
        using var first = new MetroForm();
        using var second = new MetroForm();
        using var manager = new MetroStyleManager { Owner = first };

        manager.Owner = second;
        Assert.Same(second, manager.Owner);

        // The old owner is no longer wired up, so a control added to it is untouched.
        var orphan = new MetroButton();
        first.Controls.Add(orphan);

        Assert.Null(orphan.StyleManager);
    }

    /// <summary>
    /// While a designer-generated InitializeComponent is running, style changes must
    /// not fan out over a half-built control tree; they are applied once at EndInit.
    /// </summary>
    [Fact]
    public void ChangesAreDeferredWhileInitialising()
    {
        using var form = new MetroForm();
        var button = new MetroButton();
        form.Controls.Add(button);

        using var manager = new MetroStyleManager();
        var init = (ISupportInitialize)manager;

        init.BeginInit();
        manager.Owner = form;
        manager.Theme = MetroThemeStyle.Dark;

        Assert.Null(button.StyleManager);

        init.EndInit();

        Assert.Same(manager, button.StyleManager);
        Assert.Equal(MetroThemeStyle.Dark, button.Theme);
    }

    [Fact]
    public void UpdateIsSafeWithoutAnOwner()
    {
        using var manager = new MetroStyleManager();

        manager.Update();
    }

    [Fact]
    public void ComponentsInTheParentContainerAreThemedToo()
    {
        using var container = new Container();
        using var manager = new MetroStyleManager(container) { Theme = MetroThemeStyle.Dark };
        var toolTip = new MetroToolTip();
        container.Add(toolTip);

        manager.Update();

        Assert.Same(manager, toolTip.StyleManager);
        Assert.Equal(MetroThemeStyle.Dark, toolTip.Theme);
    }
}

public class MetroStyleExtenderTests
{
    [Fact]
    public void ExtendsPlainControlsOnly()
    {
        using var extender = new MetroStyleExtender();
        var provider = (IExtenderProvider)extender;

        using var plain = new Panel();
        using var metroControl = new MetroButton();
        using var metroForm = new MetroForm();

        Assert.True(provider.CanExtend(plain));
        Assert.False(provider.CanExtend(metroControl));
        Assert.False(provider.CanExtend(metroForm));
        Assert.False(provider.CanExtend(new object()));
    }

    [Fact]
    public void ApplyMetroThemeRoundTrips()
    {
        using var extender = new MetroStyleExtender();
        using var panel = new Panel();

        Assert.False(extender.GetApplyMetroTheme(panel));

        extender.SetApplyMetroTheme(panel, true);
        Assert.True(extender.GetApplyMetroTheme(panel));

        extender.SetApplyMetroTheme(panel, false);
        Assert.False(extender.GetApplyMetroTheme(panel));
    }

    [Fact]
    public void EnablingTwiceDoesNotRegisterTheControlTwice()
    {
        using var extender = new MetroStyleExtender();
        using var panel = new Panel();

        extender.SetApplyMetroTheme(panel, true);
        extender.SetApplyMetroTheme(panel, true);
        extender.SetApplyMetroTheme(panel, false);

        Assert.False(extender.GetApplyMetroTheme(panel));
    }

    [Fact]
    public void ANullControlIsIgnored()
    {
        using var extender = new MetroStyleExtender();

        extender.SetApplyMetroTheme(null, true);

        Assert.False(extender.GetApplyMetroTheme(null));
    }

    [Fact]
    public void TheThemeIsPushedOntoExtendedControls()
    {
        using var extender = new MetroStyleExtender();
        using var panel = new Panel();
        extender.SetApplyMetroTheme(panel, true);

        extender.Theme = MetroThemeStyle.Dark;

        Assert.Equal(MetroPaint.BackColor.Form(MetroThemeStyle.Dark), panel.BackColor);
        Assert.Equal(MetroPaint.ForeColor.Label.Normal(MetroThemeStyle.Dark), panel.ForeColor);
    }

    [Fact]
    public void AStyleManagerSuppliesTheTheme()
    {
        using var extender = new MetroStyleExtender();
        using var panel = new Panel();
        extender.SetApplyMetroTheme(panel, true);

        extender.StyleManager = new MetroStyleManager { Theme = MetroThemeStyle.Dark };

        Assert.Equal(MetroThemeStyle.Dark, extender.Theme);
        Assert.Equal(MetroPaint.BackColor.Form(MetroThemeStyle.Dark), panel.BackColor);
    }
}

public class MetroToolTipTests
{
    [Fact]
    public void StartsOwnerDrawnAndAlwaysVisible()
    {
        using var toolTip = new MetroToolTip();

        Assert.True(toolTip.OwnerDraw);
        Assert.True(toolTip.ShowAlways);
    }

    [Fact]
    public void TheDecorativeSettersAreClampedToTheMetroLook()
    {
        using var toolTip = new MetroToolTip();

        toolTip.IsBalloon = true;
        toolTip.ToolTipIcon = ToolTipIcon.Error;
        toolTip.ToolTipTitle = "ignored";

        Assert.False(toolTip.IsBalloon);
        Assert.Equal(ToolTipIcon.None, toolTip.ToolTipIcon);
        Assert.Equal(string.Empty, toolTip.ToolTipTitle);
    }

    [Fact]
    public void SettingATipOnAMetroControlAlsoCoversItsChildren()
    {
        using var toolTip = new MetroToolTip();
        using var textBox = new MetroTextBox();

        toolTip.SetToolTip(textBox, "Type here");

        Assert.Equal("Type here", toolTip.GetToolTip(textBox));

        foreach (Control child in textBox.Controls)
        {
            Assert.Equal("Type here", toolTip.GetToolTip(child));
        }
    }

    [Fact]
    public void FollowsTheStyleManager()
    {
        using var toolTip = new MetroToolTip();

        toolTip.StyleManager = new MetroStyleManager
        {
            Style = MetroColorStyle.Teal,
            Theme = MetroThemeStyle.Dark
        };

        Assert.Equal(MetroColorStyle.Teal, toolTip.Style);
        Assert.Equal(MetroThemeStyle.Dark, toolTip.Theme);
    }
}
