# FutureDark: Godot/C# Implementation Outline

This document retains the development plan and verification contracts. A playable short-run implementation now exists. The accepted prototype decisions at the top of the [game design document](GAME_DESIGN.md) take precedence over the original open decisions below.

## Current implementation

The expansion now includes two generated acts and a central finale, forty cards, tactical enemy squads, vendors, supplies, relics and manual room-entry saves. The **Expanded prototype** section of the [design document](GAME_DESIGN.md) overrides older milestone assumptions.

- [project.godot](../project.godot) pins the Godot 4.7 .NET project; [FutureDark.csproj](../FutureDark.csproj) pins `Godot.NET.Sdk/4.7.2` and targets .NET 8. [global.json](../global.json) selects .NET SDK 10.0.300 with patch roll-forward.
- [Definitions.cs](../Scripts/Rules/Definitions.cs) contains typed immutable C# records and the small authored content catalogue.
- [Combat.cs](../Scripts/Rules/Combat.cs) owns seeded shuffles, card zones, phases, queries, recipients, intents, temporary effects and combo events.
- [CombatActions.cs](../Scripts/Rules/CombatActions.cs) resolves typed actions, supplies, status damage and terrain triggers. [EnemyTactics.cs](../Scripts/Rules/EnemyTactics.cs) creates committed plans, forecasts squad bonuses and resolves stable movement/action order.
- [RunState.cs](../Scripts/Rules/RunState.cs) owns the route, deck, organisation HP handoff, events, rewards and terminal transitions. During combat, `RunState.Hp` reads the battle's authoritative HP, not a second UI copy.
- [ExpansionContent.cs](../Scripts/Rules/ExpansionContent.cs) authors the expanded card pool, specialist squads, relics and supplies.
- [RunSaveStore.cs](../Scripts/Rules/RunSaveStore.cs) stores validated, versioned snapshots using a unique run ID as the filename. Writes go through a flushed temporary file and replacement; loading reconstructs the seed-generated route and optionally re-enters the saved room. Mid-room effects never enter the saved entrance snapshot.
- [Main.tscn](../Scenes/Main.tscn) hosts [GameView.cs](../Scripts/Presentation/GameView.cs), a scalable Godot Control with shared draw/input coordinates, depth-sorted vector pieces, explicit command feedback, keyboard focus, modals and combo-only audio/animation. Logical resolution commits once before presentation; inputs lock briefly during resolution.
- The view's [campaign partial](../Scripts/Presentation/GameView.Campaign.cs) handles city/route/event/vendor/save interfaces; its [tactical partial](../Scripts/Presentation/GameView.Tactical.cs) handles inspection, category art, input-driven card travel, movement interpolation and turn announcements. Save failures and unreadable slots are surfaced in the UI and Godot diagnostics.
- [Tests](../Tests) contains a dependency-free console rule runner and deterministic tactical policy. Native smoke scenarios inject real Godot mouse/key events and optionally save viewport captures.

**Deliberate simplifications:** Authored definitions use immutable typed C# records rather than Godot Resource assets so the compact catalogue and simulation can be tested without the engine. All screen presentations share a single native Control instead of separate scene hierarchies. Neither choice adds a generic effect engine or external UI framework; content can migrate to Resources if editor-based authoring becomes necessary.

**Validation:** The package-free runner covers 29 behavioral groups, including both acts and the finale, normal/upgraded resolution for all cards, squad-combo disruption, deterministic map edges, single-use transactions, inventory limits and file-backed room rollback. Native scenarios cover city/act maps, combat, enemy response, card animation, vendor/events, inventory, keyboard-only deployment, maximum hand and reduced motion. These checks do not replace human balance or accessibility playtesting. Instructions are in the [README](../README.md#validate).

## 1. Technical direction

**Confirmed:** Godot, C#, solo development, desktop first, 2D board with 2.5D characters.

**Proposed foundation:**

- Use the .NET edition of a pinned stable Godot 4 release and its supported .NET SDK.
- Start with a simple 2D renderer appropriate to the eventual visual style; avoid assuming 3D is required.
- Use plain C# for rule calculations and Godot nodes for scene lifecycle, input, and presentation.
- Use typed Godot Resources for authored card, troop, enemy, board, and encounter definitions.
- Keep mutable battle state separate from shared Resource assets.
- Prefer explicit phases and small direct methods over a generic rules engine.
- Inject a seeded random source for shuffling and route generation so failures can be reproduced.
- Ship one authored area first; procedural generation is not required.

**Open before coding:** Choose the exact engine/SDK versions and accept or revise the proposed gameplay defaults.

## 2. Suggested project layout

These are proposed directories and responsibilities, not existing code.

| Directory | Purpose |
|---|---|
| `Scenes` | Map, combat, encounter result, and reusable UI scenes |
| `Scripts/Rules` | Grid queries, targeting, damage, card zones, and phase rules |
| `Scripts/Combat` | Combat state and controller integration |
| `Scripts/Run` | Run state, map traversal, rewards, and encounter transitions |
| `Scripts/Presentation` | Board views, input, previews, animation, and HUD |
| `Data/Cards` | Authored card definitions |
| `Data/Troops` | Troop stats and movement/attack patterns |
| `Data/Enemies` | Enemy definitions and repeating routines |
| `Data/Boards` | Valid cells, deployment cells, and terrain |
| `Data/Encounters` | Fights, events, and the prototype area graph |
| `Tests` | Rule tests when test tooling is introduced |

Keep the initial implementation compact. Split files when they acquire distinct responsibilities, not merely to match this table.

## 3. Main scenes and ownership

### Run scene

Owns the active run and switches between map, combat, events, rewards, and run results.

`RunState` stores faction, organisation HP/max HP, the run deck with instance IDs and upgrades, route position, and run seed. Add currency only if an actual prototype reward/event needs it.

Do not store organisation HP independently in the map and combat UIs. They must read the authoritative run/combat state.

### Map scene

Shows the current area's node graph, selected route, completed nodes, and legal next nodes. Selecting a node launches exactly one encounter.

### Combat scene

Suggested children:

- Board view and tile overlays.
- Troop/enemy views.
- Hand and AP display.
- Organisation HP/block display.
- Enemy intent displays.
- Phase controls and discard selector.
- Combat result overlay.

`CombatController` owns the phase transition flow and calls rule methods. UI code requests actions; it does not calculate damage or mutate card piles directly.

### Event/result scenes

Present explicit choices and apply their effects once. Returning to the map preserves organisation HP and deck changes while clearing encounter-only state.

## 4. Data model

### Immutable authored definitions

| Definition | Essential content |
|---|---|
| Card | ID, faction/shared pool, category, AP cost, targeting, effects, upgrade |
| Troop | ID, base HP, movement pattern, attack pattern, damage, presentation |
| Enemy | ID, base stats, ordered repeating action sequence |
| Enemy action | Category, attack damage/pattern or explicit non-attack effect |
| Board | Valid coordinates, deployment cells, terrain |
| Encounter | Board, enemy placements, reward configuration |
| Event | Choices, requirements, explicit consequences |
| Area | Nodes, edges, encounter references, terminal boss |

Use enums and typed effect fields for the small initial effect set. Avoid an expression language, reflection-driven effect scripting, or a complex inheritance tree.

### Runtime instances

| State | Essential content |
|---|---|
| Card instance | Unique ID, definition ID, upgrade state, current encounter zone |
| Troop instance | Unique ID, source card ID, coordinate, HP, block, remaining allowances |
| Enemy instance | Unique ID, definition ID, coordinate, HP, block, routine index |
| Tile effect | Source, coordinate(s), typed effect, expiry rule |
| Combat state | Phase, round, AP, piles, units, board effects, block, outcome |
| Intel state | Visibility sources and explicit card effects with durations |

A troop's source card must be traceable throughout deployment, defeat, recovery, and combat cleanup. Never identify two duplicate cards solely by definition ID.

## 5. Rules contracts

### Board and patterns

- Represent playable cells as integer grid coordinates with membership checks.
- Keep coordinate-to-screen conversion out of movement and targeting rules.
- Validate deployment against the starting zone, occupancy, and tile effects.
- Movement patterns and attack patterns are separate definitions.
- Centralise legal-target queries so previews and action resolution agree.
- Define whether movement can jump and whether intermediate cells matter for each pattern.
- Prevent movement to missing or inaccessible cells, including holes in irregular boards.
- Keep movement denial, attack occlusion, and vision occlusion separate.

### Phase controller

Suggested phases: round start, draw, card play, troop movement, troop attack, discard, enemy resolution, round end, victory, defeat.

Expose legal commands for the active phase. Reject invalid requests with clear UI feedback and development diagnostics; do not silently apply partial actions.

Lock gameplay input during resolution animations. Commit logical effects once, then animate their results; repeated clicks must not replay effects.

### Cards and draw

- Validate AP, target, phase, and card ownership before spending or moving a card.
- Resolve five draw attempts, checking the nine-card hand limit on each.
- Reshuffle discard only when a draw needs a card and the draw pile is empty.
- Do not shuffle in-play or unavailable troop cards into the draw pile.
- End-of-turn discard requires `min(2, hand count)` selected cards.
- Rebuild encounter zones from the run deck at each combat start.
- Keep upgrades and permanent deck modifications separate from encounter effects.

### Damage and protection

For an attack, determine recipients from authoritative board state, then apply:

1. One damage application to the organisation, regardless of board position or obstacles.
2. One application of the same base damage to each troop covered by that enemy's attack.
3. Independent block consumption for each recipient.
4. Troop defeat and card unavailability where HP reaches zero.
5. Immediate organisation defeat if HP reaches zero.

Keep organisation damage outside any troop line-of-sight or target-availability condition. This is the most important regression boundary.

Stop enemy resolution once the run ends. Only advance routines for actions that actually resolve under the chosen outcome rules.

### Enemy routines and intel

- Read the current action from the routine index.
- Advance and wrap the index after that enemy resolves its action.
- Resolve enemies in a stable, documented order; proposed default is encounter spawn order.
- Recalculate visibility after movement, deployment, death, and relevant card effects.
- Keep exact enemy action data in the simulation, but expose a filtered presentation model.
- Hidden UI must not leak exact damage through previews or debug overlays intended for players.
- Compute the danger warning from the authoritative upcoming enemy actions, including unseen enemies.
- Respect intel duration and hide exact details when a revealing source expires.

### Combat cleanup

- Persist organisation HP, card upgrades, and explicit run-level rewards.
- Clear troop HP, troop block, organisation block, tile effects, intel effects, routine state, and encounter card zones.
- Return all run-deck troop cards, including defeated troops, to next combat's setup.
- Apply boss recovery once at the intended transition.

## 6. UI and input outline

Desktop prototype controls are **proposed**:

- Click a card to inspect/select it, then click a valid target.
- Click a troop during its movement phase to display legal moves and automatic attack previews.
- Click its current cell or a Hold control to stay still.
- Right-click or Escape cancels an uncommitted selection.
- Explicit controls advance from card play to troop actions and confirm mandatory discard.
- Hover/focus displays exact rules text and relevant numerical values when information is known.

Always distinguish selected tiles, valid movement, deployment, friendly attack patterns, enemy threats, and denied cells using shapes/icons as well as colour.

Show separate organisation and troop block values. Display only the rough enemy action when intel is absent. Make red haze optional and offer a non-flashing text/icon warning.

For later mobile compatibility, avoid rules that depend on hover, keep input routed through semantic commands, and plan scalable layouts. Do not build touch controls during the first prototype.

## 7. Development milestones

Each milestone produces a playable or testable increment. Complete its exit criteria before expanding content.

### M0: Lock the small rules contract

**Work:** Pin Godot/.NET versions; decide AP, attack targeting/order, discard timing, block expiry, and initial vision. Accept a movement-only obstacle prototype or resolve attack-blocking geometry first.

**Exit:** A written example round has one unambiguous outcome, including organisation damage, troop damage, discard, and block expiry.

### M1: Board sandbox

**Work:** Load one board, deploy placeholder troops in marked starting cells, implement movement patterns and attack overlays.

**Exit:** At least two troop movement patterns work on an irregular board; illegal deployment and moves are rejected; highlights match rule queries.

### M2: Complete one combat round

**Dependency:** M1.

**Work:** AP, hand/piles, troop deployment cards, automatic attacks, one enemy routine, independent organisation/troop block, round phases, defeat.

**Exit:** A multi-round fight runs without debug state editing. An enemy attack damages the organisation with no troop nearby. A troop can die without its card reappearing that fight.

### M3: Tactical vertical slice

**Dependency:** M2.

**Work:** A small card set covering all four categories, additional troops/enemies, intel, danger warning, movement-denial terrain, temporary effects.

**Exit:** A player can choose organisation protection versus troop protection, reveal and lose intel, and use board effects. Previews and resolved effects agree.

### M4: Small playable run

**Dependency:** M3.

**Work:** One authored branching area, fight/event transitions, healing, deck modification/upgrades, boss, victory/defeat, restart.

**Exit:** Complete the prototype route without developer tools. Organisation HP persists; troops reset; rewards and boss recovery apply once; restart clears the previous run.

### M5: Playtest and presentation

**Dependency:** M4.

**Work:** Placeholder-to-2.5D presentation pass, readable intent and block UI, feedback, accessibility options, balance iteration.

**Exit:** New testers can explain damage and phase outcomes, use intel, and complete or deliberately restart a run without guidance. Record confusing situations before adding content.

### Beyond the prototype

Only after the core loop works: security leader, expanded card pools, additional acts, finale, persistent unlocks, production assets, and eventual mobile investigation.

## 8. Verification matrix

The package-free C# test runner now exercises these contracts. Continue using this matrix for regression tests and focused in-engine checks as content changes.

| Area | Required check |
|---|---|
| Organisation damage | Enemy attack damages organisation with zero deployed troops |
| Obstacles | No obstacle or distance removes organisation attack damage |
| Independent block | Blocking organisation does not protect troops; blocking troops does not protect organisation |
| Multiple recipients | Two affected troops each receive the full base attack, independently blocked |
| Non-attack step | Rest/reinforce does not accidentally cause organisation damage |
| Block timing | Organisation block survives until attacks and expires at round end |
| Troop defeat | Defeated troop card is unavailable until combat ends or an explicit recovery effect |
| Reset | Troops return next fight at fresh HP; organisation HP persists |
| Draw | Five draw attempts with normal reshuffling |
| Hand cap | At nine cards, overflow enters discard; hand never exceeds nine |
| Empty piles | Drawing with empty draw/discard ends safely without a loop |
| Discard | Zero/one/two/three-card hands require zero/one/two/two discards |
| Card conservation | Each card instance is in exactly one zone; no duplicate deployment |
| Upgrades | Permanent upgrades survive fight transitions and are not applied twice |
| Movement | Hold is legal; missing cells and denied cells reject movement |
| Pattern attacks | Every highlighted target follows the agreed attack-order contract |
| Routine | Enemy sequence repeats exactly and remains stable through presentation |
| Intel | Entering/leaving visibility reveals/hides exact action detail correctly |
| Warning boundary | Just below/at the chosen threshold gives the expected warning state |
| Outcome | Organisation at zero ends the run and prevents subsequent queued actions |
| Map | Only connected next nodes can be selected; encounters/rewards resolve once |
| Boss transition | Recovery applies once; next encounter begins with cleared combat state |
| Restart | New run contains no state from the defeated run |

Use deterministic seeds and compact state snapshots for failures. Run the smallest relevant test set after each rules change, then an in-engine fight for scene/input changes.

## 9. Main risks and controls

| Risk | Control |
|---|---|
| Unavoidable organisation damage becomes a block-card tax | Tune encounter damage and recovery around meaningful protection/offence tradeoffs |
| Hidden damage feels arbitrary | Clear broad intentions, useful intel, danger warnings, legible repeatable routines |
| Automatic attacks feel outside player control | Accurate previews, explicit movement commitment, documented target resolution |
| Retained hand plus overflow creates congestion | Show discard clearly and test deck/hand behaviour before expanding card counts |
| Troop cards become stranded during combat | Visible in-play/unavailable zones and explicit recovery rules |
| Shared assets accidentally retain mutable HP or upgrades | Separate immutable definitions from runtime instances |
| Two factions and six acts overwhelm a solo scope | One resistance area first; reuse proven systems before adding content |
| Presentation obscures grid targeting | Keep rules on 2D coordinates and effects behind readable overlays |

## 10. First implementation task

The original next step was an M1-only board sandbox. The accepted implementation request superseded that limit with one complete short resistance run (M1-M4 and an initial M5 presentation pass). Do not expand this prototype into both leaders or the six-act campaign without a new scope decision. The next useful work is human playtesting of combo clarity, hand retention, block tradeoffs and difficulty.
