# Net4x.MetroFramework.Library

Modern UI ("Metro") controls for Windows Forms — a themed control set, a light and a
dark theme, and a borderless form that draws its own chrome.

This is a maintained fork of [MetroFramework](https://github.com/viperneo/winforms-modernui)
by Sven Walter, repackaged for current .NET while keeping the original API.

## Install

```
dotnet add package Net4x.MetroFramework.Library
```

Two companion packages are optional:

| Package | What it adds |
| --- | --- |
| `Net4x.MetroFramework.Design` | Visual Studio designer support: property filtering, tab page editing, the *Reset Styles to Default* verb |
| `Net4x.MetroFramework.Fonts` | Bundled Open Sans faces, used automatically when Segoe UI is missing |

## Target frameworks

`net45`, `net6.0-windows`, `net8.0-windows`, `net10.0-windows`. Windows only — it is
built on Windows Forms and GDI+.

## Getting started

Derive your form from `MetroForm` and drop Metro controls onto it:

```csharp
using MetroFramework.Controls;
using MetroFramework.Forms;

public class MainForm : MetroForm
{
    public MainForm()
    {
        Text = "Dashboard";
        Style = MetroColorStyle.Teal;
        Theme = MetroThemeStyle.Dark;

        var save = new MetroButton
        {
            Text = "Save",
            Location = new Point(20, 80),
            Highlight = true
        };
        save.Click += (_, _) => Save();

        Controls.Add(save);
    }
}
```

`MetroForm` paints its own title bar and border, so set the caption through `Text`
and the chrome through `BorderStyle`, `DisplayHeader`, `Movable`, `Resizable` and
`ShadowType` rather than the inherited `FormBorderStyle`.

## Theming

Every control carries `Style` (the accent colour) and `Theme` (light or dark).
Leaving either at `Default` means *inherit*, so you normally set them in one place.

```csharp
var styleManager = new MetroStyleManager
{
    Style = MetroColorStyle.Orange,
    Theme = MetroThemeStyle.Dark,
    Owner = this          // applies to this form and everything inside it
};
```

`MetroStyleManager` walks the whole control tree — including nested containers, tab
pages and context menus — and keeps applying itself to controls added later. A
control that sets `Style` or `Theme` explicitly keeps its own value; one left on
`Default` follows the manager.

The accent colours are `Black`, `White`, `Silver`, `Blue` (the default), `Green`,
`Lime`, `Teal`, `Orange`, `Brown`, `Pink`, `Magenta`, `Purple`, `Red` and `Yellow`.
`MetroColors` exposes them directly, and `MetroPaint.GetStyleColor(style)` maps an
enum value onto one.

Set `UseStyleColors` on a control to paint its text in the accent colour, or
`UseCustomBackColor` / `UseCustomForeColor` to take over its colours entirely.

To theme controls that are not part of this library, use `MetroStyleExtender`: it
adds an *ApplyMetroTheme* property to every plain Windows Forms control on the form.

## Controls

**Input** — `MetroButton`, `MetroCheckBox`, `MetroRadioButton`, `MetroToggle`,
`MetroComboBox`, `MetroTextBox`, `MetroTrackBar`, `MetroLink`

**Display** — `MetroLabel`, `MetroTile`, `MetroProgressBar`, `MetroProgressSpinner`

**Layout** — `MetroPanel`, `MetroTabControl`, `MetroTabPage`, `MetroUserControl`,
`MetroScrollBar`

**Forms** — `MetroForm`, `MetroTaskWindow`, `MetroMessageBox`

**Components** — `MetroStyleManager`, `MetroStyleExtender`, `MetroToolTip`

**HTML rendering** — `HtmlLabel`, `HtmlPanel`, `HtmlToolTip` render a subset of
HTML and CSS, for text that needs inline markup.

## Custom painting

Each control raises `CustomPaintBackground`, `CustomPaint` and
`CustomPaintForeground` during its paint cycle. The event argument carries the
resolved colours and the `Graphics` surface, so you can draw over the Metro look
without subclassing:

```csharp
tile.CustomPaintForeground += (sender, e) =>
    e.Graphics.DrawImage(icon, new Rectangle(8, 8, 32, 32));
```

`MetroPaint` exposes the same colour tables the controls use — for example
`MetroPaint.BackColor.Form(theme)` or `MetroPaint.BorderColor.Button.Hover(theme)` —
along with `GetStringFormat` and `GetTextFormatFlags` for aligning your own text the
way the framework does.

## Fonts

Text is rendered in Segoe UI. Add `Net4x.MetroFramework.Fonts` and the bundled Open
Sans faces are substituted automatically on machines that do not have Segoe UI, so
layouts stay consistent. Nothing else changes: the resolver is discovered at runtime
and there is no code to write.

`MetroFonts` exposes the scale the controls use, should you need it directly:
`MetroFonts.Title`, `MetroFonts.Subtitle`, and per-widget helpers such as
`MetroFonts.Label(MetroLabelSize.Medium, MetroLabelWeight.Regular)`.

## Notes

* Strong-named, and signed with the same key across all three packages.
* Source Link is enabled, so stepping into the library works from a debugger.
* `MetroForm` needs a Windows Forms message loop; the drop shadow and task window
  create real top-level windows.

## Licence

MIT. Copyright (c) 2011 Sven Walter. Open Sans is licensed under the Apache License
2.0, and the bundled HTML renderer derives from Jose Menendez Poo's
*A Professional HTML Renderer You Will Use* (BSD).
