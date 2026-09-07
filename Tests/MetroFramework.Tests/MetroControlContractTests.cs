using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using MetroFramework.Components;
using MetroFramework.Controls;
using MetroFramework.Interfaces;

namespace MetroFramework.Tests;

/// <summary>
/// Shared helpers for exercising the widgets.
/// </summary>
internal static class MetroControls
{
    /// <summary>
    /// Every concrete <see cref="IMetroControl"/> shipped by the framework, found by
    /// scanning the assembly so a new widget is covered the moment it is added.
    /// </summary>
    public static IEnumerable<Type> ControlTypes =>
        typeof(MetroButton).Assembly
            .GetTypes()
            .Where(t => t.IsClass
                        && !t.IsAbstract
                        && t.IsPublic
                        && typeof(Control).IsAssignableFrom(t)
                        && typeof(IMetroControl).IsAssignableFrom(t)
                        && t.GetConstructor(Type.EmptyTypes) != null)
            .OrderBy(t => t.FullName, StringComparer.Ordinal);

    public static IEnumerable<object[]> ControlTypeData() =>
        ControlTypes.Select(t => new object[] { t });

    public static Control Create(Type type) => (Control)Activator.CreateInstance(type);

    /// <summary>
    /// Runs the control's own OnPaint against an offscreen surface.
    /// </summary>
    public static void PaintOffscreen(Control control, int width = 220, int height = 80)
    {
        control.Size = new Size(width, height);

        using var bitmap = new Bitmap(width, height);
        using Graphics graphics = Graphics.FromImage(bitmap);

        var args = new PaintEventArgs(graphics, new Rectangle(0, 0, width, height));

        Invoke(control, "OnPaintBackground", args);
        Invoke(control, "OnPaint", args);
    }

    private static void Invoke(Control control, string methodName, PaintEventArgs args)
    {
        MethodInfo method = FindMethod(control.GetType(), methodName);
        if (method == null) return;

        try
        {
            method.Invoke(control, new object[] { args });
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw new InvalidOperationException(
                control.GetType().Name + "." + methodName + " threw " + ex.InnerException.GetType().Name +
                ": " + ex.InnerException.Message, ex.InnerException);
        }
    }

    /// <summary>
    /// Whether the control opted into <see cref="ControlStyles.SupportsTransparentBackColor"/>.
    /// </summary>
    public static bool SupportsTransparentBackColor(Control control)
    {
        MethodInfo getStyle = typeof(Control).GetMethod("GetStyle",
            BindingFlags.Instance | BindingFlags.NonPublic);

        return (bool)getStyle.Invoke(control, new object[] { ControlStyles.SupportsTransparentBackColor });
    }

    private static MethodInfo FindMethod(Type type, string name)
    {
        for (Type t = type; t != null; t = t.BaseType)
        {
            MethodInfo method = t.GetMethod(name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly,
                null, new[] { typeof(PaintEventArgs) }, null);

            if (method != null) return method;
        }

        return null;
    }
}

/// <summary>
/// Contract that every Metro widget owes <see cref="IMetroControl"/>, checked across
/// the whole widget set rather than one control at a time.
/// </summary>
public class MetroControlContractTests
{
    public static IEnumerable<object[]> AllControls() => MetroControls.ControlTypeData();

    [Fact]
    public void TheAssemblyScanFindsTheWidgets()
    {
        // Guards every theory below against passing vacuously.
        Assert.True(MetroControls.ControlTypes.Count() >= 14,
            "Only found: " + string.Join(", ", MetroControls.ControlTypes.Select(t => t.Name)));
    }

    [Theory]
    [MemberData(nameof(AllControls))]
    public void DefaultsToTheFrameworkStyleAndTheme(Type type)
    {
        using Control control = MetroControls.Create(type);
        var metro = (IMetroControl)control;

        Assert.Equal(MetroDefaults.Style, metro.Style);
        Assert.Equal(MetroDefaults.Theme, metro.Theme);
    }

    [Theory]
    [MemberData(nameof(AllControls))]
    public void AnExplicitStyleWins(Type type)
    {
        using Control control = MetroControls.Create(type);
        var metro = (IMetroControl)control;

        metro.Style = MetroColorStyle.Magenta;
        metro.Theme = MetroThemeStyle.Dark;

        Assert.Equal(MetroColorStyle.Magenta, metro.Style);
        Assert.Equal(MetroThemeStyle.Dark, metro.Theme);
    }

    [Theory]
    [MemberData(nameof(AllControls))]
    public void AStyleManagerSuppliesTheStyleWhileTheControlIsOnDefault(Type type)
    {
        using Control control = MetroControls.Create(type);
        var metro = (IMetroControl)control;

        var manager = new MetroStyleManager { Style = MetroColorStyle.Lime, Theme = MetroThemeStyle.Dark };
        metro.StyleManager = manager;

        Assert.Same(manager, metro.StyleManager);
        Assert.Equal(MetroColorStyle.Lime, metro.Style);
        Assert.Equal(MetroThemeStyle.Dark, metro.Theme);
    }

    [Theory]
    [MemberData(nameof(AllControls))]
    public void AnExplicitStyleOverridesTheStyleManager(Type type)
    {
        using Control control = MetroControls.Create(type);
        var metro = (IMetroControl)control;

        metro.StyleManager = new MetroStyleManager { Style = MetroColorStyle.Lime, Theme = MetroThemeStyle.Dark };
        metro.Style = MetroColorStyle.Red;
        metro.Theme = MetroThemeStyle.Light;

        Assert.Equal(MetroColorStyle.Red, metro.Style);
        Assert.Equal(MetroThemeStyle.Light, metro.Theme);
    }

    [Theory]
    [MemberData(nameof(AllControls))]
    public void ClearingTheStyleManagerFallsBackToTheFrameworkDefaults(Type type)
    {
        using Control control = MetroControls.Create(type);
        var metro = (IMetroControl)control;

        metro.StyleManager = new MetroStyleManager { Style = MetroColorStyle.Lime };
        metro.StyleManager = null;

        Assert.Equal(MetroDefaults.Style, metro.Style);
        Assert.Equal(MetroDefaults.Theme, metro.Theme);
    }

    [Theory]
    [MemberData(nameof(AllControls))]
    public void TheBooleanAppearanceFlagsRoundTrip(Type type)
    {
        using Control control = MetroControls.Create(type);
        var metro = (IMetroControl)control;

        metro.UseCustomBackColor = true;
        metro.UseCustomForeColor = true;
        metro.UseStyleColors = true;
        metro.UseSelectable = true;

        Assert.True(metro.UseCustomBackColor);
        Assert.True(metro.UseCustomForeColor);
        Assert.True(metro.UseStyleColors);
        Assert.True(metro.UseSelectable);

        metro.UseCustomBackColor = false;
        metro.UseCustomForeColor = false;
        metro.UseStyleColors = false;
        metro.UseSelectable = false;

        Assert.False(metro.UseCustomBackColor);
        Assert.False(metro.UseCustomForeColor);
        Assert.False(metro.UseStyleColors);
        Assert.False(metro.UseSelectable);
    }

    /// <summary>
    /// <see cref="MetroColorStyle.Default"/> and <see cref="MetroThemeStyle.Default"/>
    /// mean "ask the style manager", never a literal colour, so they must not leak out
    /// of the getter.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllControls))]
    public void TheGettersNeverReturnDefault(Type type)
    {
        using Control control = MetroControls.Create(type);
        var metro = (IMetroControl)control;

        metro.Style = MetroColorStyle.Default;
        metro.Theme = MetroThemeStyle.Default;

        Assert.NotEqual(MetroColorStyle.Default, metro.Style);
        Assert.NotEqual(MetroThemeStyle.Default, metro.Theme);
    }

    [Theory]
    [MemberData(nameof(AllControls))]
    public void CanBeDisposedTwice(Type type)
    {
        Control control = MetroControls.Create(type);

        control.Dispose();
        control.Dispose();
    }
}

/// <summary>
/// Every widget is painted offscreen in each theme and each palette colour. These
/// tests do not judge the pixels; they assert that the paint path completes without
/// throwing, which is where colour-table and geometry mistakes surface.
/// </summary>
public class MetroControlPaintTests
{
    public static IEnumerable<object[]> ControlsAndThemes() =>
        from type in MetroControls.ControlTypes
        from theme in new[] { MetroThemeStyle.Light, MetroThemeStyle.Dark }
        select new object[] { type, theme };

    public static IEnumerable<object[]> AllControls() => MetroControls.ControlTypeData();

    [Theory]
    [MemberData(nameof(ControlsAndThemes))]
    public void PaintsInEitherTheme(Type type, MetroThemeStyle theme)
    {
        using Control control = MetroControls.Create(type);
        ((IMetroControl)control).Theme = theme;

        MetroControls.PaintOffscreen(control);
    }

    [Theory]
    [MemberData(nameof(AllControls))]
    public void PaintsInEveryPaletteColour(Type type)
    {
        foreach (MetroColorStyle style in Enum.GetValues<MetroColorStyle>())
        {
            using Control control = MetroControls.Create(type);
            var metro = (IMetroControl)control;
            metro.Style = style;
            metro.UseStyleColors = true;

            MetroControls.PaintOffscreen(control);
        }
    }

    [Theory]
    [MemberData(nameof(AllControls))]
    public void PaintsWhenDisabled(Type type)
    {
        using Control control = MetroControls.Create(type);
        control.Enabled = false;

        MetroControls.PaintOffscreen(control);
    }

    [Theory]
    [MemberData(nameof(AllControls))]
    public void PaintsWithCustomColours(Type type)
    {
        using Control control = MetroControls.Create(type);
        var metro = (IMetroControl)control;
        metro.UseCustomBackColor = true;
        metro.UseCustomForeColor = true;
        control.BackColor = Color.FromArgb(10, 20, 30);
        control.ForeColor = Color.Yellow;

        MetroControls.PaintOffscreen(control);
    }

    /// <summary>
    /// A control that declares <see cref="ControlStyles.SupportsTransparentBackColor"/>
    /// takes the slow paint path, where the background is composited rather than
    /// cleared. Widgets that do not declare it are skipped: WinForms rejects an alpha
    /// back colour on them outright.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllControls))]
    public void PaintsWithATranslucentBackgroundWhereSupported(Type type)
    {
        using Control control = MetroControls.Create(type);

        if (!MetroControls.SupportsTransparentBackColor(control))
        {
            return;
        }

        ((IMetroControl)control).UseCustomBackColor = true;
        control.BackColor = Color.FromArgb(128, 10, 20, 30);

        MetroControls.PaintOffscreen(control);
    }

    [Theory]
    [MemberData(nameof(AllControls))]
    public void PaintsWithText(Type type)
    {
        using Control control = MetroControls.Create(type);
        try
        {
            control.Text = "Metro";
        }
        catch (NotSupportedException)
        {
            // A few widgets derive their own caption and reject an assignment.
            return;
        }

        MetroControls.PaintOffscreen(control);
    }

    [Theory]
    [MemberData(nameof(AllControls))]
    public void PaintsAtADegenerateSize(Type type)
    {
        using Control control = MetroControls.Create(type);

        MetroControls.PaintOffscreen(control, 1, 1);
    }

    /// <summary>
    /// The custom paint events are the documented extension point; a handler must be
    /// reached during a normal paint.
    /// </summary>
    [Fact]
    public void CustomPaintEventsAreRaised()
    {
        using var button = new MetroButton();
        bool background = false, foreground = false;

        button.CustomPaintBackground += (_, _) => background = true;
        button.CustomPaintForeground += (_, _) => foreground = true;
        button.UseCustomBackColor = true;
        button.BackColor = Color.FromArgb(10, 255, 0, 0);   // translucent, so the fast path is skipped

        MetroControls.PaintOffscreen(button);

        Assert.True(background, "CustomPaintBackground was not raised.");
        Assert.True(foreground, "CustomPaintForeground was not raised.");
    }
}
