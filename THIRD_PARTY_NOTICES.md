# Third-party notices / Fremdkomponenten

The release ZIP of Idol Showdown Access bundles the following unmodified third-party components. Their license texts are in the `licenses/` folder (in the release: `BepInEx/plugins/IdolShowdownAccess/licenses/`).

Das Release-ZIP von Idol Showdown Access enthält die folgenden unveränderten Fremdkomponenten. Ihre Lizenztexte liegen im Ordner `licenses/` (im Release: `BepInEx/plugins/IdolShowdownAccess/licenses/`).

| Component | Files | License | Source |
| --- | --- | --- | --- |
| BepInEx 5.4.23.2 | `BepInEx/core/BepInEx*.dll`, `HarmonyXInterop.dll` | LGPL-2.1 (`BepInEx-LGPL-2.1.txt`) | https://github.com/BepInEx/BepInEx |
| Unity Doorstop (bundled with BepInEx) | `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version` | LGPL-2.1 (`UnityDoorstop-LGPL-2.1.txt`) | https://github.com/NeighTools/UnityDoorstop |
| HarmonyX (bundled with BepInEx) | `BepInEx/core/0Harmony.dll`, `0Harmony20.dll` | MIT (`HarmonyX-MIT.txt`, `Harmony-MIT.txt`) | https://github.com/BepInEx/HarmonyX |
| MonoMod (bundled with BepInEx) | `BepInEx/core/MonoMod.*.dll` | MIT (`MonoMod-MIT.txt`) | https://github.com/MonoMod/MonoMod |
| Mono.Cecil (bundled with BepInEx) | `BepInEx/core/Mono.Cecil*.dll` | MIT (`Mono.Cecil-MIT.txt`) | https://github.com/jbevain/cecil |
| Tolk | `BepInEx/plugins/IdolShowdownAccess/Tolk.dll` | LGPL-3.0 (`Tolk-LGPL-3.0.txt`, with `GPL-3.0.txt`) | https://github.com/dkager/tolk |
| NVDA Controller Client (bundled with Tolk) | `BepInEx/plugins/IdolShowdownAccess/nvdaControllerClient64.dll` | LGPL-2.1 (`NVDA-Controller-Client-LGPL-2.1.txt`) | https://github.com/nvaccess/nvda |

Not bundled / Nicht enthalten: the installer `IdolShowdownAccess_Installieren.bat` downloads the unstripped .NET (Mono) and Unity engine libraries for the game's Unity version from https://unity.bepinex.dev into `unstripped_corlib/` on the player's computer. They are not part of this repository or the release.

Der Installationshelfer `IdolShowdownAccess_Installieren.bat` lädt die ungekürzten .NET- (Mono-) und Unity-Bibliotheken für die Unity-Version des Spiels von https://unity.bepinex.dev nach `unstripped_corlib/` auf dem Rechner des Spielers. Sie gehören nicht zu diesem Repository und nicht zum Release.

The LGPL components are used as separate, unmodified DLLs and can be replaced by the user. / Die LGPL-Komponenten werden als eigenständige, unveränderte DLLs genutzt und können vom Nutzer ersetzt werden.
