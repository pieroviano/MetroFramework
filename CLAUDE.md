# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A maintained fork of [MetroFramework](https://github.com/viperneo/winforms-modernui) (Modern UI
controls for Windows Forms), repackaged for current .NET while keeping the original public API.
Three NuGet packages ship from here:

| Project | Package id | Role |
| --- | --- | --- |
| `MetroFramework` | `Net4x.MetroFramework.Library` | The controls, forms, theming and an embedded HTML/CSS renderer |
| `MetroFramework.Design` | `Net4x.MetroFramework.Design` | Design-time only; loaded by the Windows Forms designer |
| `MetroFramework.Fonts` | `Net4x.MetroFramework.Fonts` | Embedded Open Sans, substituted when Segoe UI is missing |

`MetroFramework.Demo` is a sample app, not shipped. All projects multi-target
`net45;net6.0-windows;net8.0-windows;net10.0-windows` and are Windows-only.

## Commands

```bash
# build one target framework (much faster than all four)
dotnet build MetroFramework/MetroFramework.csproj -c Debug -f net10.0-windows

# all tests (1290 of them, a few seconds)
dotnet test MetroFramework.sln -c Debug

# one project / one test / one class
dotnet test MetroFramework.Tests/MetroFramework.Tests.csproj -c Debug
dotnet test MetroFramework.Tests/MetroFramework.Tests.csproj --filter "FullyQualifiedName~CssLengthTests"
dotnet test MetroFramework.Tests/MetroFramework.Tests.csproj --filter "FullyQualifiedName~GetStringFormat_AgreesWith"

# why did it fail
dotnet test <project> --logger "console;verbosity=detailed"

# coverage (coverlet is already referenced by every test project)
dotnet test MetroFramework.sln -c Debug --collect:"XPlat Code Coverage"
```

`dotnet pack` runs on every build (`GeneratePackageOnBuild`), writing to `Packages/`, which is a
symlink to a shared local feed. `Nuget.Config` lists that folder as a package source, so a build
here can be consumed by a sibling repository straight away.

There is no linter, no `.editorconfig` and no analyzer configuration. The build is warning-noisy
(~2500 CA1416 platform-compatibility warnings from the GDI+ code); treat the warning count as
background, and only new **errors** as signal.

## Things that will bite you

**The `Drawing/Html` sources are Latin-1, not UTF-8.** All 25 files under
`MetroFramework/Drawing/Html/` carry a non-UTF-8 byte in the original CodeProject licence header,
and they use CRLF. Read and write them with `latin-1` (or a byte-preserving editor); a script that
assumes UTF-8 will throw, and one that "fixes" the encoding will rewrite all 25 files. Everything
else in the repo is UTF-8 with a BOM and CRLF.

**`AssemblyVersion` is pinned at 1.3.0.0 and is load-bearing.** `MetroFrameworkAssembly.Version` in
`MetroFramework/Properties/AssemblyInfo.cs` feeds both `[assembly: AssemblyVersion]` *and* the
strong-name strings in `MetroFramework.AssemblyRef`. Those strings are how the library finds its
companions at runtime and design time:

* every control's `[Designer("MetroFramework.Design.Controls.XxxDesigner, " + AssemblyRef.MetroFrameworkDesignSN)]`
* `MetroTabControl`'s `[Editor(...)]` for `TabPages`
* `MetroFonts`'s static constructor, which does `Type.GetType(AssemblyRef.MetroFrameworkFontResolver)`

None of that is checked by the compiler. Change the assembly version, the public key, a namespace
or a type name on one side only and the designer silently falls back to the stock one, or the font
resolver silently does not load — no error anywhere. The package version (`1.4.0.<yyddd>`, from
`Directory.Build.props`) is deliberately *not* the assembly version; do not "align" them.
`MetroFramework.Design.Tests` and `MetroFramework.Fonts.Tests` exist mainly to catch this drift.

**`Controls/MetroTilePart.cs` is excluded from compilation** (`<Compile Remove>` in the csproj).
It is not dead code you can rely on, and not code you can edit and expect to run.

**Test assemblies are signed and are friends.** All three test projects sign with
`MetroFramework.snk` and are declared via `InternalsVisibleTo` in `AssemblyRef`
(`MetroFrameworkTestsIVT` and friends). That is what lets tests reach `Parser`, `MetroLocalize`,
`MetroDefaults`, `CssBoxWordSplitter` and every type in `MetroFramework.Design`, all of which are
`internal`. If you add a test project, it needs the same key and a matching IVT entry.

## Architecture

### Theming: `Default` means "inherit"

Every control implements `IMetroControl` (forms implement `IMetroForm`, components
`IMetroComponent`) and exposes `Style` (accent colour) and `Theme` (light/dark). The backing field
starts at `MetroColorStyle.Default` / `MetroThemeStyle.Default`, and **the getter resolves it**:

```
DesignMode or explicitly set  -> the stored value
StyleManager != null          -> the manager's value
otherwise                     -> MetroDefaults.Style / MetroDefaults.Theme
```

So the getter never returns `Default`, and a control's stored value and its observable value
differ. Two consequences worth remembering:

* `[DefaultValue(MetroColorStyle.Default)]` on those properties does not match what the getter
  returns. That is intentional, and it is why the designer needs
  `DesignerSerializationVisibility.Hidden` in places rather than a corrected `DefaultValue`.
  Everywhere *else*, a `[DefaultValue]` that disagrees with a freshly constructed control is a
  bug — `MetroDesignerDefaultValueTests` enforces that.
* Tests that assert "the default style" must compare against `MetroDefaults.Style`, not `Default`.

`MetroStyleManager.Owner` is the fan-out point: assigning it walks the whole control tree —
nested containers, `TabControl.TabPages`, and `ContextMenuStrip` — assigning itself to everything
that implements a Metro interface, and it subscribes to `ControlAdded` so later additions are
covered too. It implements `ISupportInitialize`, and while `BeginInit`/`EndInit` are in flight all
of that is deferred, because designer-generated `InitializeComponent` would otherwise fan out over
a half-built tree.

`MetroStyleExtender` is the escape hatch for non-Metro controls: an `IExtenderProvider` that adds
an *ApplyMetroTheme* property to any plain `Control` and pushes `BackColor`/`ForeColor` onto it.

### Painting

Controls are `UserPaint` and draw themselves. Colours come from `MetroPaint`, which is a static
lookup table nested as `MetroPaint.<Layer>.<Widget>.<State>(theme)` — e.g.
`MetroPaint.BackColor.Button.Hover(theme)`, `MetroPaint.BorderColor.Form(theme)`. Every accessor
takes a `MetroThemeStyle` and treats `Default` as `Light`. `MetroBrushes` and `MetroPens` cache one
instance per palette colour and hand out **clones**, so callers may dispose or mutate what they get.

Each control raises `CustomPaintBackground` / `CustomPaint` / `CustomPaintForeground` during its
paint cycle. Note the optimisation in every `OnPaintBackground`: when the resolved background is
fully opaque the control calls `Graphics.Clear` and returns *early*, so the custom-paint events are
only reached on the translucent path.

`MetroPaint.GetStringFormat` (GDI+) and `MetroPaint.GetTextFormatFlags` (TextRenderer) must describe
the same layout for the same `ContentAlignment`; `Alignment` is the horizontal axis and
`LineAlignment` the vertical one. The library itself only uses the `TextFormatFlags` one.

### The HTML renderer

`MetroFramework/Drawing/Html/` is a vendored, self-contained HTML/CSS renderer (~9.4k lines,
originally by Jose Menendez Poo) used by `HtmlLabel`, `HtmlPanel` and `HtmlToolTip`. It is
independent of the theming system. The pipeline is:

```
InitialContainer(html)   parse document + cascade the default stylesheet
  -> CssBox tree         one box per element; CSS properties are plain string properties
                         tagged [CssProperty("name")] and discovered by reflection into
                         CssBox._properties
  -> CssLayoutEngine     line boxes, word splitting (CssBoxWordSplitter)
  -> CssTable            table layout
  -> Paint(Graphics)
```

`Parser` holds every regular expression as a `const string`, composed by concatenation
(`CssFontSize` embeds `CssLength`, and so on). Two rules matter when touching them: regex
alternation is **first-match-wins**, so the more specific alternative has to come first (`bolder`
before `bold`, `CssLength` before `CssNumber`); and these patterns are used with
`Parser.Search`, which returns the first match rather than requiring a full match, so a
too-permissive pattern silently truncates a value instead of failing.

Style sheets and documents are untrusted input. `CssValue.GetActualColor`, `GetImage` and
`GetStyleSheet` report "cannot resolve" through their return value (`Color.Empty` / `null`) and
must not throw — the whole render is inside a control's `OnPaint`.

### Native interop and windowing

`MetroFramework/Native/` wraps the Win32 calls the look depends on (`DwmApi` for glass,
`SubClass` for message interception, `WinApi`, `WinCaret`, `Taskbar`). `MetroForm` draws its own
title bar and border and creates separate top-level shadow windows
(`MetroAeroDropShadow` / `MetroFlatDropShadow` / `MetroRealisticDropShadow`). `MetroTaskWindow` is
a singleton overlay driven by `DelayedCall`.

### Animation

`AnimationBase` + `DelayedCall` (a vendored timer helper by Yves Goergen). **`DelayedCall` captures
`SynchronizationContext.Current` and marshals every step back onto it**, throwing
`InvalidOperationException` if there is none. Constructing any `Control` installs a
`WindowsFormsSynchronizationContext` on the thread, so in practice animations require a running
Windows Forms message loop — they will start and then silently never tick without one. Do not try
to drive them to completion in a test.

## Testing

xunit, one test project per shipped project, ~1290 tests, all passing. Coverage sits around 66% of
lines in `MetroFramework`.

The suites lean on assembly scanning rather than one test per control: `MetroControls.ControlTypes`
finds every concrete `IMetroControl` by reflection, so a newly added widget is automatically
covered by the contract, paint, input and designer-default theories. Each such scan is paired with
a guard test (`TheScanFindsTheWidgets`, `ReflectionFindsTheColorTables`, …) so the theories cannot
pass vacuously if the scan ever returns nothing.

Painting is exercised offscreen: `MetroControls.PaintOffscreen` invokes the control's protected
`OnPaintBackground`/`OnPaint` against a `Graphics` from a `Bitmap`, in both themes, all 15 palette
colours, disabled, at a 1×1 size, and after synthesised mouse/keyboard/focus events. These assert
that the paint path completes rather than checking pixels — that is where colour-table and geometry
mistakes surface.

Two environment constraints, both already handled, both worth not undoing:

* **`DesignSurface` work must run on an STA thread.** A design surface puts up a
  `BehaviorService` adorner window whose handle creation registers it for drag-and-drop, an OLE
  call. On xunit's MTA worker threads that throws from inside the window procedure, where no test
  can catch it — the process shows a crash dialog and the run may still report green.
  `DesignTimePropertyFilterTests.WithDesignerHost` creates the surface on its own STA thread; use
  it for anything new that touches a designer host.
* **Animations need a message pump**, per the note above, so `AnimationLifecycleTests` covers the
  easing curves directly (via a subclass that exposes the protected `MakeTransition`) and the
  documented "no synchronisation context" failure, rather than running an animation.

`MetroFramework.Design.Tests` deliberately tests through a real `DesignSurface` and
`TypeDescriptor.GetProperties` — i.e. what the property grid would actually show — instead of
calling `PreFilterProperties` directly, which needs an initialised designer.

## Conventions

Property attributes use the framework's own category constants:
`[Category(MetroDefaults.PropertyCategory.Appearance)]` / `.Behaviour`.

`MetroToggle` gets its on/off caption from XML embedded under `Localization/<lang>/<ControlName>.xml`
via the internal `MetroLocalize`, keyed on the two-letter language of the current culture with `en`
as fallback. A missing key comes back as `"~key"`, so a literal tilde in the UI means the lookup
failed. `MetroLocalize` resolves the resource against `Assembly.GetCallingAssembly()`, so it only
finds resources when called from inside `MetroFramework.dll`.

Git history on this repo uses terse or empty commit messages; the remote is Bitbucket, and the
default branch is `master`.
