# Casey Jones

**Nexus Mods** https://www.nexusmods.com/railroader/mods/2250

This is the source of Casey Jones 0.1.0. The ready-made zip is on the Nexus page.

A Curve speed setting (1x, 1.5x, 2x or Unlimited) for the game's curve speed limit: how fast the Auto Engineer takes a curve, and how fast a car can take it before it is damaged or derails.

## Curve speed

Preferences > Mods > Casey Jones has a dropdown, Curve speed (1x, 1.5x, 2x or Unlimited), and a checkbox, Ignore car curve limits
(unrealistic). They start at 1x and off, which is exactly the base game. A save applies without a reload.
The game rates every curve with a speed limit worked from its sharpness (10 degrees is 38 mph, 20 degrees is 26 mph, straight
track is 1000). This multiplies that rating, never above 250 mph. Unlimited is 250 mph. Straight track keeps the game's own 1000.
The Auto Engineer, the cars and the tooltip follow the setting:
- Auto Engineer: it keeps a small margin under the rating and rounds down to a multiple of 5 mph, then takes the lowest of that,
  the segment limit and the track class speed. Examples, with the segment limit at 100, class 5 track (80 mph) and a car that
  takes the curve (AE limit in mph):
      curve    1x   1.5x   2x   Unlimited
       3 deg   70    80    80      80
      10 deg   35    50    70      80
      14 deg   25    40    60      80
      20 deg   20    35    45      80
      30 deg   15    25    35      80
- Cars: the same rating is a car's curve limit. Over it (or over the track class speed, whichever is lower) a car takes
  condition damage and can derail, as in the base game. A higher setting moves that limit up on curves only.
- The track hover tooltip's curve figure shows it.
Track wear does not follow it: wear, the maintenance traffic estimate and the track debug window's curve figure always use the
game's own rating. Track wears faster when you take curves faster than the game's rating, rising until twice the rating and
capped there, as in the base game. (The estimate assumes a car that takes 23 degrees.)
- A car on a curve sharper than it is built for is slowed exactly as in the base game, at every setting: from the rating at the car's
  own limit down to 5 mph 15 degrees past it. The setting applies only to curves inside the car's limit, so that limit is a sharp
  edge: the curve just inside it can run the scaled speed, the first one at or past it the base game's crawl. (A car that takes
  23 degrees, on 24, 28 and 33 degrees: 22, 16 and 10 mph at every setting. At Unlimited the same car's curve rating is 250 mph on a
  22.99 degree curve and 24 mph at 23 degrees.) Ignore car curve limits removes this slowdown.

## Ignore car curve limits (unrealistic)

Off by default. On, long cars take any curve at speed: a curve sharper than a car is built for no longer slows it toward 5 mph, and
the drag the game puts on such a car in the curve is gone. The Auto Engineer then plans the scaled curve speed on every curve,
whatever its cars can take. The curve's limit is then the Curve speed's rating alone: a car that takes 23
degrees, on 24, 28 and 33 degrees, is held to 23, 21 and 19 mph at 1x, 47, 43 and 39 mph at 2x, and 250 at Unlimited. Track wear
still charges for a car on a curve over its limit, and wear reads the game's own rating as above.
Off, all of this is exactly the base game's.

## What it does not change

The track class speeds (class 1 is 10 mph, 2 is 25, 3 is 40, 4 is 60, 5 is 80; worn track drops a class), your Railroad Speed
Limit (45 mph to start; it caps every segment), a segment's posted limit, yard mode, the engine's top speed, and how sharp a
curve each car can take, or how hard the game slows a car that can't take one. The Auto Engineer still keeps to the lowest of those
and the curve limit, so Unlimited lifts the curve limit to 250 mph, not the others.
Speed over the class limit still damages and derails exactly as before.
The base map posts limits on 577 of its segments (25, 15, 30 or 45 mph). They cap the Auto Engineer there whatever the curve
setting is. No Legacy Speed Limits removes them; the two mods don't depend on each other, work alone, and together do both jobs.
If the game changes in a way that stops the setting from applying, the mod logs one line and ignores the setting.

## Install

1. Unzip so you have a CaseyJones folder in  Documents\Railroader\Mods   (Linux/Proton: the same folder inside the game's Proton prefix).
   Inside should be CaseyJones.dll, 0Harmony.dll, Info.json, FEEDS.md, README.txt and LICENSE.txt.
2. Turn on Allow DLL Mods in Preferences > Features (Experimental section), then restart. With it off the game
   switches the mod off.
3. Main menu > Mods > Manage Mods > tick Casey Jones > Apply.
4. The first time you load a save made before you installed it, the game asks "This Save Uses Different Mods": choose
   "Keep My Current Mods" so it stays on for that load.

## Checking it worked

Player.log (Railroader folder under the game's AppData LocalLow, or the Proton prefix on Linux) should have, when the mod starts:

  Casey Jones 0.1.0 loaded: curve speed 1x, ignore car curve limits off (a host's settings rule in multiplayer)

(or your choice). A line saying the setting isn't available on this game version means the game changed under the mod; it
then does nothing.

## Multiplayer

The host runs the Auto Engineer and the car physics, so the host's copy and the host's two settings decide how trains run. The
host's choices are written into the world; a player who has the mod sees it in the tooltip and in Preferences (read only, and a
player's own setting changes nothing in someone else's world). A player without the mod still connects and sees the base game's
curve figure in the tooltip. Joining, the game logs an Information line "Object id ashenmare.casey-jones not found, saving 2
properties"; only if the host changes the setting while that player is connected does it log a Warning about an unknown object.
Both are harmless. The mod is optional. Not yet tried in a multiplayer game.
A player who has the mod, on a host that doesn't (or has it unticked), sees the last value the save held in the tooltip and in
Preferences; the host's trains run at 1x.

## Feed

FEEDS.md describes a small read-only feed (the setting in force) that other mods can read.

## Removing it

Untick it or delete its folder and every curve limit is back on the next load. What stays in the save is anything that happened
while it was on: track wear and damage a car took. The save may also keep a small value of the mod's own (the host's two
settings), which the game leaves unused without the mod. The setting itself is in settings.json in the mod's folder.
The game may ask "This Save Uses Different Mods" ("Keep My Current Mods") or, if you deleted the folder, "Mod Not
Available" ("Load Without It").

## Changes
0.1.0: First release.
New: Curve speed setting (1x, 1.5x, 2x, Unlimited) for the Auto Engineer's curve limit and car curve damage and derailment.
New: Ignore car curve limits (unrealistic) checkbox.
New: a read-only feed of the setting in force (FEEDS.md).

## Building

Needs the .NET SDK (8 or newer) and a Railroader install for the game's dlls. Point the build at the game's
Managed folder (`Railroader_Data/Managed`) with a property or an environment variable.

```
dotnet build CaseyJones.csproj -c Release -p:ManagedPath="<game folder>/Railroader_Data/Managed"
```

or set `RAILROADER_MANAGED` to that folder once and run `dotnet build CaseyJones.csproj -c Release`. The build stops with a
message if the folder is missing. NuGet brings in Harmony and the .NET Framework reference assemblies.

The output is `bin/Release/CaseyJones.dll` and `bin/Release/0Harmony.dll`. To try it, put both beside `pack/Info.json`
in a folder named `CaseyJones` under `Documents/Railroader/Mods`, and turn on Allow DLL Mods in Preferences.

## License

MIT, see LICENSE.

Making a Railroader mod? Use our settings menu in yours: https://github.com/ashenmare/railroader-mod-settings
Made with AI assistance (Claude), every release reviewed and audited in game by a human.
