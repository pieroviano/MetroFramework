using System;
using System.Drawing;
using MetroFramework;
using MetroFramework.Drawing;

namespace MetroFramework.Tests;

/// <summary>
/// The Metro palette is a fixed set of brand colours. These tests pin the exact
/// ARGB values and verify that the colour / brush / pen accessors all agree.
/// </summary>
public class MetroColorsTests
{
    public static TheoryData<MetroColorStyle, int, int, int> Palette => new()
    {
        { MetroColorStyle.Black,   0,   0,   0   },
        { MetroColorStyle.White,   255, 255, 255 },
        { MetroColorStyle.Silver,  85,  85,  85  },
        { MetroColorStyle.Blue,    0,   174, 219 },
        { MetroColorStyle.Green,   0,   177, 89  },
        { MetroColorStyle.Lime,    142, 188, 0   },
        { MetroColorStyle.Teal,    0,   170, 173 },
        { MetroColorStyle.Orange,  243, 119, 53  },
        { MetroColorStyle.Brown,   165, 81,  0   },
        { MetroColorStyle.Pink,    231, 113, 189 },
        { MetroColorStyle.Magenta, 255, 0,   148 },
        { MetroColorStyle.Purple,  124, 65,  153 },
        { MetroColorStyle.Red,     209, 17,  65  },
        { MetroColorStyle.Yellow,  255, 196, 37  },
    };

    [Theory]
    [MemberData(nameof(Palette))]
    public void GetStyleColor_ReturnsThePaletteEntry(MetroColorStyle style, int r, int g, int b)
    {
        Color actual = MetroPaint.GetStyleColor(style);

        Assert.Equal(r, actual.R);
        Assert.Equal(g, actual.G);
        Assert.Equal(b, actual.B);
        Assert.Equal(255, actual.A);
    }

    [Fact]
    public void GetStyleColor_FallsBackToBlueForDefault()
    {
        Assert.Equal(MetroColors.Blue, MetroPaint.GetStyleColor(MetroColorStyle.Default));
    }

    [Fact]
    public void GetStyleColor_FallsBackToBlueForAnUndefinedStyle()
    {
        Assert.Equal(MetroColors.Blue, MetroPaint.GetStyleColor((MetroColorStyle)9999));
    }

    [Theory]
    [MemberData(nameof(Palette))]
    public void GetStyleBrush_MatchesGetStyleColor(MetroColorStyle style, int r, int g, int b)
    {
        using SolidBrush brush = MetroPaint.GetStyleBrush(style);

        Assert.Equal(Color.FromArgb(r, g, b), brush.Color);
        Assert.Equal(MetroPaint.GetStyleColor(style), brush.Color);
    }

    [Theory]
    [MemberData(nameof(Palette))]
    public void GetStylePen_MatchesGetStyleColor(MetroColorStyle style, int r, int g, int b)
    {
        using Pen pen = MetroPaint.GetStylePen(style);

        Assert.Equal(Color.FromArgb(r, g, b), pen.Color);
        Assert.Equal(MetroPaint.GetStyleColor(style), pen.Color);
        Assert.Equal(1f, pen.Width);
    }

    [Fact]
    public void GetStyleBrushAndPen_FallBackToBlueForDefault()
    {
        using SolidBrush brush = MetroPaint.GetStyleBrush(MetroColorStyle.Default);
        using Pen pen = MetroPaint.GetStylePen(MetroColorStyle.Default);

        Assert.Equal(MetroColors.Blue, brush.Color);
        Assert.Equal(MetroColors.Blue, pen.Color);
    }

    [Fact]
    public void EveryDeclaredStyleExceptDefaultHasItsOwnColor()
    {
        var seen = new System.Collections.Generic.HashSet<Color>();

        foreach (MetroColorStyle style in Enum.GetValues<MetroColorStyle>())
        {
            if (style == MetroColorStyle.Default) continue;

            Assert.True(seen.Add(MetroPaint.GetStyleColor(style)),
                $"{style} does not have a distinct palette colour.");
        }
    }
}

/// <summary>
/// <see cref="MetroBrushes"/> and <see cref="MetroPens"/> cache one instance per
/// colour and hand out clones. Callers must be able to dispose or mutate what they
/// get back without corrupting the cache for everyone else.
/// </summary>
public class MetroBrushesAndPensTests
{
    [Fact]
    public void Brushes_HandOutIndependentClones()
    {
        SolidBrush first = MetroBrushes.Blue;
        SolidBrush second = MetroBrushes.Blue;

        Assert.NotSame(first, second);
        Assert.Equal(MetroColors.Blue, first.Color);
        Assert.Equal(MetroColors.Blue, second.Color);
    }

    [Fact]
    public void Brushes_DisposingAClone_DoesNotPoisonTheCache()
    {
        MetroBrushes.Red.Dispose();

        using SolidBrush next = MetroBrushes.Red;
        Assert.Equal(MetroColors.Red, next.Color);
    }

    [Fact]
    public void Brushes_MutatingAClone_DoesNotPoisonTheCache()
    {
        using (SolidBrush mutated = MetroBrushes.Green)
        {
            mutated.Color = Color.HotPink;
        }

        using SolidBrush next = MetroBrushes.Green;
        Assert.Equal(MetroColors.Green, next.Color);
    }

    [Fact]
    public void Pens_HandOutIndependentClones()
    {
        Pen first = MetroPens.Orange;
        Pen second = MetroPens.Orange;

        Assert.NotSame(first, second);
        Assert.Equal(MetroColors.Orange, first.Color);
        Assert.Equal(MetroColors.Orange, second.Color);
    }

    [Fact]
    public void Pens_DisposingAClone_DoesNotPoisonTheCache()
    {
        MetroPens.Teal.Dispose();

        using Pen next = MetroPens.Teal;
        Assert.Equal(MetroColors.Teal, next.Color);
    }

    [Fact]
    public void Pens_MutatingAClone_DoesNotPoisonTheCache()
    {
        using (Pen mutated = MetroPens.Purple)
        {
            mutated.Width = 42f;
        }

        using Pen next = MetroPens.Purple;
        Assert.Equal(1f, next.Width);
    }
}
