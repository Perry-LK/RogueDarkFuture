# FutureDark

A playable desktop tactical deckbuilding roguelike built in **Godot 4.7.2 .NET / C#**.

Charcoal concrete, worn ivory typography, and layered 2.5D pieces. Dossier cards use muted **olive troops, rust actions, blue block, and purple board effects**. Successful player and enemy combinations bring the board to life with cyan/amber impacts and synthesized sound. No external art or audio downloads.

### City campaign

Act routes now scroll horizontally through variable-length angular streets. Branches reconnect only at encounters: routes neither cross nor share overlapping segments. Use the mouse wheel, drag the map, drag its scrollbar, or use Left/Right and Page Up/Down. Home/End focus the entry/boss; Tab automatically reveals off-screen rooms.

Textured illustrated emblems replace the map's letter glyphs and the main HUD, card, supply and intent symbols. HUD icons expose descriptions on hover or keyboard focus; card rules and important phase actions retain text.

Five districts now lead to Central Command. Secure four to unlock the finale, or tackle the fifth first. The approved route/icon preview is retained alongside raised battlefields, linked events, a simpler starter deck, permanent card-pack unlocks and room-entry autosaves.

## Play

Requirements:

- **Godot 4.7.2 .NET**, not the standard edition.
- **.NET SDK 10.0.300** (or a later 10.0.3xx patch selected by `global.json`); project targets .NET 8.

From this directory in PowerShell:

```powershell
dotnet build .\FutureDark.csproj
```

Import [project.godot](project.godot) in the Godot .NET project manager, then press **F5**. Establish a signal, select **Outer Blocks** on the city overview, then click an available encounter icon and confirm **Enter room**. The in-game **Guide** explains the rules.

Alternatively, with `$Godot` set to the path of your Godot .NET executable:

```powershell
& $Godot --path .
```

### Controls

| Input | Action |
|---|---|
| Click card / **1-9** | Select a targeted card; play an untargeted card immediately |
| Click highlighted tile/piece | Deploy, target a card, or move a selected troop |
| Click selected troop again / **Hold** | Hold its position, spending remaining moves |
| Phase button / **Space** | Close card play, resolve attacks, confirm selected discards, or claim the encounter |
| **Right-click / Escape** | Cancel selection or close an overlay |
| **Tab / Shift+Tab**, **Enter** | Focus and activate interface controls, cards, pieces and tiles |
| **H**, **M**, **R** | Field guide, mute, reduced motion |
| **Escape** with nothing selected | Pause; access options or save and exit |

Hover or keyboard-focus a hand card to read its full rules in the right-hand inspector. Click a troop for movement and automatic attack previews. Hover/click an enemy to inspect its special role and observable plan. Recon adds numbers to intent badges and movement/target previews to the board. The top bar shows card play, board orders, cleanup, or enemy response; brief notices announce turns.

Cards deal in sequence, lift on hover, travel on play/discard, and reflow without a fan. Reduced motion removes these movements. The UI uses a scalable, centered tactical canvas to remain readable on 1080p and ultrawide displays rather than stretching the cards.

## Make the combinations

| Combo | Trigger | Result |
|---|---|---|
| **Ghost Signal** | Recon Sweep + a Marksman's attack | +3 damage |
| **Momentum** | Forced March + a Runner moving at least twice + its attack | +3 damage |
| **Kill Circuit** | Coordinated Strike + an attack on an Exposed Ground enemy | +3 additional damage, beyond Strike's +2 and exposure's +2 |
| **Breach** | Mark an enemy, then land a direct hit | Consume one mark for +3 damage |
| **Meltdown** | Apply corrosion, then Detonate Circuit | Consume corrosion for twice its stack count in additional damage |
| **Dead End** | Push/pull an enemy into a trap, or intercept its planned movement | Trigger the trap/mine once |
| **Fire Support** | Breach Charge with a nearby friendly piece | +5 action damage |
| **United Front / Hold Fast** | Rally a formation / Entrenched Fire without moving | Additional troop attack power |
| **Hotwired** | Amplify a supply, then connect its damaging effect | Improved one-use effect |

Different combos stack. Preparing one is deliberately quiet. Color, impact trails, camera impulse and sound trigger **only on a combo hit that removes enemy HP**, not on setup, misses, ordinary attacks, or fully absorbed hits.

**Example:** A normal Marksman hits for 4. Recon raises that to 7; Strike and Exposed Ground together raise that to 14, connecting both Ghost Signal and Kill Circuit.

## Campaign, economy and saves

- **Five districts**, each a seeded, generated nine-room route with branches, events, vendors, elite red zones and a specialist boss; then a short **central finale**.
- The Outer Blocks, Foundry Belt, Signal Ward, Civic Spire and Glass Ward progress from abandoned streets to high-rise territory. Jagged districts fill with faded amber on liberation. Four completions unlock Central Command; the fifth is optional.
- **40 card definitions** and six troop types, unlocked across future runs. The ten-card Resistance starter contains two Runners, three Signal Spikes, three Emergency Networks, one Recon Sweep and one Forced March.
- Tactical enemy squads move with piece-specific rules and commit plans at round start. Spotter/sniper precision, shield formations, commander bonuses, controller/riot-unit containment and reinforcements have explicit setup and disruption rules. Later sections combine more specialists.
- Generated **7x6 to 9x8** battlefields: gantries, terraces and raised flanks, ramps, solid walls, destructible cover and persistent hazards. Normal pieces use ramps; Breachers leap one level. Ranged attacks gain +1 against lower units, never against organisation HP. Walls/cover stop troop fire, not proximity intel. Pushes/pulls cannot cross unconnected ledges.
- Five draws per round, nine-card hand cap, three AP, retained hands, and explicit two-card cleanup.
- Organisation HP persists; deployed and defeated troop cards return fresh next fight.
- A shared **run wallet**. Vendors sell cards, supplies and relics; buy back supplies at half price; and remove cards for a rising fee while preserving an eight-card minimum.
- **Twelve run-long relics** and **six supply types**. Carry three individual supplies; one free use per round during card play, with specialist exceptions.
- Safehouses, salvage/risky fights, black-market deals and workshops join linked courier/relay/sabotage and broker/dividend/pursuit stories. Decisions affect later costs and enemies. Bosses and elites award an unlocked, unowned relic when available.
- Built-in guide, card archives, combat log, keyboard controls and a persistent unlock collection.

The organisation starts with **64 HP** and **65 funds**. Normal victories give 20 funds and 3 recovery; elites give 42 funds; bosses give 65 funds and 18 recovery, before relic modifiers. Every enemy attack still threatens organisation HP regardless of distance. Block pools remain independent.

### Autosaves and unlocks

Keep up to **three unfinished runs**. The game autosaves room entrances and route/city boundaries; there is no manual save command. **Continue** resumes the most recently played run; **Saved runs** lets you choose or explicitly abandon a slot. Pause offers **Save & main menu** and **Save & quit**. Closing the window also saves before exit.

Loading restores the **entrance to the current room**, including original HP, funds, deck, inventory, narrative flags and seeded content. Purchases, event choices and rewards roll back together, preventing rerolls and duplicated rewards. Victory/defeat settles XP once and removes only that run's slot. Explicit abandonment awards no XP.

Wins and losses earn `min(160, victories * 8 + completed districts * 18 + victory bonus of 40)` XP. Packs unlock at 60, 140, 240 and 360 cumulative XP: Disruption, Chemical Warfare, Formations and Logistics. Rewards, shops and relic pools use the tier captured when that run began. No permanent stat boosts; three to five substantial runs can open the collection, while short early losses earn less.

Data lives under Godot's `user://`: `runs` contains version-2 checkpoints, `progress.json` the unlock profile, and `settings.cfg` options. On Windows this is normally `%APPDATA%\Godot\app_userdata\FutureDark`. Legacy version-1 manual saves are not supported. No legacy save files were present during this update.

### Options

Title and pause menus provide master, ambience/music, combat and UI volumes (all initially 50%), global mute and mute on focus loss. Quiet synthesized ambience and interface clicks sit beneath combo feedback.

Choose windowed/borderless/fullscreen, resolution, UI fit, text scale, shake strength, combo intensity, reduced motion and independently accelerated 2x animations. These settings persist separately from runs. Zero combo intensity removes impact effects, not damage numbers.

This remains a playable prototype. Multiplayer, the playable Security faction, mobile, production art and exported installers remain deferred. Relics and run stats reset on each new run.

## Validate

Package-free, deterministic C# rule tests:

```powershell
dotnet run --project .\Tests\FutureDark.Rules.Tests.csproj
```

The 34 behavioral groups cover damage isolation, phase legality, conservation, committed plans, squad combinations, every card's normal/upgraded resolution, transactions, room rollback, three save slots, XP idempotency, linked stories, cover, high ground, ramps, hazards and complete campaigns. Route geometry checks 640 seeded district maps; terrain connectivity checks 144 layouts. The deterministic policy completes one of six campaign seeds in the current baseline; this is a regression result, not a target human win rate.

Native Godot input/render smoke test (requires a graphical session):

```powershell
dotnet build .\FutureDark.csproj
& $Godot --path . -- --smoke-test --smoke-screen=combo
```

The smoke test injects Godot mouse/key events, validates a real combo, and exits. Optional `--screenshot=C:\absolute\path\capture.png` saves a viewport image. Use the Windows GUI `.exe` directly for captured automation rather than its `_console.exe` wrapper.

Other checks: `title`, `icons`, `city`, `liberated`, `map`, `mapscroll`, `combat`, `guide`, `deck`, `dense`, `reduced`, `keyboard`, `enemyturn`, `vendor`, `event`, `act2`, `inventory`, `action`, `intel`, `saves`, `terrain`, `options`, `pause-options`, `unlocks`, `end`. `mapscroll` checks actual scrolling and post-scroll hitboxes. `terrain` targets raised cover on a late-act board. `options` checks persistence, UI-fit cycling, audio gain and focus muting. `saves` checks autosave, Save & main menu, Continue rollback and deletion on defeat. Each smoke case isolates all persistence in its own temporary directory and removes it afterward. Specialist combo and late-campaign previews use explicit fixtures, not the player's basic starter.

## Source and design

- [Rules](Scripts/Rules): engine-independent definitions, combat and run state.
- [Presentation](Scripts/Presentation): native Godot rendering, input, UI and synthesized feedback.
- [Game design](docs/GAME_DESIGN.md): original design plus the accepted prototype decisions.
- [Implementation outline](docs/GODOT_IMPLEMENTATION.md): architecture, development plan and verification contracts.
