Idol Showdown Access 1.1.0
==========================

Accessibility mod for Idol Showdown: spoken menus, character select, opponent
locator sounds, warning sounds at critical health and spoken on-screen texts.
For blind players, with controller or keyboard.
Deutsche Anleitung: IdolShowdownAccess_LIESMICH.txt


NEW IN VERSION 1.1.0
--------------------
- Control on its own now always stops speech, also in the mod menu and while
  JAWS is running. A controller button for this can be chosen.
- New key layout: F2 details, F5 match status, F6 opponent position, F9 repeat,
  F10 stop speech, F11 mod menu (previously F5).
- The mod menu now has submenus: Language and voice, Reading, Fight and
  opponent, Sounds and volume, Controls, Advanced. Plus "Save and close",
  "Discard", "Donation" and "Accessibility Mods by Grybyx". B goes up one level,
  Y explains the entry, F11 saves.
- Entries that belong to a sound play it after being announced. "Play all
  sounds" explains every sound.
- Warning sound when the opponent fires a projectile, from their side (new,
  not yet tested).
- Locator sounds start only once the fight begins and end with the round.
- "Bijou versus Fubuki. Round 1" now comes in the right order.
- Win and loss are checked against the game's win counters.
- "Super ready" and "Burst ready" no longer repeat at every round start.
- No more wrong "Pause" before the first menu item at game start.
- F11 during an offline fight pauses the game and opens the mod menu.
- Game language changed: the mod announces it and names missing voices.
- Ducking separately for speech (on) and sounds (off); you now set the game
  volume while speaking (default 60 percent).
- Own log IdolShowdownAccess.log with the previous log, voice list in
  IdolShowdownAccessVoices.txt, data collector for bug reports.
- The version number is announced at startup.
- After this update the mod menu opens once by itself.


INSTALLATION
------------
1. Open the Idol Showdown game folder.
   In Steam: right-click Idol Showdown, Manage, Browse local files.
   IdolShowdown.exe is in this folder.
2. Unpack the whole content of this ZIP archive into exactly this folder.
   If Windows asks whether to replace files: yes.
3. Run IdolShowdownAccess_Installieren.bat in the game folder.
   It downloads two small ZIP files from unity.bepinex.dev once: the complete
   .NET and Unity libraries. The game ships stripped libraries; without the
   complete ones it hangs at the epilepsy warning with the mod. At the end it
   says "Installation complete". Run it again after every update.
4. Start Idol Showdown through Steam as usual. While loading, the mod says
   "Idol Showdown Access 1.1.0 loaded".

If the download fails, download these two files yourself:
  https://unity.bepinex.dev/corlibs/2020.3.49.zip
  https://unity.bepinex.dev/libraries/2020.3.49.zip
From the first, unpack all DLL files; from the second only UnityEngine.dll and
the UnityEngine.<Name>Module.dll files, into the folder unstripped_corlib in the
game folder.

Speech needs at least one 32-bit SAPI voice (e.g. Microsoft David or Zira
Desktop for English, Microsoft Hedda Desktop for German). They come with
Windows.


LANGUAGE AND VOICE
------------------
All announcements use the 32-bit Windows speech (SAPI), independent of your
screen reader. The small helper ISAccessSapi32.exe runs in the background for
this, because the 64-bit game cannot load the 32-bit voices itself. In the mod
menu you can switch to output through your screen reader (NVDA, JAWS).

The mod speaks the game's language: if the game runs in English, announcements
and mod menu are English and an English voice speaks. German is used when the
game runs in German. A fixed language can be chosen in the mod menu. Choose the
voice under "Voice for <language>"; it is remembered per language.

A new announcement, such as a newly selected menu item, always interrupts the
previous one. Control on its own stops any announcement.

Audio ducking: while the mod speaks, the game gets quieter (default 60 percent)
and then fades back. The mod sounds stay loud.


THE MOD MENU
------------
Open: F11, or hold the View button on the controller. During an offline fight
the mod pauses the game first. It opens by itself at the first start and after
updates with new entries.

  Up / Down              Select entry (controller also LB / RB)
  Left / Right           Change value (controller also X to decrease)
  Enter, Space, A        Toggle, trigger, open submenu
  Backspace, Escape, B   Up one level; on the main level: discard
  F1 or Y                Explain entry
  F11                    Save and close
  Home / End             First / last entry
  Control                Stop speech

Main level:
- Mod enabled
- Language and voice: mod language, speech output, voice for the current
  language, speech rate, voice volume, ducking while speaking, ducking for
  sounds, game volume while ducking
- Reading: spoken menus, position in menus, descriptions and dialog texts,
  character select with jumping cursor, dialogues, tutorial and notices
- Fight and opponent: opponent locator, side switch, corner, projectiles,
  invincibility, big damage (from percent), health warnings and heartbeat
  (below percent), opponent health, match announcements, super/burst/collab
  ready, your side in local versus
- Sounds and volume: volume of mod sounds, play sounds in the menu, play all
  sounds
- Controls: controller shortcuts, buttons for status, opponent position,
  repeat and stop speech (with Xbox and PlayStation names), hear key bindings
- Advanced: open the menu at every start, detailed log
- Donation: opens https://buymeacoffee.com/blindretrogamer/
- Accessibility Mods by Grybyx: opens https://github.com/grybyx/
- Save and close
- Discard: restores all values to how they were when the menu opened


KEYS IN THE GAME
----------------
Keyboard:
  Control  stop speech (always, also with JAWS)
  F1       help
  F2       details: info and stats of the character in character select, the
           menu item with its description, the current tutorial task
  F5       match status: your health, opponent, time, rounds, super, burst,
           collab
  F6       opponent position: left or right, distance, air or ground, corner
  F7       locator sound on or off
  F8       whole mod on or off
  F9       repeat last announcement
  F10      stop speech
  F11      mod menu

Controller (Xbox names, PlayStation in brackets; changeable in the mod menu):
  Tap View (Share)            match status in a fight, otherwise details
  Hold View (Share)           mod menu
  Press left stick (L3)       opponent position in a fight, otherwise repeat
  Press right stick (R3)      repeat last announcement
  Stop speech                 off by default, choose in the mod menu


CHARACTER SELECT
----------------
The game's free cursor snaps: each direction press jumps to the next character
or button and its name is announced. A short low tone marks the edge. Confirm
selects. Then: costume (up/down, left/right changes page), collab partner
(up/down), then "Ready" with character and partner. F2 or View: description and
stats of the character.


SOUNDS IN A FIGHT
-----------------
All sounds start only once the fight begins ("Go").
- Opponent on the ground: round plop from their direction. The closer, the
  faster.
- Opponent in the air: bright rising sound.
- Opponent right next to you: rough buzz from the center.
- Side switch: noise moves to the opponent's new side.
- Corner: two dull knocks on the side of the wall.
- Opponent's projectile: two hard short beeps from their side.
- You are invincible: two bright chimes; end: soft falling tone. Super armor:
  metallic clink.
- Big damage in one combo (default from 15 percent): deep impact, then e.g.
  "Minus 23 percent".
- Critical health (default below 25 percent): heartbeat, faster below
  10 percent.
Announcements: "Bijou versus Fubuki. Round 1", round won or lost with score,
victory or defeat, 30 and 10 seconds left, half health, health critical,
opponent below half, super ready, burst ready, collab call-in ready, off-collab
ready.
In the mod menu under "Sounds and volume, Play all sounds" you hear every sound
with an explanation.


COLLAB PARTNER IN A FIGHT
-------------------------
- Collab call-in: hold forward and press the collab button. Costs super meter.
- Off-collab: collab button alone. Costs the colorful superchat meter.
Neither works during hit stun or block stun. F5 tells you what is ready.


REPORTING BUGS
--------------
1. In the mod menu under "Advanced", turn on the detailed log.
2. Play until the problem happens. Note the time.
3. Quit the game and run IdolShowdownAccess_Daten_sammeln.bat in the game
   folder. It puts a ZIP with date and time on your desktop.
4. Send the ZIP with one sentence about the problem.


TROUBLESHOOTING
---------------
Game hangs at the warning screen: the folder unstripped_corlib must contain
mscorlib.dll and UnityEngine.CoreModule.dll. Otherwise run the installer again.
No announcement at start: check whether BepInEx\LogOutput.log is created. If
not, BepInEx does not start (usually winhttp.dll or doorstop_config.ini is
missing).
Sounds but no speech: IdolShowdownAccessVoices.txt (plugin folder) lists the
voices found. If it is empty, a 32-bit voice is missing. In the mod menu you can
switch speech output to screen reader.


UNINSTALL
---------
Delete winhttp.dll in the game folder; this turns the mod off. To remove it
completely, also delete doorstop_config.ini, .doorstop_version, the folders
BepInEx and unstripped_corlib and the IdolShowdownAccess_* files.


LINKS
-----
Donation:                     https://buymeacoffee.com/blindretrogamer/
Accessibility Mods by Grybyx: https://github.com/grybyx/
Source code:                  https://github.com/grybyx/Idol-Showdown-Access
Game on Steam:                https://store.steampowered.com/app/1742020/Idol_Showdown/


LICENSES
--------
The mod is under the MIT license (BepInEx\plugins\IdolShowdownAccess\
LICENSE.txt). Bundled: BepInEx 5.4.23.2 with HarmonyX, MonoMod and Mono.Cecil,
Unity Doorstop, Tolk and the NVDA Controller Client; details in
THIRD_PARTY_NOTICES.txt and the licenses folder next to it. The mod is a fan
project and not affiliated with the game's developers.
