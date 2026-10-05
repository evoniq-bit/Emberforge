# Enshrouded — Emberforge

Quellcode der Windows-App **Emberforge 0.2.15**. Die Flugfunktion heißt in der Oberfläche „Gleiterflug“ bzw. „Glider flight“. Die technische Flugfunktion entspricht Version 0.2.14.

## Inhalt

- `Quellcode/`: C#-Quellen, WPF-Oberfläche, DE/EN-Texte, Hook-Manifeste und Prüfungen.
- `Quellcode/Bauen.ps1`: baut die App mit dem Windows-.NET-Framework-Compiler.
- `Emberforge-Logo.png`: eingebettetes Logo.
- `Credits.txt`: Herkunft und Credits, insbesondere für Turks Glider Flight.
- `Zstandard-Lizenz.txt` und `Katalog-Format-Lizenz.txt`: mitgelieferte Drittanbieterhinweise.

Dieses Paket enthält keine Emberforge.exe, persönlichen Einstellungen, Sitzungsdateien oder Spieldateien. `Quellcode/libzstd.dll` ist die benötigte native Zstandard-Abhängigkeit und wird beim Bauen neben die App kopiert.

## Bauen unter Windows

Benötigt werden Windows 10/11 mit .NET Framework 4.x, der 64-Bit-Framework-Compiler und WPF. Visual Studio und Python sind für den normalen App-Build nicht erforderlich.

Die Ordnerstruktur beibehalten. PowerShell im Projektordner öffnen:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Quellcode\Bauen.ps1
```

Das erzeugt `Emberforge.exe` und `libzstd.dll` im Projektordner. Zum Weitergeben beide Dateien gemeinsam verwenden.

## Starten und Prüfen

Ältere Emberforge-Versionen schließen, Enshrouded starten, die lokale Welt laden und die neue App verbinden. Die aktuelle unterstützte EXE ist Steam-Build **23966345**; der Fingerabdruck wird vor dem Verbinden geprüft.

Die enthaltenen automatischen Prüfungen lassen sich nach dem Bauen ohne Änderungen am laufenden Spiel ausführen:

```powershell
.\Emberforge.exe --self-test "$PWD\Pruefung.txt"
```

Bei Fehlern entsteht `Pruefung.txt.error.txt`. Erfolgreiche Ausführung erzeugt den Prüfbericht. Der Build 0.2.14 bestand 362 automatische Prüfungen; 0.2.15 ändert die sichtbaren Flugtexte und wurde zusätzlich in Deutsch/Englisch/Deutsch geprüft. Die automatischen Prüfungen ersetzen keine Spieltests. Gleiterflug, Unterwasser-Atem und Rückbauverhalten können separat im Spiel geprüft werden.

## Flugfunktion und Credits

Die Flugfunktion basiert auf **Turks Glider Flight** aus der bereitgestellten Enshrouded Building Companion Cheat Table. Sie erweitert die untere Flugwinkelgrenze auf `-1.57`. Emberforge ergänzt App-Integration, lokalen Spielerfilter, Versionsprüfung, Ausdauererhaltung und Wiederherstellung. Die früheren eigenen Eingriffe in Vertikalgeschwindigkeit und Höhenhaltung sind entfernt.

Originaler Flugcode: `Quellcode/Turk-Glider-Flight-Original.txt`. Die Anpassung steht in `Quellcode/Hook-Generator/build_movement_hooks.py`, das fertige Hook-Manifest in `Quellcode/CharacterHooks.json`.

## Hook-Generatoren

`Quellcode/Hook-Generator/` enthält Entwicklungswerkzeuge zur Analyse einer lokal installierten Spiel-EXE. Dafür sind Python und die Pakete aus `requirements.txt` erforderlich; den Spielpfad gegebenenfalls in `disasm_game.py` anpassen. Die App wird mit den mitgelieferten Manifesten gebaut. Generatoren verändern diese Manifeste; sie sind kein notwendiger Schritt des normalen Builds.
