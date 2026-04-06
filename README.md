# eXSert - Beta Build README

## Build Overview

This Beta build represents a significantly expanded and refined version of the eXSert Alpha Build.

The entire playable section of the game has been updated with improved combat encounters, clearer player guidance, expanded traversal routes, and major visual upgrades across multiple zones.

Key updates in this build include:

* Fully playable progression from start to final boss encounter
* Major level design improvements in Crew Quarters, Hangar, and Conservatory
* Conservatory now fully accessible using the new elevator lift system
* Large-scale visual updates and environmental polish
* A showcase version of the final boss arena with a basic boss AI
* Reworked checkpoint system for smoother progression
* Improved encounter system for better combat flow and performance
* Expanded interaction feedback, including audio and camera guidance

This build focuses on improving gameplay clarity, pacing, and overall player experience.

Narrative systems and some advanced boss mechanics are still in development.

## Repository Progress Since March 7, 2026

Local git history shows 509 commits landed after March 7, 2026.

Recent progress areas include:

* Conservatory progression and lift functionality
* Charging Station art, boss arena setup, and Augur encounter iteration
* Boss damage, mounting, ejection, and cage match fixes
* Checkpoint, death handling, and respawn improvements
* Visual effects attachment and environment polish
* Interaction, audio, camera guidance, and combat-flow fixes

---

## Installation & Launch

1. Unzip `eXSert Beta Build.zip`
2. Run `eXSert.exe`

No additional setup or external dependencies are required.

---

## Controls

### Gamepad (Xbox / PlayStation Layout)

Move - Left Stick  
Look - Right Stick

Light Attack - X / Square  
Heavy Attack - Y / Triangle

Jump - A / Cross  
Dash - Right Trigger

Guard/Parry - Right Shoulder

Lock-On - Right Stick Press or D-Pad Up  
Switch Target - D-Pad Left / Right

Interact - B / Circle  
Pause - Start / Options

---

### Keyboard & Mouse

Move - WASD  
Look - Mouse

Light Attack - Left Mouse Button  
Heavy Attack - Right Mouse Button

Jump - Space  
Dash - Left Shift

Guard/Parry - E

Lock-On - C  
Interact - F

Pause - Escape

---

## Level Order

1. Elevator
2. Cargo Bay
3. Crew Quarters
4. Hangar
5. Charging Station
6. Conservatory
7. Engine Core (Final Boss)

---

## Gameplay Flow

Players will progress through multiple zones of the airship while clearing combat encounters and unlocking traversal paths.

Each zone introduces new combat encounters and environmental routes that lead toward the final boss arena.

The final boss room has been prepared for demonstration and includes a basic boss AI that actively pursues the player.

---

## Major Mechanics

### Combo System

Attacks can chain into multi-stage combos. Finisher attacks deal increased damage and help control groups of enemies.

### Aerial Combat

Enemies can be launched into the air and followed with aerial attacks before finishing with plunge strikes.

### Guard & Parry

Guarding slows movement but increases combat control.  
Parrying enemy attacks during a short timing window stuns enemies.

### Dash / Air Dash

High-speed ground and aerial mobility allow players to reposition quickly and extend combos.

### Traversal & Platforming

Vertical routes, scaffolding, catwalks, and lift systems are integrated into combat arenas and exploration paths.

---

## Updated Interaction System

Interactions have been significantly improved.

Players will now receive proper audio feedback when attempting interactions that are unavailable.

Certain interactions will also trigger camera transitions, providing clearer visual guidance toward important objectives or progression routes.

---

## Checkpoint System

The checkpoint system has been fully redesigned.

Checkpoints now provide more reliable respawn points and smoother progression between encounters.

Combat encounters and level triggers have also been optimized for better loading performance and gameplay feedback.

---

## Conservatory Progression Guide

Progression in Crew Quarters now follows a structured encounter and keycard sequence.

1. Enter Room 1 (first room on the left) and defeat all enemies.
2. Collect the Key dropped by the encounter.
3. Exit through the opposite door and move toward Room 2.
4. On the right side of the area, find a gap in the fence leading to a ramp downward.
5. Use the ramp to descend to the lower floors.
6. Navigate the second floor and locate the opening that leads to the first floor.

While progressing downward:

* Players must defeat enemy groups along the path.
* These enemies will drop a Key Card required to activate the lift system in Room 2.

Using the lift system allows players to ascend the structure.

Players may choose to continue exploring upward toward the third floor.

After clearing the encounters on the third floor:

* Enemies will drop a Key required to activate the third room lift.
* Inside the third room, defeating the enemies will reward the player with a Golden Key.

The Golden Key unlocks a console on the third floor.

Interacting with this console allows players to open the maintenance hatch, which leads down to the Engine Core, where the final boss encounter takes place.

---

## Known Bugs

The following issues are currently known in the Beta build.

### Gameplay / Progression

* Entering a New Game, returning to Main Menu, then attempting to start another New Game may lock the game.
* Interaction during dash may break player movement and cause dash to become locked after restarting.
* Player movement during attacks can behave inconsistently when lock-on is active.

### UI / Settings

* Audio sliders sometimes do not update visually in the Settings Menu.
* All settings changed in the Main Menu may not save properly.
* Objective UI does not update correctly.
* Brightness slider currently does not function.
* Combo Progression Manager fail to toggle correctly.
* Motion blur cannot currently be disabled in the settings.

### Controls / Input

* Interaction feedback occasionally updates slowly.

### Audio

* Elevator audio may play during the initial cutscene.
* Double jump SFX volume is currently too quiet.

### Environment / Level Issues

* Card keys may occasionally spawn floating.
* Missing NavMesh in Crew Quarters causes enemies to not chase after player.
* Missing enemy zone in Hangar causes enemies to not chase after player.
* Hangar key ID assignment may fail, allowing players to interact with consoles before acquiring proper keycard.
* Small collision gap between magnet and cargo in Cargo Bay.
* Player may slightly float in Cargo Bay due to collider issues.

These issues are currently under investigation and will be addressed in future builds.

---

## Removed Debug Shortcuts

Debug shortcuts used during Alpha testing have been removed.

The following features are no longer available:

* Scene Load Shortcuts
* Cargo Bay Progression Cheat

All encounter systems and progression paths are now fully functional within the intended gameplay flow.

---

## Build Info

Engine: Unity 6000.2.15f2  
Platform: Windows 10 / 11 (DX11, URP)  
Milestone: Beta  
Last Update: March 8th, 2026

---

## AI Disclosure

During the production of eXSert Beta Build, Janitor's Closet Studio utilized GitHub Copilot in debugging and iterating compiler errors during building, and ChatGPT to summarize the known bug lists and quickly iterate the current README document to reflect the updated state of the build compared to previous submissions.

All assets and scripts are created by Janitor's Closet Studio's artists, composers, and engineers.

---

Thank you for participating in the eXSert Beta playtest.

Your feedback helps us refine combat feel, encounter pacing, level clarity, and overall gameplay quality.

Please submit bug reports and gameplay feedback through the Google Form provided in the Discord server.
