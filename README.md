# eXSert – Beta Build
**eXSert** is a third-person character-action prototype set aboard a derelict research airship. Master stance-based combat, aerial launchers, guard-based counters, and fast traversal while fighting through autonomous security drones as the ship gradually opens up around you.
This **Beta build** represents a significantly expanded and refined version of the original Alpha slice, introducing multiple new playable zones, improved combat pacing, expanded traversal routes, and major visual upgrades across the airship.

---

# Build Overview

This Beta build features a complete playable progression from the opening elevator sequence to the **Engine Core boss arena**.

Major updates include:
* Fully playable progression from **start to final boss encounter**
* Major level design updates across **Crew Quarters, Hangar, and Conservatory**
* **Conservatory area unlocked via the new elevator lift system**
* Large-scale **visual upgrades and environmental polish**
* A showcase version of the **final boss arena with a basic boss AI**
* Reworked **checkpoint system** for smoother progression
* Improved **encounter spawning and combat flow**
* Expanded **interaction feedback with audio and camera guidance**

This build focuses on improving **gameplay clarity, pacing, and overall player experience**.

Some **narrative systems and advanced boss mechanics are still in development.**

---

# Installation & Launch

1. Download and unzip **`eXSert Beta Build.zip`**
2. Extract the folder anywhere outside `Program Files`
3. Run **`eXSert.exe`**

No additional setup or dependencies are required.

---

# Controls

## Gamepad (Xbox / PlayStation Layout)

| Action        | Input                         |
| ------------- | ----------------------------- |
| Move          | Left Stick                    |
| Look          | Right Stick                   |
| Light Attack  | X / Square                    |
| Heavy Attack  | Y / Triangle                  |
| Jump          | A / Cross                     |
| Dash          | Right Trigger                 |
| Guard / Parry | Right Shoulder                |
| Lock-On       | Right Stick Press or D-Pad Up |
| Switch Target | D-Pad Left / Right            |
| Interact      | B / Circle                    |
| Pause         | Start / Options               |

---

## Keyboard & Mouse

| Action        | Input              |
| ------------- | ------------------ |
| Move          | WASD               |
| Look          | Mouse              |
| Light Attack  | Left Mouse Button  |
| Heavy Attack  | Right Mouse Button |
| Jump          | Space              |
| Dash          | Left Shift         |
| Guard / Parry | E                  |
| Lock-On       | C                  |
| Interact      | F                  |
| Pause         | Escape             |

---

# Level Progression

The Beta build progresses through several sections of the airship:

1. Elevator
2. Cargo Bay
3. Crew Quarters
4. Hangar
5. Charging Station
6. Conservatory
7. Engine Core (Final Boss)

Each zone introduces new **combat encounters, traversal paths, and environmental mechanics** leading toward the final boss arena.

---

# Gameplay Flow

Players explore multiple sections of the airship while clearing combat encounters and unlocking traversal routes.

The game alternates between:

* **Combat arenas**
* **Traversal segments**
* **Environmental progression puzzles**

The final area features a **demonstration version of the boss encounter**, including a **basic AI that actively pursues the player**.

---

# Major Mechanics

## Combo System

Attacks chain into multi-stage combos.
Finishers deal increased damage and help control groups of enemies.

## Aerial Combat

Enemies can be launched into the air and followed with aerial attacks before finishing with plunge strikes.

## Guard & Parry

Guarding slows movement but increases combat control.

Successful **parries within a short timing window stun enemies**, allowing powerful follow-ups.

## Dash & Air Dash

Fast ground and aerial mobility allow players to reposition quickly and extend combo chains.

## Traversal & Platforming

Scaffolding, catwalks, vertical routes, and lift systems are integrated into combat arenas and exploration paths.

---

# Updated Interaction System

The interaction system has been significantly improved.

Players now receive:

* **Audio feedback** when attempting unavailable interactions
* **Camera guidance** when activating important objectives
* Clearer prompts when progressing through level mechanics

These improvements help guide players through complex environments while maintaining immersion.

---

# Checkpoint System

The checkpoint system has been redesigned to improve gameplay flow.

Updates include:

* More reliable respawn points
* Faster scene recovery after death
* Improved synchronization between encounters and progression triggers

These improvements allow smoother progression between combat encounters and level sections.

---

# Conservatory Progression Guide

Progression through **Crew Quarters and Conservatory** now follows a structured encounter and keycard sequence.

### Initial Progression

1. Enter **Room 1** (first room on the left)
2. Defeat all enemies
3. Collect the **Key** dropped by the encounter
4. Exit through the opposite door and move toward **Room 2**

### Lower Level Access

5. On the right side of the area, locate a **gap in the fence**
6. Follow the **descending ramp**
7. Navigate down through the **second floor toward the first floor**

While progressing downward:

* Enemy encounters must be cleared
* Enemies drop a **Key Card required to activate the lift system**

### Lift System

Activating the lift system allows players to return upward through the structure.

Players may continue exploring toward the **third floor**.

### Final Key Sequence

After clearing the third floor encounters:

* Enemies drop a **Key** to activate the third room lift
* Defeating enemies inside the room grants the **Golden Key**

The **Golden Key unlocks a console** on the third floor.

Activating this console opens a **maintenance hatch leading to the Engine Core**, where the final boss encounter takes place.

---

# Known Issues (Beta)

The following issues are currently known in the Beta build.

## Gameplay & Progression

* Starting a new game, returning to menu, then starting another may lock the game
* Interacting during dash may break player movement
* Lock-on movement occasionally behaves inconsistently

## UI & Settings

* Audio sliders may not update visually
* Settings changes in the main menu may not save correctly
* Objective UI updates inconsistently
* Brightness slider currently nonfunctional
* Combo Progression Manager toggle issue
* Motion blur cannot currently be disabled

## Controls & Input

* Interaction feedback occasionally updates slowly

## Audio

* Elevator audio may play during the opening cutscene
* Double jump sound effect volume is currently too quiet

## Environment & Level Issues

* Key cards may occasionally spawn floating
* Missing NavMesh in Crew Quarters may cause enemies to not chase the player
* Missing enemy zone in Hangar may prevent enemies from pursuing
* Hangar key ID assignment may occasionally fail
* Small collision gap between magnet and cargo in Cargo Bay
* Player may float slightly in Cargo Bay due to collider issues

These issues are currently under investigation and will be addressed in future builds.

---

# Removed Debug Shortcuts

Debug shortcuts used during Alpha testing have been removed.

The following features are no longer available:

* Scene Load Shortcuts
* Cargo Bay Progression Cheat

All progression paths now function through **normal gameplay flow.**

---

# Build Info

Engine: **Unity 6000.2.15f2**
Platform: **Windows 10 / 11 (DX11, URP)**
Milestone: **Beta**
Last Update: **March 8, 2026**

---

# AI Disclosure

During development of the **eXSert Beta Build**, Janitor's Closet Studio used:

* **GitHub Copilot** to assist with debugging compiler errors and refactoring during development.
* **ChatGPT** to help summarize known bug lists and restructure README documentation.

All gameplay systems, assets, and design work were created by **Janitor's Closet Studio's artists, designers, and engineers.**

---

Thank you for participating in the **eXSert Beta Playtest**.

Your feedback helps us refine:

* Combat feel
* Encounter pacing
* Level clarity
* Overall gameplay quality

Please submit bug reports and gameplay feedback through the **Google Form provided in the Discord server.**

If you'd like, I can also show you **3 small README tweaks that make GitHub repos look much more professional** (they're used in a lot of AAA studio public repos).
