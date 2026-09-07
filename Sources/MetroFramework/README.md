# Net4x.MetroFramework

Modern UI ("Metro") for Windows Forms, in one package: the themed control set, the
Visual Studio designer support and the bundled fonts.

This is the bundle. It contains no assemblies of its own — installing it pulls in the
three packages that do the work:

| Package | What it adds |
| --- | --- |
| [`Net4x.MetroFramework.Library`](https://www.nuget.org/packages/Net4x.MetroFramework.Library) | The controls, forms, theming and the embedded HTML/CSS renderer |
| [`Net4x.MetroFramework.Design`](https://www.nuget.org/packages/Net4x.MetroFramework.Design) | Design-time behaviour: property filtering, tab page editing, the *Reset Styles to Default* verb |
| [`Net4x.MetroFramework.Fonts`](https://www.nuget.org/packages/Net4x.MetroFramework.Fonts) | Open Sans faces, substituted automatically when Segoe UI is missing |

Take this package when you are building a desktop application and want the whole
thing. Reference `Net4x.MetroFramework.Library` on its own instead when you are
writing a library, or when the designer and the font fallback are dead weight — for
example in a service or a test project that only needs to compile against the types.

Because the three packages are versioned and released together, the bundle also keeps
them in step: the controls resolve their designers and the font resolver by fully
qualified strong name, and a mismatched set fails silently — the designer quietly
falls back to the stock one, the fonts quietly do not load.

This is a maintained fork of [MetroFramework](https://github.com/viperneo/winforms-modernui)
by Sven Walter, repackaged for current .NET while keeping the original API.

## Install

```
dotnet add package Net4x.MetroFramework
```

## Target frameworks

`net45`, `net6.0-windows`, `net8.0-windows`, `net10.0-windows`. Windows only — it is
built on Windows Forms and GDI+.

## Getting started

Derive your form from `MetroForm` and drop Metro controls onto it:

```csharp
using MetroFramework;
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

Every control carries `Style` (the accent colour) and `Theme` (light or dark), and
leaving either at `Default` means *inherit* — so you normally set them once, on a
`MetroStyleManager` whose `Owner` is the form. It walks the whole control tree,
nested containers, tab pages and context menus included, and keeps applying itself to
controls added later.

The controls, the theming model, the custom-paint events and the HTML renderer are
documented in the
[`Net4x.MetroFramework.Library` readme](https://www.nuget.org/packages/Net4x.MetroFramework.Library/#readme-body-tab).

## Licence

MIT. Copyright (c) 2011 Sven Walter. Open Sans is licensed under the Apache License
2.0, and the bundled HTML renderer derives from Jose Menendez Poo's
*A Professional HTML Renderer You Will Use* (BSD).
