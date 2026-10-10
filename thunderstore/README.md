# SpawnTracker

Shows where creatures respawn around you and how long until they come back.

Press **F8** to toggle.

## Features
- Floating labels over every nearby spawner with the creature, distance and status.
- A list of the closest spawners on the right side of the screen.
- **Fixed spawners** (crypt Draugr and Skeletons, tar pit Growths, location creatures):
  `Alive`, `Respawns in 12:34`, `Ready` and what is blocking it (night only, too far, inside a player base, world progress).
- **Spawn areas** (Greydwarf nests, bone piles, Draugr spawners): countdown to the next spawn and how many creatures are near versus the maximum.

## Configuration
`BepInEx/config/nnikoloo.SpawnTracker.cfg`

| Setting | Default | |
|---|---|---|
| ToggleKey | F8 | Show or hide |
| Range | 80 | Max distance in metres |
| ShowLabels | true | Floating labels |
| ShowList | true | On-screen list |
| ListSize | 8 | Entries in the list |

## Notes
- Read-only: it never changes spawns or world data. Client-side only.
- Timers use world time, so sleeping advances them.
- In multiplayer, spawn area timers are only visible to the player who owns that zone.
- Random world spawns (the ones without a fixed point) are not shown.
