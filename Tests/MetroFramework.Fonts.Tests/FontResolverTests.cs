using System;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Reflection;
using MetroFramework;
using MetroFramework.Fonts;

namespace MetroFramework.Fonts.Tests;

/// <summary>
/// <see cref="FontResolver"/> substitutes the bundled Open Sans faces for Segoe UI
/// on machines that do not have it, so a Metro application looks the same
/// everywhere. It is discovered by MetroFramework.dll at runtime, by name, through
/// the internal IMetroFontResolver interface.
/// </summary>
public class FontResolverTests
{
    private static readonly string[] BundledFaces =
    {
        "MetroFramework.Fonts.Resources.Open_Sans.ttf",
        "MetroFramework.Fonts.Resources.Open_Sans_Light.ttf",
        "MetroFramework.Fonts.Resources.Open_Sans_Bold.ttf",
    };

    [Fact]
    public void TheFontFilesAreEmbeddedInTheAssembly()
    {
        string[] resources = typeof(FontResolver).Assembly.GetManifestResourceNames();

        foreach (string face in BundledFaces)
        {
            Assert.Contains(face, resources);
        }
    }

    [Fact]
    public void TheEmbeddedFontFilesAreNotEmpty()
    {
        foreach (string face in BundledFaces)
        {
            using System.IO.Stream stream = typeof(FontResolver).Assembly.GetManifestResourceStream(face);

            Assert.NotNull(stream);
            Assert.True(stream.Length > 1000, face + " is only " + stream.Length + " bytes.");
        }
    }

    /// <summary>
    /// MetroFramework.dll looks the resolver up by the assembly-qualified name held
    /// in its AssemblyRef, so the type name, namespace, assembly name, version and
    /// public key token all have to line up. Nothing checks that at compile time.
    /// </summary>
    [Fact]
    public void MetroFrameworkCanFindTheResolverByName()
    {
        string name = (string)typeof(AssemblyRef)
            .GetField("MetroFrameworkFontResolver", BindingFlags.NonPublic | BindingFlags.Static)
            .GetRawConstantValue();

        Type resolved = Type.GetType(name, throwOnError: false);

        Assert.True(resolved != null, "MetroFramework cannot resolve its font resolver: " + name);
        Assert.Equal(typeof(FontResolver), resolved);
    }

    [Fact]
    public void TheResolverImplementsTheInterfaceMetroFrameworkAsksFor()
    {
        Type contract = typeof(MetroFonts).GetNestedType("IMetroFontResolver",
            BindingFlags.NonPublic | BindingFlags.Public);

        Assert.NotNull(contract);
        Assert.True(contract.IsAssignableFrom(typeof(FontResolver)),
            "FontResolver does not implement MetroFonts.IMetroFontResolver.");
    }

    [Fact]
    public void TheResolverHasAParameterlessConstructor()
    {
        // MetroFonts creates it with Activator.CreateInstance.
        Assert.NotNull(typeof(FontResolver).GetConstructor(Type.EmptyTypes));
        Assert.NotNull(Activator.CreateInstance(typeof(FontResolver)));
    }

    [Theory]
    [InlineData("Segoe UI", FontStyle.Regular)]
    [InlineData("Segoe UI", FontStyle.Bold)]
    [InlineData("Segoe UI Light", FontStyle.Regular)]
    [InlineData("Segoe UI Light", FontStyle.Bold)]
    public void TheMetroFacesAlwaysResolveToSomething(string family, FontStyle style)
    {
        var resolver = new FontResolver();

        using Font font = resolver.ResolveFont(family, 12f, style, GraphicsUnit.Pixel);

        Assert.NotNull(font);
        Assert.Equal(12f, font.Size);
        Assert.Equal(GraphicsUnit.Pixel, font.Unit);
    }

    [Theory]
    [InlineData(8f)]
    [InlineData(12f)]
    [InlineData(24f)]
    [InlineData(44f)]
    public void TheRequestedSizeIsHonoured(float size)
    {
        var resolver = new FontResolver();

        using Font font = resolver.ResolveFont("Segoe UI", size, FontStyle.Regular, GraphicsUnit.Pixel);

        Assert.Equal(size, font.Size);
    }

    [Theory]
    [InlineData(GraphicsUnit.Pixel)]
    [InlineData(GraphicsUnit.Point)]
    public void TheRequestedUnitIsHonoured(GraphicsUnit unit)
    {
        var resolver = new FontResolver();

        using Font font = resolver.ResolveFont("Segoe UI", 12f, FontStyle.Regular, unit);

        Assert.Equal(unit, font.Unit);
    }

    [Fact]
    public void ABoldRequestComesBackBold()
    {
        var resolver = new FontResolver();

        using Font font = resolver.ResolveFont("Segoe UI", 12f, FontStyle.Bold, GraphicsUnit.Pixel);

        Assert.True(font.Bold);
    }

    /// <summary>
    /// A family the resolver knows nothing about is passed straight through to GDI+,
    /// which substitutes on its own; the resolver must not interfere.
    /// </summary>
    [Theory]
    [InlineData("Arial")]
    [InlineData("Courier New")]
    [InlineData("No Such Font At All")]
    public void AnUnrelatedFamilyIsLeftAlone(string family)
    {
        var resolver = new FontResolver();

        using Font font = resolver.ResolveFont(family, 12f, FontStyle.Regular, GraphicsUnit.Pixel);

        Assert.NotNull(font);
        Assert.Equal(12f, font.Size);
    }

    [Fact]
    public void ResolvingRepeatedlyKeepsWorking()
    {
        var resolver = new FontResolver();

        for (int i = 0; i < 25; i++)
        {
            using Font light = resolver.ResolveFont("Segoe UI Light", 12f, FontStyle.Regular, GraphicsUnit.Pixel);
            using Font regular = resolver.ResolveFont("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Pixel);
            using Font bold = resolver.ResolveFont("Segoe UI", 12f, FontStyle.Bold, GraphicsUnit.Pixel);

            Assert.NotNull(light);
            Assert.NotNull(regular);
            Assert.NotNull(bold);
        }
    }

    [Fact]
    public void TwoResolversDoNotInterfereWithEachOther()
    {
        var first = new FontResolver();
        var second = new FontResolver();

        using Font a = first.ResolveFont("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Pixel);
        using Font b = second.ResolveFont("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Pixel);

        Assert.Equal(a.FontFamily.Name, b.FontFamily.Name);
    }

    /// <summary>
    /// When Segoe UI is missing the substitution has to actually happen, otherwise
    /// this package does nothing. The check is conditional because a developer
    /// machine normally does have Segoe UI installed, in which case the resolver
    /// correctly leaves it alone.
    /// </summary>
    [Fact]
    public void SegoeUiIsSubstitutedOnlyWhenItIsMissing()
    {
        bool segoeInstalled;
        using (var installed = new InstalledFontCollection())
        {
            segoeInstalled = installed.Families.Any(f =>
                string.Equals(f.Name, "Segoe UI", StringComparison.OrdinalIgnoreCase));
        }

        var resolver = new FontResolver();
        using Font font = resolver.ResolveFont("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Pixel);

        if (segoeInstalled)
        {
            Assert.Equal("Segoe UI", font.Name);
        }
        else
        {
            Assert.Contains("Open Sans", font.FontFamily.Name);
        }
    }
}

/// <summary>
/// The whole point of the package: once it is loadable, the fonts MetroFramework
/// hands to its widgets come through the resolver.
/// </summary>
public class MetroFontsIntegrationTests
{
    [Fact]
    public void MetroFontsStillProducesUsableFonts()
    {
        using Font title = MetroFonts.Title;
        using Font subtitle = MetroFonts.Subtitle;
        using Font label = MetroFonts.Label(MetroLabelSize.Medium, MetroLabelWeight.Regular);

        Assert.Equal(24f, title.Size);
        Assert.Equal(14f, subtitle.Size);
        Assert.Equal(14f, label.Size);
    }

    [Fact]
    public void EveryLabelCombinationResolves()
    {
        foreach (MetroLabelSize size in Enum.GetValues<MetroLabelSize>())
        {
            foreach (MetroLabelWeight weight in Enum.GetValues<MetroLabelWeight>())
            {
                using Font font = MetroFonts.Label(size, weight);

                Assert.NotNull(font);
                Assert.True(font.Size > 0f);
            }
        }
    }
}
