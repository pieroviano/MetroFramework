using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using MetroFramework;
using MetroFramework.Drawing;

namespace MetroFramework.Tests;

/// <summary>
/// <see cref="MetroPaint.GetStringFormat"/> and
/// <see cref="MetroPaint.GetTextFormatFlags"/> both translate a
/// <see cref="ContentAlignment"/> into a renderer-specific alignment. They are two
/// spellings of the same mapping, so they must agree: in GDI+ terms
/// <see cref="StringFormat.Alignment"/> is the horizontal axis and
/// <see cref="StringFormat.LineAlignment"/> the vertical one.
/// </summary>
public class MetroPaintTextAlignmentTests
{
    public static TheoryData<ContentAlignment, StringAlignment, StringAlignment> Alignments => new()
    {
        //                                   horizontal              vertical
        { ContentAlignment.TopLeft,      StringAlignment.Near,   StringAlignment.Near   },
        { ContentAlignment.TopCenter,    StringAlignment.Center, StringAlignment.Near   },
        { ContentAlignment.TopRight,     StringAlignment.Far,    StringAlignment.Near   },
        { ContentAlignment.MiddleLeft,   StringAlignment.Near,   StringAlignment.Center },
        { ContentAlignment.MiddleCenter, StringAlignment.Center, StringAlignment.Center },
        { ContentAlignment.MiddleRight,  StringAlignment.Far,    StringAlignment.Center },
        { ContentAlignment.BottomLeft,   StringAlignment.Near,   StringAlignment.Far    },
        { ContentAlignment.BottomCenter, StringAlignment.Center, StringAlignment.Far    },
        { ContentAlignment.BottomRight,  StringAlignment.Far,    StringAlignment.Far    },
    };

    [Theory]
    [MemberData(nameof(Alignments))]
    public void GetStringFormat_MapsHorizontalToAlignmentAndVerticalToLineAlignment(
        ContentAlignment align, StringAlignment expectedHorizontal, StringAlignment expectedVertical)
    {
        using StringFormat format = MetroPaint.GetStringFormat(align);

        Assert.Equal(expectedHorizontal, format.Alignment);
        Assert.Equal(expectedVertical, format.LineAlignment);
    }

    [Fact]
    public void GetStringFormat_AlwaysTrimsWithAnEllipsis()
    {
        foreach (ContentAlignment align in Enum.GetValues<ContentAlignment>())
        {
            using StringFormat format = MetroPaint.GetStringFormat(align);
            Assert.Equal(StringTrimming.EllipsisCharacter, format.Trimming);
        }
    }

    [Fact]
    public void GetStringFormat_ReturnsAFreshInstanceEachCall()
    {
        using StringFormat first = MetroPaint.GetStringFormat(ContentAlignment.TopLeft);
        using StringFormat second = MetroPaint.GetStringFormat(ContentAlignment.TopLeft);

        Assert.NotSame(first, second);
    }

    public static TheoryData<ContentAlignment, TextFormatFlags, TextFormatFlags> FlagAlignments => new()
    {
        { ContentAlignment.TopLeft,      TextFormatFlags.Left,             TextFormatFlags.Top            },
        { ContentAlignment.TopCenter,    TextFormatFlags.HorizontalCenter, TextFormatFlags.Top            },
        { ContentAlignment.TopRight,     TextFormatFlags.Right,            TextFormatFlags.Top            },
        { ContentAlignment.MiddleLeft,   TextFormatFlags.Left,             TextFormatFlags.VerticalCenter },
        { ContentAlignment.MiddleCenter, TextFormatFlags.HorizontalCenter, TextFormatFlags.VerticalCenter },
        { ContentAlignment.MiddleRight,  TextFormatFlags.Right,            TextFormatFlags.VerticalCenter },
        { ContentAlignment.BottomLeft,   TextFormatFlags.Left,             TextFormatFlags.Bottom         },
        { ContentAlignment.BottomCenter, TextFormatFlags.HorizontalCenter, TextFormatFlags.Bottom         },
        { ContentAlignment.BottomRight,  TextFormatFlags.Right,            TextFormatFlags.Bottom         },
    };

    [Theory]
    [MemberData(nameof(FlagAlignments))]
    public void GetTextFormatFlags_CarriesBothAxesPlusEndEllipsis(
        ContentAlignment align, TextFormatFlags horizontal, TextFormatFlags vertical)
    {
        TextFormatFlags flags = MetroPaint.GetTextFormatFlags(align);

        // TextFormatFlags.Left and .Top are both zero, so a plain HasFlag check
        // would pass vacuously. Mask off each axis and compare the whole value.
        const TextFormatFlags horizontalMask =
            TextFormatFlags.HorizontalCenter | TextFormatFlags.Right;
        const TextFormatFlags verticalMask =
            TextFormatFlags.VerticalCenter | TextFormatFlags.Bottom;

        Assert.Equal(horizontal, flags & horizontalMask);
        Assert.Equal(vertical, flags & verticalMask);
        Assert.True(flags.HasFlag(TextFormatFlags.EndEllipsis));
    }

    /// <summary>
    /// The two helpers describe the same layout intent, so a caller switching from
    /// TextRenderer to GDI+ must not see the text jump to a different corner.
    /// </summary>
    [Theory]
    [MemberData(nameof(Alignments))]
    public void GetStringFormat_AgreesWithGetTextFormatFlags(
        ContentAlignment align, StringAlignment expectedHorizontal, StringAlignment expectedVertical)
    {
        _ = expectedHorizontal;
        _ = expectedVertical;

        using StringFormat format = MetroPaint.GetStringFormat(align);
        TextFormatFlags flags = MetroPaint.GetTextFormatFlags(align);

        Assert.Equal(HorizontalOf(flags), format.Alignment);
        Assert.Equal(VerticalOf(flags), format.LineAlignment);
    }

    private static StringAlignment HorizontalOf(TextFormatFlags flags)
    {
        if (flags.HasFlag(TextFormatFlags.Right)) return StringAlignment.Far;
        if (flags.HasFlag(TextFormatFlags.HorizontalCenter)) return StringAlignment.Center;
        return StringAlignment.Near;
    }

    private static StringAlignment VerticalOf(TextFormatFlags flags)
    {
        if (flags.HasFlag(TextFormatFlags.Bottom)) return StringAlignment.Far;
        if (flags.HasFlag(TextFormatFlags.VerticalCenter)) return StringAlignment.Center;
        return StringAlignment.Near;
    }
}

/// <summary>
/// The theme colour tables are the visual contract of the framework. These tests
/// walk every accessor by reflection so a newly added widget cannot silently ship
/// with a light-mode-only palette.
/// </summary>
public class MetroPaintThemeTests
{
    /// <summary>
    /// Every nested colour accessor: a static method taking a single
    /// <see cref="MetroThemeStyle"/> and returning a <see cref="Color"/>.
    /// </summary>
    internal static IEnumerable<(Type Owner, MethodInfo Method)> EnumerateThemedAccessors(Type type)
    {
        foreach (MethodInfo method in type.GetMethods(BindingFlags.Public |
                                                      BindingFlags.Static |
                                                      BindingFlags.DeclaredOnly))
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (method.ReturnType == typeof(Color) &&
                parameters.Length == 1 &&
                parameters[0].ParameterType == typeof(MetroThemeStyle))
            {
                yield return (type, method);
            }
        }

        foreach (Type nested in type.GetNestedTypes(BindingFlags.Public))
        {
            foreach ((Type Owner, MethodInfo Method) pair in EnumerateThemedAccessors(nested))
            {
                yield return pair;
            }
        }
    }

    public static IEnumerable<object[]> ThemedAccessors()
    {
        return EnumerateThemedAccessors(typeof(MetroPaint))
            .Select(pair => new object[] { pair.Owner, pair.Method.Name });
    }

    [Fact]
    public void ReflectionFindsTheColorTables()
    {
        // Guards the theories below: if the walk ever returned nothing, they would
        // all pass vacuously.
        Assert.True(EnumerateThemedAccessors(typeof(MetroPaint)).Count() > 40);
    }

    [Theory]
    [MemberData(nameof(ThemedAccessors))]
    public void EveryThemedColor_IsOpaqueAndDefinedForEveryTheme(Type owner, string methodName)
    {
        MethodInfo method = owner.GetMethod(methodName, new[] { typeof(MetroThemeStyle) });
        Assert.NotNull(method);

        foreach (MetroThemeStyle theme in Enum.GetValues<MetroThemeStyle>())
        {
            var color = (Color)method.Invoke(null, new object[] { theme });

            Assert.False(color.IsEmpty, owner.Name + "." + methodName + "(" + theme + ") returned Color.Empty.");
            Assert.Equal(255, color.A);
        }
    }

    [Theory]
    [MemberData(nameof(ThemedAccessors))]
    public void EveryThemedColor_TreatsDefaultAsLight(Type owner, string methodName)
    {
        MethodInfo method = owner.GetMethod(methodName, new[] { typeof(MetroThemeStyle) });
        Assert.NotNull(method);

        var light = (Color)method.Invoke(null, new object[] { MetroThemeStyle.Light });
        var fallback = (Color)method.Invoke(null, new object[] { MetroThemeStyle.Default });

        Assert.Equal(light, fallback);
    }

    [Fact]
    public void FormColors_InvertBetweenLightAndDark()
    {
        Assert.Equal(Color.FromArgb(255, 255, 255), MetroPaint.BackColor.Form(MetroThemeStyle.Light));
        Assert.Equal(Color.FromArgb(17, 17, 17), MetroPaint.BackColor.Form(MetroThemeStyle.Dark));
        Assert.Equal(Color.FromArgb(204, 204, 204), MetroPaint.BorderColor.Form(MetroThemeStyle.Light));
        Assert.Equal(Color.FromArgb(68, 68, 68), MetroPaint.BorderColor.Form(MetroThemeStyle.Dark));
    }
}

public class MetroPaintEventArgsTests
{
    [Fact]
    public void CarriesTheValuesItWasGiven()
    {
        using var bitmap = new Bitmap(4, 4);
        using Graphics graphics = Graphics.FromImage(bitmap);

        var args = new MetroPaintEventArgs(Color.Red, Color.Blue, graphics);

        Assert.Equal(Color.Red, args.BackColor);
        Assert.Equal(Color.Blue, args.ForeColor);
        Assert.Same(graphics, args.Graphics);
    }
}
