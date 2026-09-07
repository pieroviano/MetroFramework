using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using MetroFramework.Controls;
using MetroFramework.Forms;
using MetroFramework.Interfaces;

namespace MetroFramework.Tests;

/// <summary>
/// The widgets track hover, press and focus themselves and repaint from those
/// handlers. These tests drive the protected On... methods directly and then paint,
/// which is where a state flag that the paint path does not expect shows up.
/// </summary>
public class MetroControlInputTests
{
    public static IEnumerable<object[]> AllControls() => MetroControls.ControlTypeData();

    private static void Raise(Control control, string handler, EventArgs args)
    {
        for (Type t = control.GetType(); t != null; t = t.BaseType)
        {
            MethodInfo method = t.GetMethod(handler,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                null, new[] { args.GetType() }, null);

            if (method == null)
            {
                // Fall back to the declared parameter type (e.g. OnMouseDown takes
                // MouseEventArgs, OnMouseEnter takes EventArgs).
                foreach (MethodInfo candidate in t.GetMethods(
                             BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    ParameterInfo[] parameters = candidate.GetParameters();
                    if (candidate.Name == handler && parameters.Length == 1 &&
                        parameters[0].ParameterType.IsInstanceOfType(args))
                    {
                        method = candidate;
                        break;
                    }
                }
            }

            if (method == null) continue;

            try
            {
                method.Invoke(control, new object[] { args });
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw new InvalidOperationException(
                    control.GetType().Name + "." + handler + " threw " +
                    ex.InnerException.GetType().Name + ": " + ex.InnerException.Message,
                    ex.InnerException);
            }

            return;
        }
    }

    private static MouseEventArgs Click(int x, int y) =>
        new MouseEventArgs(MouseButtons.Left, 1, x, y, 0);

    [Theory]
    [MemberData(nameof(AllControls))]
    public void AHoverThenLeaveRepaintsCleanly(Type type)
    {
        using Control control = MetroControls.Create(type);
        control.Size = new Size(220, 80);

        Raise(control, "OnMouseEnter", EventArgs.Empty);
        MetroControls.PaintOffscreen(control);

        Raise(control, "OnMouseLeave", EventArgs.Empty);
        MetroControls.PaintOffscreen(control);
    }

    [Theory]
    [MemberData(nameof(AllControls))]
    public void APressThenReleaseRepaintsCleanly(Type type)
    {
        using Control control = MetroControls.Create(type);
        control.Size = new Size(220, 80);

        Raise(control, "OnMouseDown", Click(10, 10));
        MetroControls.PaintOffscreen(control);

        Raise(control, "OnMouseUp", Click(10, 10));
        MetroControls.PaintOffscreen(control);
    }

    [Theory]
    [MemberData(nameof(AllControls))]
    public void MouseMovementAcrossTheWidgetRepaintsCleanly(Type type)
    {
        using Control control = MetroControls.Create(type);
        control.Size = new Size(220, 80);

        foreach (Point point in new[]
                 {
                     new Point(0, 0), new Point(110, 40), new Point(219, 79),
                     new Point(-5, -5), new Point(500, 500),
                 })
        {
            Raise(control, "OnMouseMove", new MouseEventArgs(MouseButtons.None, 0, point.X, point.Y, 0));
        }

        MetroControls.PaintOffscreen(control);
    }

    [Theory]
    [MemberData(nameof(AllControls))]
    public void GainingAndLosingFocusRepaintsCleanly(Type type)
    {
        using Control control = MetroControls.Create(type);
        control.Size = new Size(220, 80);

        Raise(control, "OnGotFocus", EventArgs.Empty);
        Raise(control, "OnEnter", EventArgs.Empty);
        MetroControls.PaintOffscreen(control);

        Raise(control, "OnLeave", EventArgs.Empty);
        Raise(control, "OnLostFocus", EventArgs.Empty);
        MetroControls.PaintOffscreen(control);
    }

    [Theory]
    [MemberData(nameof(AllControls))]
    public void KeyboardInputIsHandledCleanly(Type type)
    {
        using Control control = MetroControls.Create(type);
        control.Size = new Size(220, 80);

        foreach (Keys key in new[] { Keys.Space, Keys.Enter, Keys.Left, Keys.Right, Keys.Up, Keys.Down, Keys.Tab })
        {
            Raise(control, "OnKeyDown", new KeyEventArgs(key));
            Raise(control, "OnKeyUp", new KeyEventArgs(key));
        }

        MetroControls.PaintOffscreen(control);
    }

    [Theory]
    [MemberData(nameof(AllControls))]
    public void AResizeRepaintsCleanly(Type type)
    {
        using Control control = MetroControls.Create(type);

        foreach (Size size in new[] { new Size(1, 1), new Size(50, 20), new Size(400, 300) })
        {
            control.Size = size;
            Raise(control, "OnResize", EventArgs.Empty);
        }

        MetroControls.PaintOffscreen(control, 400, 300);
    }

    [Fact]
    public void TheScrollBarRespondsToTheWheelAndTheKeyboard()
    {
        using var scrollBar = new MetroScrollBar { Minimum = 0, Maximum = 100, Value = 50 };
        scrollBar.Size = new Size(12, 200);

        Raise(scrollBar, "OnMouseWheel", new MouseEventArgs(MouseButtons.None, 0, 5, 100, -120));
        Raise(scrollBar, "OnMouseWheel", new MouseEventArgs(MouseButtons.None, 0, 5, 100, 120));

        Assert.InRange(scrollBar.Value, scrollBar.Minimum, scrollBar.Maximum);

        MetroControls.PaintOffscreen(scrollBar, 12, 200);
    }

    [Fact]
    public void DraggingTheScrollBarThumbKeepsTheValueInRange()
    {
        using var scrollBar = new MetroScrollBar { Minimum = 0, Maximum = 100 };
        scrollBar.Size = new Size(12, 200);

        Raise(scrollBar, "OnMouseDown", Click(6, 10));
        for (int y = 10; y < 200; y += 20)
        {
            Raise(scrollBar, "OnMouseMove", new MouseEventArgs(MouseButtons.Left, 1, 6, y, 0));
        }
        Raise(scrollBar, "OnMouseUp", Click(6, 190));

        Assert.InRange(scrollBar.Value, scrollBar.Minimum, scrollBar.Maximum);
    }

    [Fact]
    public void TheTrackBarRespondsToTheWheelWithoutLeavingItsRange()
    {
        using var trackBar = new MetroTrackBar { Minimum = 0, Maximum = 100, Value = 50 };
        trackBar.Size = new Size(200, 24);

        Raise(trackBar, "OnMouseWheel", new MouseEventArgs(MouseButtons.None, 0, 100, 12, -120));
        Raise(trackBar, "OnMouseWheel", new MouseEventArgs(MouseButtons.None, 0, 100, 12, 120));

        Assert.InRange(trackBar.Value, trackBar.Minimum, trackBar.Maximum);
    }

    [Fact]
    public void TheTrackBarKeyboardKeepsTheValueInRange()
    {
        using var trackBar = new MetroTrackBar { Minimum = 0, Maximum = 10, Value = 0 };
        trackBar.Size = new Size(200, 24);

        for (int i = 0; i < 30; i++)
        {
            Raise(trackBar, "OnKeyDown", new KeyEventArgs(Keys.Right));
            Raise(trackBar, "OnKeyDown", new KeyEventArgs(Keys.PageUp));
        }

        Assert.InRange(trackBar.Value, trackBar.Minimum, trackBar.Maximum);

        for (int i = 0; i < 30; i++)
        {
            Raise(trackBar, "OnKeyDown", new KeyEventArgs(Keys.Left));
            Raise(trackBar, "OnKeyDown", new KeyEventArgs(Keys.PageDown));
        }

        Assert.InRange(trackBar.Value, trackBar.Minimum, trackBar.Maximum);
    }

    [Fact]
    public void TheTileTracksItsPressedState()
    {
        using var tile = new MetroTile { Text = "Tile", TileCount = 3 };
        tile.Size = new Size(150, 150);

        Raise(tile, "OnMouseEnter", EventArgs.Empty);
        Raise(tile, "OnMouseDown", Click(75, 75));
        MetroControls.PaintOffscreen(tile, 150, 150);

        Raise(tile, "OnMouseUp", Click(75, 75));
        Raise(tile, "OnMouseLeave", EventArgs.Empty);
        MetroControls.PaintOffscreen(tile, 150, 150);
    }
}

/// <summary>
/// The task window is a singleton overlay. Its static entry points are reachable
/// before anything has been shown, and must not fall over in that state.
/// </summary>
public class MetroTaskWindowTests
{
    [Fact]
    public void NothingIsVisibleBeforeAnythingIsShown()
    {
        MetroTaskWindow.ForceClose();

        Assert.False(MetroTaskWindow.IsVisible());
    }

    [Fact]
    public void ClosingWhenNothingIsOpenIsHarmless()
    {
        MetroTaskWindow.ForceClose();
        MetroTaskWindow.ForceClose();
        MetroTaskWindow.CancelAutoClose();

        Assert.False(MetroTaskWindow.IsVisible());
    }

    [Fact]
    public void ItIsAMetroForm()
    {
        using var window = new MetroTaskWindow();

        Assert.IsAssignableFrom<MetroForm>(window);
        Assert.IsAssignableFrom<IMetroForm>(window);
    }

    [Fact]
    public void CancelTimerRoundTrips()
    {
        using var window = new MetroTaskWindow { CancelTimer = true };

        Assert.True(window.CancelTimer);

        window.CancelTimer = false;
        Assert.False(window.CancelTimer);
    }

    [Fact]
    public void ItHostsTheControlItIsGiven()
    {
        using var hosted = new MetroLabel { Text = "hosted" };
        using var window = new MetroTaskWindow(0, hosted);

        Assert.Equal("hosted", hosted.Text);
        Assert.NotNull(window);
    }
}
