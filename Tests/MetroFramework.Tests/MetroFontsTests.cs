using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using MetroFramework;

namespace MetroFramework.Tests;

/// <summary>
/// <see cref="MetroFonts"/> maps each widget's size/weight pair onto a concrete
/// font. Every widget family uses the same 12/14/18 pixel scale and the same
/// light/regular/bold weights, so the whole matrix is checked by reflection: a new
/// widget family that breaks the scale is caught without a new test.
/// </summary>
public class MetroFontsTests
{
    /// <summary>
    /// The size/weight factory methods: static, returning a Font, taking one
    /// "...Size" enum and one "...Weight" enum.
    /// </summary>
    private static IEnumerable<MethodInfo> FontFactories()
    {
        return typeof(MetroFonts)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m =>
            {
                if (m.ReturnType != typeof(Font)) return false;

                ParameterInfo[] parameters = m.GetParameters();
                return parameters.Length == 2
                       && parameters[0].ParameterType.IsEnum
                       && parameters[1].ParameterType.IsEnum
                       && parameters[0].ParameterType.Name.EndsWith("Size", StringComparison.Ordinal)
                       && parameters[1].ParameterType.Name.EndsWith("Weight", StringComparison.Ordinal);
            })
            .OrderBy(m => m.Name, StringComparer.Ordinal);
    }

    public static IEnumerable<object[]> Factories() =>
        FontFactories().Select(m => new object[] { m.Name });

    [Fact]
    public void TheScanFindsTheWidgetFontFamilies()
    {
        // Button, CheckBox, ComboBox, Label, Link, ProgressBar, TabControl,
        // TextBox and Tile.
        Assert.True(FontFactories().Count() >= 8,
            "Only found: " + string.Join(", ", FontFactories().Select(m => m.Name)));
    }

    private static MethodInfo Factory(string name) => FontFactories().Single(m => m.Name == name);

    [Theory]
    [MemberData(nameof(Factories))]
    public void EveryCombinationResolvesToAFont(string factoryName)
    {
        MethodInfo factory = Factory(factoryName);
        Type sizeType = factory.GetParameters()[0].ParameterType;
        Type weightType = factory.GetParameters()[1].ParameterType;

        foreach (object size in Enum.GetValues(sizeType))
        {
            foreach (object weight in Enum.GetValues(weightType))
            {
                using var font = (Font)factory.Invoke(null, new[] { size, weight });

                Assert.NotNull(font);
                Assert.Equal(GraphicsUnit.Pixel, font.Unit);
                Assert.True(font.Size > 0f);
            }
        }
    }

    private static float SizeOf(MethodInfo factory, string size, string weight)
    {
        object sizeValue = Enum.Parse(factory.GetParameters()[0].ParameterType, size);
        object weightValue = Enum.Parse(factory.GetParameters()[1].ParameterType, weight);

        using var font = (Font)factory.Invoke(null, new[] { sizeValue, weightValue });
        return font.Size;
    }

    /// <summary>
    /// Whatever the absolute sizes, Small &lt; Medium &lt; Tall must hold for every
    /// widget family, and the weight must not change the size.
    /// </summary>
    [Theory]
    [MemberData(nameof(Factories))]
    public void TheSizeScaleIncreasesAndIsIndependentOfTheWeight(string factoryName)
    {
        MethodInfo factory = Factory(factoryName);

        float small = SizeOf(factory, "Small", "Regular");
        float medium = SizeOf(factory, "Medium", "Regular");
        float tall = SizeOf(factory, "Tall", "Regular");

        Assert.True(small < medium, factoryName + ": Small (" + small + ") is not below Medium (" + medium + ")");
        Assert.True(medium < tall, factoryName + ": Medium (" + medium + ") is not below Tall (" + tall + ")");

        foreach (string size in new[] { "Small", "Medium", "Tall" })
        {
            Assert.Equal(SizeOf(factory, size, "Regular"), SizeOf(factory, size, "Light"));
            Assert.Equal(SizeOf(factory, size, "Regular"), SizeOf(factory, size, "Bold"));
        }
    }

    /// <summary>
    /// Text widgets share a 12/14/18 pixel scale; buttons sit one step smaller so
    /// their caption fits the chrome.
    /// </summary>
    [Theory]
    [InlineData("Label", 12f, 14f, 18f)]
    [InlineData("Link", 12f, 14f, 18f)]
    [InlineData("Tile", 12f, 14f, 18f)]
    [InlineData("ComboBox", 12f, 14f, 18f)]
    [InlineData("TextBox", 12f, 14f, 18f)]
    [InlineData("ProgressBar", 12f, 14f, 18f)]
    [InlineData("TabControl", 12f, 14f, 18f)]
    [InlineData("CheckBox", 12f, 14f, 18f)]
    [InlineData("Button", 11f, 13f, 16f)]
    public void TheScaleIsTheDocumentedOne(string factoryName, float small, float medium, float tall)
    {
        MethodInfo factory = Factory(factoryName);

        Assert.Equal(small, SizeOf(factory, "Small", "Regular"));
        Assert.Equal(medium, SizeOf(factory, "Medium", "Regular"));
        Assert.Equal(tall, SizeOf(factory, "Tall", "Regular"));
    }

    [Theory]
    [MemberData(nameof(Factories))]
    public void OnlyTheBoldWeightIsBold(string factoryName)
    {
        MethodInfo factory = Factory(factoryName);
        Type sizeType = factory.GetParameters()[0].ParameterType;
        Type weightType = factory.GetParameters()[1].ParameterType;

        object medium = Enum.Parse(sizeType, "Medium");

        foreach (object weight in Enum.GetValues(weightType))
        {
            using var font = (Font)factory.Invoke(null, new[] { medium, weight });

            Assert.Equal(weight.ToString() == "Bold", font.Bold);
        }
    }

    [Theory]
    [MemberData(nameof(Factories))]
    public void AnUndefinedCombinationStillYieldsAUsableFont(string factoryName)
    {
        MethodInfo factory = Factory(factoryName);
        Type sizeType = factory.GetParameters()[0].ParameterType;
        Type weightType = factory.GetParameters()[1].ParameterType;

        object size = Enum.ToObject(sizeType, 99);
        object weight = Enum.ToObject(weightType, 99);

        using var font = (Font)factory.Invoke(null, new[] { size, weight });

        Assert.NotNull(font);
        Assert.True(font.Size > 0f);
    }

    [Theory]
    [InlineData(8f)]
    [InlineData(12f)]
    [InlineData(24f)]
    public void TheBaseFacesHonourTheRequestedSize(float size)
    {
        using Font light = MetroFonts.DefaultLight(size);
        using Font regular = MetroFonts.Default(size);
        using Font bold = MetroFonts.DefaultBold(size);

        foreach (Font font in new[] { light, regular, bold })
        {
            Assert.Equal(size, font.Size);
            Assert.Equal(GraphicsUnit.Pixel, font.Unit);
        }

        Assert.False(light.Bold);
        Assert.False(regular.Bold);
        Assert.True(bold.Bold);
    }

    [Fact]
    public void TheDocumentFontsUseTheDocumentedSizes()
    {
        using Font title = MetroFonts.Title;
        using Font subtitle = MetroFonts.Subtitle;
        using Font tileCount = MetroFonts.TileCount;

        Assert.Equal(24f, title.Size);
        Assert.Equal(14f, subtitle.Size);
        Assert.Equal(44f, tileCount.Size);
    }

    [Fact]
    public void EachCallHandsBackAnIndependentFont()
    {
        Font first = MetroFonts.Default(12f);
        Font second = MetroFonts.Default(12f);

        Assert.NotSame(first, second);

        // Disposing one must not disturb the other.
        first.Dispose();
        Assert.Equal(12f, second.Size);
        second.Dispose();
    }
}
