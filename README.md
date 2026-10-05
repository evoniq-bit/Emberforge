# Emberforge

**Emberforge** is a standalone trainer and modding tool for **Enshrouded** on Windows.

The goal of Emberforge is to provide useful quality-of-life, building and gameplay tools in one easy-to-use application.

Emberforge connects to the running Enshrouded process and allows you to enable or configure individual features directly from its interface.

**Current source version:** `v0.2.15`

> **Development Notice**
>
> Emberforge was developed with significant assistance from **OpenAI Codex**.
> I want to be transparent about the use of AI in the development of this project.

---

# Features

## Character

- **Infinite Health**  
  Keeps your health at its current maximum.

- **No Fall Damage**  
  Prevents health loss caused by falling.

- **Infinite Stamina**  
  Keeps stamina at its current maximum.

- **Infinite Mana**  
  Keeps mana at its current maximum.

- **Infinite Cold Timer**  
  Keeps your warmth at maximum and protects against cold damage.

- **Infinite Shroud Timer**  
  Keeps the remaining Shroud time at maximum.

- **Infinite Breath**  
  Prevents underwater breath consumption.

- **Add Experience**  
  Add a custom amount of experience points. Normal level-up processing and the game's maximum level still apply.

- **Additional Skill Points**  
  Add temporary bonus skill points while Emberforge is connected. Already unlocked talents are still saved by the game.

- **Glider Flight**  
  Extends the glider pitch range so the normal glider controls can be used to climb as well as descend. Stamina is preserved for extended flight.

---

## Building Blocks

- **No Building Material Consumption**  
  Materials are not consumed when placing building blocks or placeable objects. Normal material requirements in the build menu still apply.

- **Free Building**  
  Bypasses the material requirement when placing building blocks and placeable objects.

- **Capture Material with the Pickaxe**  
  Aim at a block or material in the world and capture it using a configurable hotkey.

- **Material Override by ID**  
  Enter a material ID from `1–255` and use it as the replacement material.

- **Material Favorites**  
  Save frequently used materials under custom names and quickly select them again.

- **Configurable Override Hotkeys**  
  Configure hotkeys for capturing, activating and disabling material overrides.

---

## Object Override

Object Override allows normally unavailable world objects to be used as replacements for placeable objects.

- **Capture Objects with the Pickaxe**  
  Aim at an object in the world and capture it using a configurable hotkey.

- **Selectable Source Object**  
  Use either the currently selected placeable object or a fixed source object.

- **Fixed Source Objects**  
  Choose a source from the object catalog or use the last normally placed object.

- **Object Catalog**  
  Build and update a catalog directly from the installed Enshrouded game files.

- **Catalog Search**  
  Search available object definitions.

- **Object Favorites**  
  Mark frequently used objects as favorites and filter the catalog accordingly.

- **Extended Catalog Display**  
  Optionally display creatures, effects and technical templates.

- **Use Catalog Objects as Replacements**  
  Select compatible catalog entries and use them directly as replacement objects.

- **Nearby Object Scanner**  
  Scan loaded objects around the player using a configurable search radius.

- **Use Nearby Objects as Replacements**  
  Select an object found by the nearby scanner and activate it as the replacement.

- **Dismantle Overridden Objects (Experimental)**  
  Allows newly placed overridden fences, doors and other compatible world props to be dismantled normally. This currently applies only to the selected replacement object and is still considered a test feature.

- **Configurable Override Hotkeys**  
  Configure hotkeys for capturing, activating and disabling object overrides.

> Not every object definition contained in the game files is necessarily usable as a placeable building object.

---

## Inventory & Equipment

- **Infinite Durability**  
  Weapons and tools no longer lose durability while being used.

- **Edit Selected Stack**  
  Change the amount of a stackable item selected in the hotbar.

- **Increase Maximum Stack Size**  
  Increase the maximum stack size for the selected item type.

- **Stack Sizes up to 50,000**  
  Stack amounts and limits can currently be increased up to `50,000`.

- **Restore Normal Stack Limit**  
  Restore the original stack-size limit for the selected item type.

Items with their own durability are not modified by the stack editor.

---

## Crafting

- **No Ingredient Consumption**  
  Craft available recipes without consuming the required ingredients.

The required materials must still be present in the crafting interface.

---

## Settings

- **English interface**
- **German interface**
- **Configurable hotkeys**
- **Global in-game hotkeys**  
  Shortcuts can remain active while either Enshrouded or Emberforge is in the foreground.
- **Always on Top**  
  Keep the Emberforge window above other windows.
- **Save Settings**  
  Store your language, hotkey and interface preferences for future sessions.
- **Restart as Administrator**  
  Restart Emberforge with administrator rights when Enshrouded is running elevated.

---

# Building & Object Override

One of the main features of Emberforge is the ability to override building materials and placeable objects.

Enshrouded contains many materials, decorations, doors, railings and other world objects that normally cannot be directly built by the player.

Emberforge allows compatible objects and materials to be captured or selected and used as replacements for normally placeable content.

For example, a normal building material such as Dirt can be temporarily replaced with another material found in the world.

The same concept can be used with placeable objects.

---

# Requirements

- Windows
- Enshrouded on Steam
- A running local Enshrouded game session

No Cheat Engine installation or additional trainer software is required.

---

# Source Code

The complete source code for **Emberforge v0.2.15** is published in this repository for transparency, community review and Nexus Mods security review.

You can find it here:

**[Emberforge-v0.2.15-Source/](Emberforge-v0.2.15-Source/)**

The source folder contains the C# source, WPF interface/resources, hook manifests, automated tests, hook generators, catalog data, the required Zstandard dependency, license notices and the original reference used for Turk645's Glider Flight implementation.

Build instructions are available in [BUILDING.md](BUILDING.md).

---

# Antivirus Notice

Emberforge directly interacts with the memory of the running Enshrouded process.

Because techniques such as process memory access, memory modification and runtime hooks are also used by trainers and other debugging/modding tools, some antivirus products may classify Emberforge as:

- Trainer
- GameHack
- Riskware
- Suspicious
- Generic / Heuristic detection

This does **not automatically mean that every antivirus detection is a false positive**.

The complete source code for Emberforge is published in this repository so that users and platform moderators can inspect how Emberforge works.

For more information, see [SECURITY.md](SECURITY.md) and [THIRD_PARTY.md](THIRD_PARTY.md).

---

# Credits

Special thanks to **Turk645**, the original creator of **Builder's Companion**.

The original concept and functionality behind overriding building blocks and placeable objects came from his work. Other parts of Builder's Companion also inspired ideas and functionality that were later rebuilt, adapted or expanded in Emberforge.

The current **Glider Flight** feature is also based on Turk645's original Glider Flight work and has been adapted for the current Enshrouded version and integrated into Emberforge.

These features and ideas have been adapted, rebuilt, expanded and integrated into Emberforge for current versions of Enshrouded.

Without his original work, several parts of Emberforge probably would not exist in their current form.

See [CREDITS.md](CREDITS.md) for more information.

---

# Development

Emberforge is still under active development.

New features, improvements and compatibility updates for future versions of Enshrouded are planned.

Bug reports, suggestions and feature ideas are welcome.

---

# Disclaimer

Use Emberforge at your own risk.

Because Emberforge modifies game memory and game functionality while Enshrouded is running, bugs, crashes or unexpected behavior may occur.

Creating regular backups of your Enshrouded save files and worlds is strongly recommended, especially before using new or experimental features and after major game updates.
