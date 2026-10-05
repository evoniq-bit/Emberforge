# Building Emberforge v0.2.15

This repository publishes the Emberforge source code for transparency, community review and Nexus Mods security review.

## Requirements

- Windows 10 or Windows 11
- .NET Framework 4.x
- 64-bit .NET Framework C# compiler (`csc.exe`)
- WPF
- `libzstd.dll` placed inside the `src` directory before building
- `Emberforge-Logo.png` placed in the project root before building

Visual Studio and Python are **not** required for the normal application build. Python is only required for the development tools in `src/Hook-Generator/`.

## Build

Keep the directory structure intact and run PowerShell from the project root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\src\Bauen.ps1
```

The build script creates `Emberforge.exe` and copies `libzstd.dll` next to it.

## Source archive

The repository contains a clean source archive for **Emberforge v0.2.15** under:

`source-archives/Emberforge-v0.2.15-Source-Code.zip`

The archive contains the C# source, WPF resources, hook manifests, tests, hook generators, catalog data and license notices. It intentionally does not include the compiled Emberforge executable or personal settings/session files.

The native `libzstd.dll` dependency and the application logo are binary/resource files rather than Emberforge source code and are therefore not included in the clean source-code archive.

## Self-test

After building, the included automated checks can be run with:

```powershell
.\Emberforge.exe --self-test "$PWD\Pruefung.txt"
```

Automated checks do not replace in-game testing.

## Glider Flight

The Glider Flight feature is based on Turk645's original Glider Flight work from Builder's Companion and has been adapted for the current Enshrouded version and integrated into Emberforge.

The original reference is preserved in `src/Turk-Glider-Flight-Original.txt` inside the source archive.

## Third-party dependency

`libzstd.dll` is a native Zstandard dependency. Its BSD license notice is included as `Zstandard-Lizenz.txt`.

See [THIRD_PARTY.md](THIRD_PARTY.md) for additional third-party information.
