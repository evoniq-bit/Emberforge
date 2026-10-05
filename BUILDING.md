# Building Emberforge v0.2.15

The complete Emberforge v0.2.15 source code is published in this repository for transparency, community review and Nexus Mods security review.

## Source location

The source package is located at:

`Emberforge-v0.2.15-Source/`

The main application source is inside:

`Emberforge-v0.2.15-Source/Quellcode/`

## Requirements

- Windows 10 or Windows 11
- .NET Framework 4.x
- 64-bit .NET Framework C# compiler (`csc.exe`)
- WPF

Visual Studio and Python are **not** required for the normal application build.

Python is only required for the development tools inside:

`Emberforge-v0.2.15-Source/Quellcode/Hook-Generator/`

The required native Zstandard dependency is included as:

`Emberforge-v0.2.15-Source/Quellcode/libzstd.dll`

The application logo is included as:

`Emberforge-v0.2.15-Source/Emberforge-Logo.png`

## Build

Keep the directory structure intact and run PowerShell from the repository root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Emberforge-v0.2.15-Source\Quellcode\Bauen.ps1
```

The build script creates:

`Emberforge-v0.2.15-Source/Emberforge.exe`

and copies `libzstd.dll` next to the executable.

## Source contents

The published source includes:

- C# application source
- WPF XAML interface and translations
- Runtime hook manifests
- Automated tests
- Hook generator scripts
- Building/object catalog data
- Third-party license notices
- Turk645 Glider Flight reference
- Build script
- Required `libzstd.dll` dependency
- Emberforge logo resource

The repository does **not** need personal settings or generated test-output files.

## Self-test

After building, change into the source folder:

```powershell
cd .\Emberforge-v0.2.15-Source
```

Then run:

```powershell
.\Emberforge.exe --self-test "$PWD\Pruefung.txt"
```

If a self-test fails, Emberforge may create `Pruefung.txt.error.txt`.

Automated checks do not replace in-game testing.

## Glider Flight

The Glider Flight feature is based on Turk645's original Glider Flight work from Builder's Companion and has been adapted for the current Enshrouded version and integrated into Emberforge.

The preserved original reference can be found at:

`Emberforge-v0.2.15-Source/Quellcode/Turk-Glider-Flight-Original.txt`

The adapted generator is located at:

`Emberforge-v0.2.15-Source/Quellcode/Hook-Generator/build_movement_hooks.py`

## Third-party components

`libzstd.dll` is a native Zstandard dependency.

The related license notices are included in the published source folder:

- `Emberforge-v0.2.15-Source/Zstandard-Lizenz.txt`
- `Emberforge-v0.2.15-Source/Katalog-Format-Lizenz.txt`

See [THIRD_PARTY.md](THIRD_PARTY.md) for additional information.
