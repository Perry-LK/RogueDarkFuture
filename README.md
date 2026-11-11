# FutureDark

A playable desktop tactical deckbuilding roguelike built in **Godot 4.7.2 .NET / C#**.

Charcoal concrete, worn ivory typography, and layered 2.5D pieces. Dossier cards use muted **olive troops, rust actions, blue block, and purple board effects**. Successful player and enemy combinations bring the board to life with cyan/amber impacts and synthesized sound. No external art or audio downloads.

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
| **F5** while playing / **Save Run** | Manually save this run to its own slot |

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

- **Two developed acts**, each a seeded, generated nine-room route with branches, events, vendors, elite red zones and a boss; then a distinct **central finale**.
- A persistent, jagged street-map overview appears between acts. Districts fill with faded amber on liberation. The centre requires two-thirds of main districts: both in this two-act version.
- **40 card definitions**, six troops and one balanced sixteen-card starting deck. Rewards and vendors support action/status/control, formation, movement and supply-specialist builds.
- Tactical enemy squads move with piece-specific rules and commit plans at round start. Spotter/sniper precision, shield formations, commander bonuses, controller/riot-unit containment and reinforcements have explicit setup and disruption rules. Later sections combine more specialists.
- Irregular boards, deployment, chess-inspired movement, attack previews, independent block, temporary terrain effects, proximity intel and threat warnings.
- Five draws per round, nine-card hand cap, three AP, retained hands, and explicit two-card cleanup.
- Organisation HP persists; deployed and defeated troop cards return fresh next fight.
- A shared **run wallet**. Vendors sell cards, supplies and relics; buy back supplies at half price; and remove cards for a rising fee while preserving an eight-card minimum.
- **Twelve run-long relics** and **six supply types**. Carry three individual supplies; one free use per round during card play, with specialist exceptions.
- Five event families: safehouses, salvage/risky fights, black-market deals, workshops and narrative tradeoffs. Bosses and elites award an unowned relic when available.
- Built-in guide, card archives, combat log, keyboard controls, mute, reduced motion and optional threat haze. Accessibility settings persist locally.

The organisation starts with **64 HP** and **65 funds**. Normal victories give 20 funds and 3 recovery; elites give 42 funds; bosses give 65 funds and 18 recovery, before relic modifiers. Every enemy attack still threatens organisation HP regardless of distance. Block pools remain independent.

### Manual saving

Use **Save Run** or **F5** in-game. Each new run receives a unique identity and exactly one save file; saving again overwrites that run's slot. Multiple different runs can coexist without a fixed slot limit.

Loading restores the **entrance to the current room**, including its original HP, funds, deck, inventory, seed and generated content. Purchases, event choices and rewards inside the room are rolled back together, so loading cannot reroll stock or duplicate rewards. At a map/city boundary, saving retains that boundary. **Defeat deletes only the defeated run's slot.** There is no autosave.

Choose **Continue a saved run** on the title screen or **Menu -> Load Run**. Saves reside under Godot's `user://runs` directory (normally `%APPDATA%\Godot\app_userdata\FutureDark\runs` on Windows).

This remains a playable prototype. Further acts, the security faction, cross-run power progression, mobile, production art and exported installers remain out of scope. Relics reset when starting a new run.

## Validate

Package-free, deterministic C# rule tests:

```powershell
dotnet run --project .\Tests\FutureDark.Rules.Tests.csproj
```

The 29 behavioral groups cover damage isolation, phase legality, conservation, overflow, committed movement, squad combinations, action builds, every card's normal/upgraded resolution, supply limits, vendor transactions, save identity/rollback and complete campaigns.

Native Godot input/render smoke test (requires a graphical session):

```powershell
dotnet build .\FutureDark.csproj
& $Godot --path . -- --smoke-test --smoke-screen=combo
```

The smoke test injects Godot mouse/key events, validates a real combo, and exits. Optional `--screenshot=C:\absolute\path\capture.png` saves a viewport image. Other checks: `title`, `city`, `liberated`, `map`, `combat`, `guide`, `deck`, `dense`, `reduced`, `keyboard`, `enemyturn`, `vendor`, `event`, `act2`, `inventory`, `action`, `intel`, `saves`. Vendor/event/act-two/captured-city previews use deterministic fixtures; complete route behavior is tested by the console runner. The native `saves` case uses a temporary directory, checks manual save/load and automatic deletion on defeat, then removes its test files.

## Source and design

- [Rules](Scripts/Rules): engine-independent definitions, combat and run state.
- [Presentation](Scripts/Presentation): native Godot rendering, input, UI and synthesized feedback.
- [Game design](docs/GAME_DESIGN.md): original design plus the accepted prototype decisions.
- [Implementation outline](docs/GODOT_IMPLEMENTATION.md): architecture, development plan and verification contracts.
