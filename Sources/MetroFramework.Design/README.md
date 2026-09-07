# Net4x.MetroFramework.Design

Visual Studio designer support for
[Net4x.MetroFramework.Library](https://www.nuget.org/packages/Net4x.MetroFramework.Library).

This package contains no runtime API. It is loaded by the Windows Forms designer to
make the Metro controls behave properly on the design surface.

## Install

```
dotnet add package Net4x.MetroFramework.Design
```

Add it alongside `Net4x.MetroFramework.Library`. Nothing needs wiring up: each Metro
control names its designer, and the designer host resolves it from this assembly.

## What it does

**Hides settings that have no effect.** The Metro controls draw their own text and
chrome, so the property grid stops offering the inherited Windows Forms settings the
Metro look overrides — `Font`, `RightToLeft`, `FlatStyle`, `BackColor`, the `Image*`
family, and so on — leaving only the properties that actually change anything.

**Constrains resizing.** `MetroScrollBar` can only be resized along its own axis,
and a docked `MetroTabControl` is not draggable.

**Edits tab pages properly.** `MetroTabControl` gets *Add Tab* and *Remove Tab*
verbs on its smart tag, and its `TabPages` collection editor creates `MetroTabPage`
instances rather than plain `TabPage`s.

**Resets a theme in one step.** `MetroStyleManager` gets a *Reset Styles to Default*
verb that walks the owning form — nested containers, tab pages and context menus
included — and puts every control's `Style` and `Theme` back to `Default`, so they
inherit from the manager again.

## Target frameworks

`net45`, `net6.0-windows`, `net8.0-windows`, `net10.0-windows`. Windows only.

## Notes

Strong-named with the same key as the main package. The controls reference their
designers by fully qualified strong name, so this package's assembly version and
public key token have to match what `MetroFramework.dll` was built against — always
take both packages from the same release.

## Licence

MIT. Copyright (c) 2011 Sven Walter. The tab control designer is based on work by
Mick Doherty.
