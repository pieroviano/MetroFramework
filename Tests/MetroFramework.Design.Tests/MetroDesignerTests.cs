using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing.Design;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using MetroFramework.Controls;
using MetroFramework.Design.Controls;

namespace MetroFramework.Design.Tests;

/// <summary>
/// The widgets in MetroFramework.dll point at their designers with
/// <see cref="DesignerAttribute"/> strings that carry a full strong name. Nothing
/// checks those strings at compile time: if the assembly version, the public key
/// token or a type name ever drifts, the designer silently stops being used and
/// the widget falls back to the default one. These tests resolve every such string.
/// </summary>
public class DesignerAttributeTests
{
    private static Assembly MetroFramework => typeof(MetroButton).Assembly;

    public static IEnumerable<object[]> TypesWithADesigner()
    {
        foreach (Type type in MetroFramework.GetTypes().Where(t => t.IsPublic))
        {
            foreach (DesignerAttribute attribute in type.GetCustomAttributes<DesignerAttribute>(inherit: false))
            {
                yield return new object[] { type.FullName, attribute.DesignerTypeName };
            }
        }
    }

    [Fact]
    public void TheScanFindsTheDesignerAttributes()
    {
        // Every widget plus MetroStyleManager.
        Assert.True(TypesWithADesigner().Count() >= 14,
            "Only found " + TypesWithADesigner().Count() + " designer attributes.");
    }

    [Theory]
    [MemberData(nameof(TypesWithADesigner))]
    public void EveryDesignerAttributeResolvesToARealType(string owner, string designerTypeName)
    {
        Type designer = Type.GetType(designerTypeName, throwOnError: false);

        Assert.True(designer != null,
            owner + " points at a designer that cannot be resolved: " + designerTypeName);
    }

    [Theory]
    [MemberData(nameof(TypesWithADesigner))]
    public void EveryDesignerLivesInTheDesignAssembly(string owner, string designerTypeName)
    {
        Type designer = Type.GetType(designerTypeName, throwOnError: false);
        Assert.NotNull(designer);

        Assert.Equal(typeof(MetroButtonDesigner).Assembly, designer.Assembly);
        Assert.True(typeof(IDesigner).IsAssignableFrom(designer),
            owner + "'s designer " + designer.Name + " is not an IDesigner.");
    }

    /// <summary>
    /// MetroTabControl's TabPages property is edited through a custom collection
    /// editor, named the same way and just as unchecked.
    /// </summary>
    public static IEnumerable<object[]> PropertiesWithAnEditor()
    {
        foreach (Type type in MetroFramework.GetTypes().Where(t => t.IsPublic))
        {
            foreach (EditorAttribute attribute in type.GetCustomAttributes<EditorAttribute>(inherit: false))
            {
                yield return new object[] { type.FullName, attribute.EditorTypeName };
            }

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                foreach (EditorAttribute attribute in property.GetCustomAttributes<EditorAttribute>(inherit: false))
                {
                    yield return new object[] { type.FullName + "." + property.Name, attribute.EditorTypeName };
                }
            }
        }
    }

    /// <summary>
    /// Only the editors this repository ships. An editor from System.Design is the
    /// framework's business, and does not resolve on .NET at all.
    /// </summary>
    public static IEnumerable<object[]> PropertiesWithAMetroEditor() =>
        PropertiesWithAnEditor()
            .Where(row => ((string)row[1]).Contains("MetroFramework.Design", StringComparison.Ordinal));

    [Fact]
    public void TheScanFindsTheEditorAttributes()
    {
        Assert.NotEmpty(PropertiesWithAMetroEditor());
    }

    [Theory]
    [MemberData(nameof(PropertiesWithAMetroEditor))]
    public void EveryEditorAttributeResolvesToARealType(string owner, string editorTypeName)
    {
        Type editor = Type.GetType(editorTypeName, throwOnError: false);

        Assert.True(editor != null,
            owner + " points at an editor that cannot be resolved: " + editorTypeName);
        Assert.Equal(typeof(MetroButtonDesigner).Assembly, editor.Assembly);
    }

    [Theory]
    [MemberData(nameof(PropertiesWithAMetroEditor))]
    public void EveryEditorIsAUITypeEditor(string owner, string editorTypeName)
    {
        Type editor = Type.GetType(editorTypeName, throwOnError: false);
        Assert.NotNull(editor);

        Assert.True(typeof(UITypeEditor).IsAssignableFrom(editor),
            owner + "'s editor " + editor.Name + " is not a UITypeEditor.");
    }
}

/// <summary>
/// Each designer hides the inherited Windows Forms properties that the Metro look
/// controls itself, so the property grid does not offer settings that have no
/// effect. These tests drive a real <see cref="DesignSurface"/>, so what they see
/// is what the property grid would show.
/// </summary>
public class DesignTimePropertyFilterTests
{
    /// <summary>
    /// Builds a design surface and runs <paramref name="body"/> against its host.
    /// <para>
    /// This always happens on a dedicated single-threaded apartment thread. A design
    /// surface puts up a BehaviorService adorner window, and creating that window's
    /// handle registers it for drag and drop, which is an OLE call: on a
    /// multi-threaded apartment it throws from inside the window procedure, where no
    /// test can catch it and the process shows a crash dialog instead. The test
    /// runner's worker threads are MTA, so the surface gets a thread of its own.
    /// </para>
    /// </summary>
    private static void WithDesignerHost(Action<IDesignerHost> body)
    {
        Exception failure = null;

        var thread = new System.Threading.Thread(() =>
        {
            DesignSurface surface = null;
            try
            {
                surface = new DesignSurface();
                surface.BeginLoad(typeof(Form));
                var host = (IDesignerHost)surface.GetService(typeof(IDesignerHost));

                body(host);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                try { surface?.Dispose(); } catch { /* teardown must not mask the result */ }
            }
        });

        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(120)), "The design surface thread did not finish.");

        if (failure != null) throw failure;
    }

    private static PropertyDescriptorCollection DesignTimePropertiesOf(IDesignerHost host, Type componentType)
    {
        IComponent component = host.CreateComponent(componentType);
        return TypeDescriptor.GetProperties(component);
    }

    private static string[] Names(PropertyDescriptorCollection properties) =>
        properties.Cast<PropertyDescriptor>().Select(p => p.Name).ToArray();

    [Fact]
    public void TheDesignSurfaceIsUsable()
    {
        // Guards every test below: without a host they would all throw rather than
        // silently pass, but this failure is far easier to read.
        WithDesignerHost(host =>
        {
            Assert.NotNull(host);
            Assert.NotNull(host.RootComponent);
        });
    }

    /// <summary>
    /// Every widget that declares a designer must actually get that designer at
    /// design time. This is the end to end version of
    /// <see cref="DesignerAttributeTests.EveryDesignerAttributeResolvesToARealType"/>:
    /// the attribute string is resolved by the real designer host, so a strong name
    /// that no longer matches shows up here as a silent fallback.
    /// </summary>
    [Fact]
    public void EveryDeclaredDesignerIsTheOneThatGetsUsed()
    {
        WithDesignerHost(host =>
        {
            int checkedWidgets = 0;

            foreach (Type widget in typeof(MetroButton).Assembly.GetTypes())
            {
                if (!widget.IsPublic || widget.IsAbstract) continue;
                if (!typeof(Control).IsAssignableFrom(widget)) continue;
                if (widget.GetConstructor(Type.EmptyTypes) == null) continue;

                var declared = widget.GetCustomAttribute<DesignerAttribute>(inherit: false);
                if (declared == null) continue;

                Type expected = Type.GetType(declared.DesignerTypeName, throwOnError: false);
                Assert.True(expected != null, widget.Name + " declares an unresolvable designer.");

                IComponent component = host.CreateComponent(widget);
                IDesigner actual = host.GetDesigner(component);

                Assert.True(actual != null, widget.Name + " has no designer.");
                Assert.True(expected == actual.GetType(),
                    widget.Name + " declares " + expected.Name + " but the host used " +
                    actual.GetType().FullName + ".");

                checkedWidgets++;
            }

            Assert.True(checkedWidgets >= 12, "Only checked " + checkedWidgets + " widgets.");
        });
    }

    /// <summary>
    /// The Metro look draws its own text, so the inherited font and right-to-left
    /// settings must not be offered on the widgets that paint themselves.
    /// </summary>
    [Fact]
    public void TheFontAndDirectionAreHidden()
    {
        WithDesignerHost(host =>
        {
            foreach (Type widget in new[]
                     {
                         typeof(MetroButton), typeof(MetroCheckBox), typeof(MetroRadioButton),
                         typeof(MetroLabel), typeof(MetroLink), typeof(MetroTile), typeof(MetroScrollBar),
                     })
            {
                PropertyDescriptorCollection properties = DesignTimePropertiesOf(host, widget);

                Assert.True(properties["Font"] == null,
                    widget.Name + " still offers Font: " + string.Join(", ", Names(properties)));
                Assert.True(properties["RightToLeft"] == null, widget.Name + " still offers RightToLeft.");
            }
        });
    }

    [Fact]
    public void TheButtonHidesTheImageAndFlatStyleSettings()
    {
        WithDesignerHost(host =>
        {
            PropertyDescriptorCollection properties = DesignTimePropertiesOf(host, typeof(MetroButton));

            foreach (string hidden in new[]
                     {
                         "Image", "ImageAlign", "ImageIndex", "ImageKey", "ImageList",
                         "TextImageRelation", "FlatAppearance", "FlatStyle",
                         "UseVisualStyleBackColor", "AutoEllipsis", "UseCompatibleTextRendering",
                         "ImeMode", "Padding",
                     })
            {
                Assert.True(properties[hidden] == null, "MetroButton still offers " + hidden + ".");
            }
        });
    }

    [Fact]
    public void TheScrollBarHidesTheColourAndTextSettings()
    {
        WithDesignerHost(host =>
        {
            PropertyDescriptorCollection properties = DesignTimePropertiesOf(host, typeof(MetroScrollBar));

            foreach (string hidden in new[]
                     {
                         "Text", "BackgroundImage", "BackgroundImageLayout", "ForeColor",
                         "BackColor", "ImeMode", "Padding",
                     })
            {
                Assert.True(properties[hidden] == null, "MetroScrollBar still offers " + hidden + ".");
            }
        });
    }

    /// <summary>
    /// Filtering must not take away the Metro settings themselves.
    /// </summary>
    [Fact]
    public void TheMetroSettingsSurvive()
    {
        WithDesignerHost(host =>
        {
            foreach (Type widget in new[]
                     {
                         typeof(MetroButton), typeof(MetroLabel), typeof(MetroTile),
                         typeof(MetroScrollBar), typeof(MetroTabControl),
                     })
            {
                PropertyDescriptorCollection properties = DesignTimePropertiesOf(host, widget);

                Assert.True(properties["Style"] != null, widget.Name + " lost Style.");
                Assert.True(properties["Theme"] != null, widget.Name + " lost Theme.");
                Assert.True(properties["UseStyleColors"] != null, widget.Name + " lost UseStyleColors.");
            }
        });
    }

    [Fact]
    public void TheTabControlUsesTheMetroCollectionEditor()
    {
        WithDesignerHost(host =>
        {
            IComponent tabControl = host.CreateComponent(typeof(MetroTabControl));
            PropertyDescriptor tabPages = TypeDescriptor.GetProperties(tabControl)["TabPages"];

            Assert.NotNull(tabPages);

            var editor = tabPages.GetEditor(typeof(UITypeEditor)) as UITypeEditor;

            Assert.True(editor != null, "TabPages has no UITypeEditor at all.");
            Assert.Equal("MetroTabPageCollectionEditor", editor.GetType().Name);
        });
    }

    [Fact]
    public void TheStyleManagerOffersTheResetVerb()
    {
        WithDesignerHost(host =>
        {
            IComponent manager = host.CreateComponent(typeof(MetroFramework.Components.MetroStyleManager));
            var designer = host.GetDesigner(manager) as ComponentDesigner;

            Assert.NotNull(designer);

            DesignerVerbCollection verbs = designer.Verbs;

            Assert.Single(verbs);
            Assert.Equal("Reset Styles to Default", verbs[0].Text);
        });
    }

    [Fact]
    public void TheTabControlDesignerOffersAddAndRemoveTab()
    {
        WithDesignerHost(host =>
        {
            IComponent tabControl = host.CreateComponent(typeof(MetroTabControl));
            var designer = (ComponentDesigner)host.GetDesigner(tabControl);

            DesignerVerbCollection verbs = designer.Verbs;

            Assert.Equal(2, verbs.Count);
            Assert.Equal("Add Tab", verbs[0].Text);
            Assert.Equal("Remove Tab", verbs[1].Text);
        });
    }

    [Fact]
    public void RemoveTabIsDisabledUntilThereIsATab()
    {
        WithDesignerHost(host =>
        {
            IComponent tabControl = host.CreateComponent(typeof(MetroTabControl));
            var designer = (ComponentDesigner)host.GetDesigner(tabControl);

            Assert.False(designer.Verbs[1].Enabled);

            ((MetroTabControl)tabControl).TabPages.Add(
                (MetroTabPage)host.CreateComponent(typeof(MetroTabPage)));

            Assert.True(designer.Verbs[1].Enabled);
        });
    }
}

public class MetroTabPageCollectionEditorTests
{
    private static object CreateEditor()
    {
        Type type = typeof(MetroButtonDesigner).Assembly
            .GetType("MetroFramework.Design.Controls.MetroTabPageCollectionEditor", throwOnError: true);

        return Activator.CreateInstance(type, new object[] { typeof(TabControl.TabPageCollection) });
    }

    private static object InvokeProtected(object target, string name)
    {
        MethodInfo method = target.GetType().GetMethod(name,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        return method.Invoke(target, null);
    }

    [Fact]
    public void TheCollectionHoldsMetroTabPages()
    {
        object editor = CreateEditor();

        Assert.Equal(typeof(MetroTabPage), InvokeProtected(editor, "CreateCollectionItemType"));
    }

    [Fact]
    public void OnlyMetroTabPagesCanBeAdded()
    {
        object editor = CreateEditor();

        var types = (Type[])InvokeProtected(editor, "CreateNewItemTypes");

        Assert.Equal(new[] { typeof(MetroTabPage) }, types);
    }

    [Fact]
    public void TheEditorIsACollectionEditor()
    {
        Assert.IsAssignableFrom<CollectionEditor>(CreateEditor());
    }
}

public class MetroStyleManagerDesignerTests
{
    private static object CreateDesigner()
    {
        Type type = typeof(MetroButtonDesigner).Assembly
            .GetType("MetroFramework.Design.Components.MetroStyleManagerDesigner", throwOnError: true);

        return Activator.CreateInstance(type);
    }

    [Fact]
    public void OffersAResetVerb()
    {
        object designer = CreateDesigner();
        try
        {
            var verbs = (DesignerVerbCollection)designer.GetType()
                .GetProperty("Verbs", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(designer);

            Assert.Single(verbs);
            Assert.Equal("Reset Styles to Default", verbs[0].Text);
        }
        finally
        {
            ((IDisposable)designer).Dispose();
        }
    }

    [Fact]
    public void TheVerbCollectionIsBuiltOnceAndReused()
    {
        object designer = CreateDesigner();
        try
        {
            PropertyInfo verbs = designer.GetType().GetProperty("Verbs", BindingFlags.Instance | BindingFlags.Public);

            Assert.Same(verbs.GetValue(designer), verbs.GetValue(designer));
        }
        finally
        {
            ((IDisposable)designer).Dispose();
        }
    }

    [Fact]
    public void IsAComponentDesigner()
    {
        object designer = CreateDesigner();
        try
        {
            Assert.IsAssignableFrom<ComponentDesigner>(designer);
        }
        finally
        {
            ((IDisposable)designer).Dispose();
        }
    }
}
