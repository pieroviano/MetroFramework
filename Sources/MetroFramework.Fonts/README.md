# Net4x.MetroFramework.Fonts

Bundled fonts for
[Net4x.MetroFramework.Library](https://www.nuget.org/packages/Net4x.MetroFramework.Library),
so a Metro application looks the same on machines that do not have Segoe UI.

## Install

```
dotnet add package Net4x.MetroFramework.Fonts
```

That is the whole setup. There is no API to call and no configuration: the main
package looks for this assembly at startup and, if it is present, routes every font
request through it.

## What it does

MetroFramework renders its text in Segoe UI, which ships with Windows but is not
guaranteed to be there — a stripped-down image, a locked-down desktop or a Windows
Server install may not have it. Without a substitute, GDI+ silently falls back to a
default face and the layout shifts.

This package embeds three Open Sans faces and maps them onto the ones the framework
asks for:

| Requested | Substituted with |
| --- | --- |
| Segoe UI | Open Sans |
| Segoe UI, bold | Open Sans Bold |
| Segoe UI Light | Open Sans Light |

The substitution only happens when the requested family is genuinely unavailable. On
a machine that has Segoe UI, the real font is used and this package changes nothing.
Fonts the framework does not ask for are never touched.

The faces are loaded from embedded resources into a private font collection, so
nothing is installed on the machine and no elevation is needed.

## Target frameworks

`net45`, `net6.0-windows`, `net8.0-windows`, `net10.0-windows`. Windows only.

## Notes

Strong-named with the same key as the main package. The main package resolves this
assembly by fully qualified strong name, so its assembly version and public key
token have to match what `MetroFramework.dll` was built against — always take both
packages from the same release.

## Licence

The package is MIT, copyright (c) 2011 Sven Walter. The bundled Open Sans font is
by Steve Matteson and is licensed under the Apache License 2.0; the licence text
ships in the repository next to the font files.
