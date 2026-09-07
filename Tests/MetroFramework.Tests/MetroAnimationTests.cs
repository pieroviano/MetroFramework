using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using MetroFramework.Animation;

namespace MetroFramework.Tests;

/// <summary>
/// Exposes the protected easing function so the curves can be checked directly.
/// </summary>
internal sealed class TransitionProbe : AnimationBase
{
    public int Transition(TransitionType type, float t, float b, float d, float c)
    {
        transitionType = type;
        return MakeTransition(t, b, d, c);
    }
}

public class TransitionTests
{
    private static readonly TransitionType[] Curves =
    {
        TransitionType.Linear,
        TransitionType.EaseInQuad,
        TransitionType.EaseOutQuad,
        TransitionType.EaseInOutQuad,
        TransitionType.EaseInCubic,
        TransitionType.EaseOutCubic,
        TransitionType.EaseInOutCubic,
        TransitionType.EaseInQuart,
        TransitionType.EaseInExpo,
        TransitionType.EaseOutExpo,
    };

    public static TheoryData<TransitionType> AllCurves
    {
        get
        {
            var data = new TheoryData<TransitionType>();
            foreach (TransitionType curve in Curves) data.Add(curve);
            return data;
        }
    }

    /// <summary>
    /// Every easing curve starts at the origin value and finishes at origin + change,
    /// whatever it does in between.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllCurves))]
    public void EveryCurveStartsAndEndsOnTheEndpoints(TransitionType curve)
    {
        var probe = new TransitionProbe();
        const float begin = 100f, change = 200f, duration = 10f;

        Assert.Equal(100, probe.Transition(curve, 0f, begin, duration, change));
        Assert.Equal(300, probe.Transition(curve, duration, begin, duration, change));
    }

    [Theory]
    [MemberData(nameof(AllCurves))]
    public void EveryCurveStaysWithinTheEndpoints(TransitionType curve)
    {
        var probe = new TransitionProbe();
        const float begin = 0f, change = 100f, duration = 10f;

        for (float t = 0f; t <= duration; t += 0.5f)
        {
            int value = probe.Transition(curve, t, begin, duration, change);

            Assert.InRange(value, 0, 100);
        }
    }

    [Theory]
    [MemberData(nameof(AllCurves))]
    public void EveryCurveIsMonotonic(TransitionType curve)
    {
        var probe = new TransitionProbe();
        int previous = int.MinValue;

        for (float t = 0f; t <= 10f; t += 0.5f)
        {
            int value = probe.Transition(curve, t, 0f, 10f, 100f);

            Assert.True(value >= previous,
                curve + " went backwards at t=" + t + ": " + value + " after " + previous);
            previous = value;
        }
    }

    [Fact]
    public void LinearIsExactlyProportional()
    {
        var probe = new TransitionProbe();

        Assert.Equal(0, probe.Transition(TransitionType.Linear, 0f, 0f, 10f, 100f));
        Assert.Equal(50, probe.Transition(TransitionType.Linear, 5f, 0f, 10f, 100f));
        Assert.Equal(100, probe.Transition(TransitionType.Linear, 10f, 0f, 10f, 100f));
    }

    [Fact]
    public void ACurveCanRunBackwards()
    {
        var probe = new TransitionProbe();

        Assert.Equal(300, probe.Transition(TransitionType.Linear, 0f, 300f, 10f, -200f));
        Assert.Equal(100, probe.Transition(TransitionType.Linear, 10f, 300f, 10f, -200f));
    }
}

public class AnimationLifecycleTests
{
    [Fact]
    public void AFreshAnimationIsNeitherRunningNorHalfFinished()
    {
        var animation = new MoveAnimation();

        Assert.False(animation.IsRunning);
        Assert.True(animation.IsCompleted);
    }

    [Fact]
    public void CancellingAnAnimationThatNeverStartedIsHarmless()
    {
        new MoveAnimation().Cancel();
        new ExpandAnimation().Cancel();
        new ColorBlendAnimation().Cancel();
    }

    /// <summary>
    /// Runs <paramref name="body"/> on a thread that has no synchronisation context.
    /// </summary>
    private static void OnAContextFreeThread(Action body)
    {
        Exception failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                // Constructing a Control installs a WindowsFormsSynchronizationContext
                // on the thread, so build what the test needs first and clear the
                // context immediately before exercising the animation.
                body();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "The worker thread did not finish.");

        if (failure != null) throw failure;
    }

    /// <summary>
    /// The animations marshal every step back onto the synchronisation context that
    /// was current when they were started, which is how they reach the UI thread.
    /// Starting one from a thread that has no context cannot silently do nothing:
    /// it is reported straight away.
    /// </summary>
    [Fact]
    public void StartingWithoutASynchronizationContextIsRejected()
    {
        OnAContextFreeThread(() =>
        {
            using var control = new Control();
            SynchronizationContext.SetSynchronizationContext(null);

            Assert.Throws<InvalidOperationException>(
                () => new MoveAnimation().Start(control, new Point(10, 10), TransitionType.Linear, 5));
        });
    }

    [Fact]
    public void ARejectedStartLeavesTheAnimationIdle()
    {
        OnAContextFreeThread(() =>
        {
            using var control = new Control();
            var animation = new ExpandAnimation();
            SynchronizationContext.SetSynchronizationContext(null);

            Assert.Throws<InvalidOperationException>(
                () => animation.Start(control, new Size(10, 10), TransitionType.Linear, 5));

            Assert.False(animation.IsRunning);
            Assert.True(animation.IsCompleted);
        });
    }

    [Fact]
    public void ColorBlendAlsoNeedsASynchronizationContext()
    {
        OnAContextFreeThread(() =>
        {
            using var control = new Control();
            SynchronizationContext.SetSynchronizationContext(null);

            Assert.Throws<InvalidOperationException>(
                () => new ColorBlendAnimation().Start(control, "BackColor", Color.Red, 5));
        });
    }
}
