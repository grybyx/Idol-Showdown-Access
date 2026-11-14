# Idol Showdown Access

**English** · [Deutsch](#deutsch)

An accessibility mod that makes **Idol Showdown** playable for blind players: spoken menus and character select, opponent locator sounds in fights, warning sounds at critical health and spoken on-screen texts. Works with an Xbox or PlayStation controller and the keyboard. Speech uses 32-bit Windows SAPI voices (or your screen reader, if you prefer).

## About the game

[Idol Showdown on Steam](https://store.steampowered.com/app/1742020/Idol_Showdown/)

Idol Showdown is a free, fan-made 2D fighting game starring VTubers from hololive. It was made by Besto Game Team and published by holo Indie in May 2023. Each idol plays like a classic fighting-game archetype, with moves taken from her streams and running jokes. Before a fight you pick an idol, a costume and a collab partner, another idol who can jump in to help. You can play Arcade, the Virtual Frontier single-player mode, versus against the computer or a friend, training and tutorials. Online matches use rollback netcode. The game has no monetization.

## Features

- **Spoken menus:** main menu, submenus, settings with all tabs, pause and training menus, stage select, results, pop-ups. Switches and sliders are read with their value, changes immediately; optionally with the position ("3 of 7").
- **Character select:** the game's free cursor snaps to the next character with every direction press. Character, costume, collab partner and "Ready" are announced; F2 reads the character's description and stats.
- **Opponent locator:** a stereo sound from the opponent's side. The closer, the faster. A distinct sound for "in the air" and "right next to you", a whoosh when sides switch, knocking in the corner, a warning when the opponent fires a projectile.
- **Health and state:** heartbeat at critical health, impact sound for big damage, chimes when you are invincible, announcements for rounds, score, win and loss, time, super, burst and collab readiness.
- **Texts:** dialogues, tutorial tasks, notices and victory texts. Chat and superchat overlays are deliberately not read.
- **Own spoken mod menu** with submenus. Everything can be turned off. Each sound can be previewed.
- **Language:** the mod follows the game language (English or German) with a matching voice, and announces language changes.
- **Audio ducking:** the game gets quieter while the mod speaks.
- **Control on its own** always stops speech, also with JAWS running.

## Installation

1. Download the ZIP from [Releases](https://github.com/grybyx/Idol-Showdown-Access/releases).
2. Unpack it into the Idol Showdown game folder (Steam: right-click the game, Manage, Browse local files).
3. Run `IdolShowdownAccess_Installieren.bat` once. It downloads the complete .NET and Unity libraries from unity.bepinex.dev, which the mod loader needs because the game ships stripped ones. Run it again after every update.
4. Start the game through Steam. The mod announces "Idol Showdown Access 1.1.0 loaded".

Full instructions: `IdolShowdownAccess_README.txt` (English) and `IdolShowdownAccess_LIESMICH.txt` (German) in the release.

## Keys

| Function | Keyboard | Controller (default) |
| --- | --- | --- |
| Stop speech | Control, F10 | selectable |
| Help | F1 | – |
| Details (menu item, character) | F2 | tap View |
| Match status | F5 | tap View (in a fight) |
| Opponent position | F6 | press left stick (L3) |
| Locator sound on/off | F7 | – |
| Mod on/off | F8 | – |
| Repeat last announcement | F9 | press right stick (R3) |
| Mod menu | F11 | hold View |

## Building

The mod is a [BepInEx 5](https://github.com/BepInEx/BepInEx) plugin. It is compiled deliberately against the game's stripped libraries, so everything that builds also exists in the game.

```sh
ISA_GAME_MANAGED=/path/to/IdolShowdown_Data/Managed \
ISA_BEPINEX_CORE=/path/to/BepInEx/core \
./build.sh
```

`build.sh` needs PowerShell 7 (`pwsh`), whose bundled Roslyn compiler builds `IdolShowdownAccess.dll` and the 32-bit speech helper `ISAccessSapi32.exe` without a .NET SDK. On Windows you can also use `dotnet build -c Release -p:GameDir="C:\...\Idol Showdown"` for the plugin. `python3 tools/package.py` (with `ISA_BEPINEX_DIR` and `ISA_TOLK_DIR` pointing to unpacked BepInEx 5.4.23.2 and Tolk) builds the release ZIP.

## Support and more mods

- Donation: [buymeacoffee.com/blindretrogamer](https://buymeacoffee.com/blindretrogamer/)
- More accessibility mods: [github.com/grybyx](https://github.com/grybyx/)

## License

MIT, see [LICENSE](LICENSE). Bundled third-party parts: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). This is a fan project, not affiliated with the developers or publishers of Idol Showdown.

---

<a id="deutsch"></a>

# Idol Showdown Access (Deutsch)

Ein Barrierefreiheits-Mod, der **Idol Showdown** für blinde Spielerinnen und Spieler spielbar macht: gesprochene Menüs und Charakterauswahl, Gegnerortung per Ton im Kampf, Warntöne bei kritischer Gesundheit und vorgelesene Einblendungen. Bedienbar mit Xbox- oder PlayStation-Controller und Tastatur. Die Sprache kommt über 32-Bit-SAPI-Stimmen von Windows (auf Wunsch über den Screenreader).

## Über das Spiel

[Idol Showdown auf Steam](https://store.steampowered.com/app/1742020/Idol_Showdown/)

Idol Showdown ist ein kostenloses, von Fans entwickeltes 2D-Kampfspiel mit VTuberinnen von hololive. Entwickelt hat es das Besto Game Team, veröffentlicht wurde es im Mai 2023 von holo Indie. Jedes Idol spielt sich wie ein klassischer Kampfspiel-Typ, mit Angriffen aus ihren Streams und Insider-Witzen. Vor dem Kampf wählst du ein Idol, ein Kostüm und einen Kollab-Partner, ein weiteres Idol, das dir zu Hilfe kommen kann. Es gibt Arcade, den Einzelspielermodus Virtual Frontier, Versus gegen den Computer oder einen Freund, Training und Tutorials. Online wird mit Rollback-Netcode gespielt. Das Spiel kommt ohne Bezahlinhalte aus.

## Funktionen

- **Gesprochene Menüs:** Hauptmenü, Untermenüs, Optionen mit allen Reitern, Pause- und Trainingsmenü, Bühnenauswahl, Ergebnisse, Popups. Schalter und Regler mit ihrem Wert, Änderungen sofort; auf Wunsch mit Position („3 von 7“).
- **Charakterauswahl:** Der freie Zeiger des Spiels springt mit jedem Richtungsdruck zur nächsten Figur. Figur, Kostüm, Kollab-Partner und „Bereit“ werden angesagt; F2 liest Beschreibung und Werte der Figur.
- **Gegnerortung:** Stereo-Ton von der Seite des Gegners, je näher, desto schneller. Eigene Klänge für „in der Luft“ und „direkt bei dir“, Rauschen beim Seitenwechsel, Klopfen in der Ecke, Warnung bei Geschossen.
- **Gesundheit und Zustand:** Herzschlag bei kritischer Gesundheit, Einschlag bei großem Schaden, Glockentöne bei Unverwundbarkeit, Ansagen zu Runden, Stand, Sieg und Niederlage, Zeit, Super, Burst und Kollab.
- **Texte:** Dialoge, Tutorial-Aufgaben, Hinweise und Siegtexte. Chat- und Superchat-Einblendungen werden bewusst nicht vorgelesen.
- **Eigenes gesprochenes Mod-Menü** mit Untermenüs. Alles ist abschaltbar, jeder Ton lässt sich vorhören.
- **Sprache:** Der Mod folgt der Spielsprache (Englisch oder Deutsch) mit passender Stimme und sagt Sprachwechsel an.
- **Audio-Ducking:** Das Spiel wird leiser, solange der Mod spricht.
- **Strg allein** unterbricht immer die Sprache, auch mit laufendem JAWS.

## Installation

1. Die ZIP unter [Releases](https://github.com/grybyx/Idol-Showdown-Access/releases) herunterladen.
2. In den Spielordner von Idol Showdown entpacken (Steam: Rechtsklick auf das Spiel, Verwalten, Lokale Dateien durchsuchen).
3. Einmal `IdolShowdownAccess_Installieren.bat` starten. Sie lädt die vollständigen .NET- und Unity-Bibliotheken von unity.bepinex.dev, die der Mod-Lader braucht, weil das Spiel gekürzte mitbringt. Nach jedem Update erneut ausführen.
4. Das Spiel über Steam starten. Der Mod sagt „Idol Showdown Access 1.1.0 geladen“.

Ausführliche Anleitung: `IdolShowdownAccess_LIESMICH.txt` (Deutsch) und `IdolShowdownAccess_README.txt` (Englisch) im Release.

## Tasten

| Funktion | Tastatur | Controller (Standard) |
| --- | --- | --- |
| Sprache unterbrechen | Strg, F10 | wählbar |
| Hilfe | F1 | – |
| Details (Menüpunkt, Figur) | F2 | Ansicht kurz |
| Kampfstatus | F5 | Ansicht kurz (im Kampf) |
| Gegnerposition | F6 | linken Stick drücken (L3) |
| Ortungston an/aus | F7 | – |
| Mod an/aus | F8 | – |
| Letzte Ansage wiederholen | F9 | rechten Stick drücken (R3) |
| Mod-Menü | F11 | Ansicht halten |

## Bauen

Der Mod ist ein Plugin für [BepInEx 5](https://github.com/BepInEx/BepInEx). Er wird absichtlich gegen die gekürzten Bibliotheken des Spiels kompiliert; was baut, gibt es also auch im Spiel.

```sh
ISA_GAME_MANAGED=/pfad/zu/IdolShowdown_Data/Managed \
ISA_BEPINEX_CORE=/pfad/zu/BepInEx/core \
./build.sh
```

`build.sh` braucht PowerShell 7 (`pwsh`). Dessen mitgelieferter Roslyn-Compiler baut `IdolShowdownAccess.dll` und den 32-Bit-Sprachhelfer `ISAccessSapi32.exe` ohne .NET-SDK. Unter Windows geht für das Plugin auch `dotnet build -c Release -p:GameDir="C:\...\Idol Showdown"`. `python3 tools/package.py` (mit `ISA_BEPINEX_DIR` und `ISA_TOLK_DIR` auf entpacktes BepInEx 5.4.23.2 und Tolk) baut das Release-ZIP.

## Unterstützung und weitere Mods

- Spenden: [buymeacoffee.com/blindretrogamer](https://buymeacoffee.com/blindretrogamer/)
- Weitere Zugänglichkeits-Mods: [github.com/grybyx](https://github.com/grybyx/)

## Lizenz

MIT, siehe [LICENSE](LICENSE). Mitgelieferte Fremdteile: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). Der Mod ist ein Fanprojekt und steht in keiner Verbindung zu den Entwicklern oder Herausgebern von Idol Showdown.
