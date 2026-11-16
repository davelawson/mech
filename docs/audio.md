# Audio

Every sound plays through a **cue**, a name in `Cue` (`Scripts/Sound.cs`). Call sites name the cue; `SoundBank.Cues` maps it to files, a bus, a base level, random pitch spread, a voice cap and a cooldown. To change a sound, change its files in that table. No other code changes. A cue with several files picks one at random on each play.

## Buses and settings

- Buses: **Master**, with **Music**, **Sfx** (battlefield) and **Ui** (interface) sending to it. `SoundBank.EnsureBuses` creates them at startup, so the project needs no bus-layout resource.
- Main menu **Audio** sets Master, Music, Effects and Interface levels from 0 to 100 in steps of 5, plus **Mute all sound**.
  - A level of 0 mutes its bus.
  - Changes apply and save at once to `user://settings.cfg` (`CommandApp.SettingsPath`), outside every save slot.
  - Defaults: 80, with music at 60.
  - Right-click, Escape and **Back** close the panel.

## Interface

- Every button in the app, office hotspots included, ticks on mouse hover while enabled and clicks when pressed. `CommandApp` connects each button as it enters the tree, so new buttons need no wiring.
- `Ui.Sound(button, cue)` replaces a button's press sound; `Ui.Silent(button)` removes it.
  - **Confirm:** Establish Company / Confirm Lance, Apply, Quick Launch, Accept Mission, Launch, Start Campaign.
  - **Back:** Back and Close buttons, Cancel Journey, Cancel Repair, and dialog actions named Close, Cancel or Back.
- Right-click and Escape back gestures play the back sound when they close something.
- **UiError** sounds when time cannot advance in co-op.

## Battlefield

Battlefield sounds are positional. They play in the battlefield's own 3D world, heard by a listener that hangs above the camera's ground focus, higher as the view zooms out. Distant fights are quieter and pan left or right with the camera. A joined co-op player hears the replicated battle the same way.

Sounds follow what the player can see. An undetected enemy's shots, steps and jets are silent, as their effects are.

| Moment | Cue | Notes |
| --- | --- | --- |
| Laser, PPC, flamer, autocannon or missile rack fires | `FireSmallLaser`, `FireLargeLaser`, `FirePpc`, `FireFlamer`, `FireAutocannon`, `FireMissiles` | At the muzzle. Pitch falls with caliber: AC/20 lowest, AC/2 highest; LRM-20 lower than LRM-5. One launch sound per salvo |
| Machine gun round | `FireMachineGun` | Per tracer round, capped |
| Laser hits | `LaserHit` | At the struck location |
| Shell lands (hit or ground) | `ShellExplosion` | When its fireball appears, pitched by caliber |
| PPC bolt lands | `PpcBurst` | When its burst appears |
| Missile lands (hit or ground) | `MissileExplosion` | Per missile, capped |
| Machine gun round lands | `BulletHit` / `ImpactGround` | Hit or miss |
| Location destroyed | `LocationDestroyed` | Per destroyed location |
| Mech destroyed | `MechDestroyed` | Replaces the location sounds of the killing shot |
| Critical hit / ammunition explosion | `CriticalHit` / `AmmoExplosion` | At the critical location |
| Foot planted | `Footstep` | Each foot as its swing ends (`MechModel.Footfalls`). Heavier mechs thud lower and louder |
| Jumping | `JumpJets` | Loops from the mech while airborne; `Landing` on touchdown |
| Knockdown | `Knockdown` | When the fallen mech hits the ground; `StandUp` as it starts to rise |
| Heat at or above 20 | `HeatWarning` | Flat alarm every 2 s while any active friendly mech is that hot and the battle runs |

## Music

| Context | When | Track |
| --- | --- | --- |
| Menu | Main menu, Audio, lobby and draft | *Menu Music* |
| Strategy | Office, star map, hangar and the other strategic screens | *Another space background track* |
| Combat | A battle in progress | *Battle Theme A* |

Tracks loop. A change of context crossfades over 1.5 s, and the playing context never restarts.

## Voice limits

Each cue has a voice cap (for example 6 for shell explosions and 8 for footsteps) and a minimum gap between plays. A missile salvo or a long exchange therefore never stacks dozens of copies. The interface has 24 flat voices; each battlefield world has 48 positional ones. `SoundPlayer.Recent` records the cues actually played, which lets tests check sound routing in headless runs.

## Sources and licences

Every sound and track is **CC0** (public domain); crediting is optional, but they are listed here. Files keep their original names under `Assets/Audio/<pack>/`.

| Folder | Source | Author | Licence |
| --- | --- | --- | --- |
| `kenney-interface/` | [Interface Sounds](https://opengameart.org/content/interface-sounds) (also [kenney.nl](https://kenney.nl/assets/interface-sounds)) | Kenney | CC0 (`License.txt`) |
| `kenney-scifi/` | [Sci-Fi Sounds](https://opengameart.org/content/sci-fi-sounds) (also [kenney.nl](https://kenney.nl/assets/sci-fi-sounds)) | Kenney | CC0 (`License.txt`) |
| `cc0-sfx/` | [100 CC0 SFX](https://opengameart.org/content/100-cc0-sfx) | rubberduck | CC0 |
| `music/menu-music.ogg` | [Menu Music](https://opengameart.org/content/menu-music) | mrpoly | CC0 |
| `music/observing-the-star.ogg` | [Another space background track](https://opengameart.org/content/another-space-background-track) | yd | CC0 |
| `music/battle-theme-a.mp3` | [Battle Theme A](https://opengameart.org/content/battle-theme-a) | cynicmusic | CC0 |
