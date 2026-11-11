# FutureDark: Game Design Document

## Expanded prototype / accepted direction

The current build supersedes the original short-slice scope:

- **Board-first UI:** compact phase/HP/funds bar, small enemy-intent badges, one hover/selection inspector, straight card row. Dossier cards use olive (troop), rust (action), steel blue (block), and faded purple (board), with separate category symbols. Card dealing, hover lift, play/discard travel and piece movement animate; reduced motion removes movement.
- **Two acts plus central command:** Outer Blocks and Glass Ward each generate a seeded nine-room branching route on entry. Icons open encounter dialogs. Paths commit forward; branches are left behind. Events, fights and red zones vary by seed; a vendor and act boss are guaranteed.
- **Between-act city overview:** jagged district borders, rough streets, rundown outer architecture and denser high-rises. Completed districts fill with faded amber. The centre unlocks after `ceil(main act count * 2 / 3)` completions, requiring both current acts.
- **Enemy squads, not enemy decks:** movement and fixed routines are authored per type. Plans lock at round start. Occupied/denied destinations cause a hold, not surprise retargeting. Actual troop attacks are restricted to the overlap of the committed area and the pattern from the actual final position; organisation damage is never removed by a blocked move.
- **Enemy synergies:** marked troops enable a surviving, unjammed spotter's sniper bonus; adjacent shield units provide breakable formation armour; commanders empower nearby allies; controllers lay hazards that riot units push troops into; relay carriers summon drones into committed landing tiles. Summons wait until the next round and squads cap at six. Jam disables movement, action and support for the entire enemy phase.
- **Action-heavy builds:** forty distinct cards and six troop types support precision, corrosion/detonation, displacement/traps, formations and supply manipulation. A small troop core can support an action-led deck. No special starter-deck selection was added.
- **Economy:** one wallet across the run, reset on new runs. Initial HP is 64 and funds 65. Vendors sell cards, supplies and relics, buy back only supplies at half price, and offer paid card removal (45 funds initially, +15 per use, minimum eight cards).
- **Supplies:** three slots, one item each. One free use per round in card play. Cards and relics can increase uses or improve effects. Recovery and AP/draw engines exhaust where necessary to avoid self-sustaining loops.
- **Relics:** twelve passive upgrades, active for the entire run, reset on a new run. Elite/boss victories grant an unowned relic when one remains; vendors and black-market events can also grant them.
- **Manual saves:** one slot per unique run, unlimited distinct runs. Saving during a room stores its entrance snapshot; reloading restores all resources and deterministic content together. Saving at a route/city boundary stores that boundary. No autosave. Defeat deletes that run's saved slot.

Normal victories recover three HP and grant twenty funds; elites grant forty-two funds; bosses recover eighteen HP and grant sixty-five funds, before relic modifiers. Healing caps at maximum. Events offer explicit costs and resolve once. Rewards may be skipped.

### Additional combo contracts

- **Breach:** a direct hit consumes one mark for +3 damage (+5 with Targeting Prism). Troop previews include the mark bonus exactly once.
- **Meltdown:** Detonate consumes all corrosion and adds twice its stack count. Corrosion otherwise ticks before enemy movement, ignores guard, and decays by one.
- **Dead End:** entering a friendly trap or mine triggers and consumes it once; displacement can force this. Traps root for the pending enemy phase.
- **Fire Support:** Breach Charge adds five damage when a friendly troop is within two Manhattan cells.
- **United Front / Hold Fast:** Rally grants bonuses to currently adjacent allies; Entrenched Fire requires no movement that round.
- **Hotwired:** an amplified damaging supply produces combo feedback on actual HP loss. Non-damaging supply setup remains quiet.

All successful enemy combinations also produce feedback. Normal hits, empty setup, fully absorbed hits and failed moves remain visually restrained. Difficulty needs continued human playtesting; automated tactical policies are regression checks, not a promise of a target human win rate.

## Initial prototype decisions (historical)

The repository now contains the **short playable resistance run**, rather than only the M1 board sandbox. This scope and the following defaults were accepted for the first implementation; they supersede conflicting "Open" or "Proposed" labels below for this prototype only.

- Godot 4.7.2 .NET, C#, desktop. Layered vector silhouettes on a dimetric 2D grid implement the 2.5D presentation without 3D models.
- Brutalist tactical-terminal aesthetic: charcoal, concrete, worn ivory. Electric cyan and hot amber are reserved for successful combo feedback; ordinary turns remain desaturated. A muted red incoming-threat warning is the accessibility exception.
- Controlled combo trails, brief impact rings, a small board-only impulse and synthesized audio. Reduced motion replaces motion effects with a static target outline; audio and threat haze have separate controls.
- Three AP; five draw attempts from an initially empty hand; nine-card cap; one basic move per troop; all covered enemies hit in deployment order. Close card play before movement. Choose `min(2, hand size)` discards after automatic attacks, before enemy actions. The attack control explicitly holds every unused move.
- Terrain and temporary denied tiles affect destinations only, never attacks, organisation damage, or vision. Breachers can jump intermediate cells; all movement rejects occupied, missing or denied destinations.
- Organisation and troop block are consumed independently and expire after enemy actions. Enemy guard stacks and remains until consumed. Enemies resolve in spawn order; organisation defeat stops resolution immediately.
- Manhattan-distance-two proximity reveals the next intent. Recon reveals all enemies for the current round (two rounds when upgraded). Unobserved intent never exposes exact damage or target tiles. The warning uses raw incoming organisation attack damage at or above 25% of current HP, independent of block.
- Starting organisation HP: 56. The relay offers either 14 healing (capped at maximum) or one permanent card upgrade. Normal fights offer one of three random cards or a skip. Boss victory applies eight recovery once and ends the prototype.
- No mid-run save or permanent unlocks; accessibility settings alone persist.

### Combo contract

1. **Ghost Signal:** A Marksman attacking while Recon Sweep's intel effect is active gains +3 damage. Proximity intel alone does not enable this.
2. **Momentum:** A Runner affected by Forced March gains +3 damage after making at least two actual moves this round. Holding is not movement.
3. **Kill Circuit:** A troop with Coordinated Strike attacking an enemy on Exposed Ground gains +3 additional damage, on top of Strike (+2) and exposure (+2).

Different combos stack. Multiple copies of Recon refresh duration, rather than multiplying Ghost Signal. Strike bonuses stack, but Kill Circuit adds its +3 once per hit; exposure on a tile is replaced rather than multiplied. Combo audiovisual events require **positive enemy HP loss** after guard. An armed combo or fully guarded hit produces no spectacle.

### Authored starting content

The sixteen-card starting deck contains two Runners, two Marksmen, one Breacher, three Emergency Networks, one Field Armour, one Intercept Package, two Recon Sweeps, one Forced March, one Coordinated Strike, one Barricade and one Exposed Ground.

| Piece | Cost | HP / attack | Move | Attack |
|---|---|---|---|---|
| Runner | 1 | 11 / 3 | One orthogonal cell | Orthogonal neighbours |
| Marksman | 2 | 8 / 4 | One orthogonal cell | Up to four cells toward row 1 in its column |
| Breacher | 2 | 14 / 4 | Knight leap | All eight adjacent cells |

Recon costs zero; other non-troop cards cost one. Network and Armour grant seven block to their respective recipients; Intercept grants four to each. Barricade and Exposed Ground last two enemy phases. Upgraded troops gain two HP and one attack; block cards gain three block; Recon/Barricade gain one phase of duration; March gains another move; Strike/Exposed gain one damage.

Route: **Checkpoint 09** -> **Dead Frequency** (event) or **The Foundry** (fight) -> **Underpass** -> **The Warden**. See the [README](../README.md) for controls and validation commands. The remainder of this document retains the wider design and deferred decisions.

## 1. Vision

**Confirmed:** A dark-future dystopian tactical deckbuilding roguelike. The player controls one faction leader per run, travelling through a district represented by branching encounter maps. Battles combine card play, chess-inspired troop movement, automatic patterned attacks, and persistent organisation health.

### Design pillars

1. **Protect the movement, or protect its people.** Organisation block and troop block are separate investments.
2. **Commit at the right moment.** Deploy and position troops to exploit enemy routines without leaving them exposed.
3. **Information is a resource.** Broad enemy intentions are visible; proximity and intel cards reveal actionable detail.
4. **Build a tactical identity.** Troop, action, block, and board cards support different approaches to the same core rules.
5. **Make the board readable.** Movement, attack patterns, danger, deployment zones, and temporary effects need clear previews.

### Core player decisions

- Spend AP on deployment, damage, intel, board control, or protection?
- Block organisation damage, troop damage, or some of each?
- Move into attack range now, remain safe, or spend a card to create a better opening?
- Keep a useful card for later or discard it to manage the hand?
- Take a risky encounter for deck improvement or seek recovery through an event?

## 2. Presentation and platform

| Topic | Decision |
|---|---|
| Working title | **Confirmed:** FutureDark |
| Engine/language | **Confirmed:** Godot with C# |
| Presentation | **Confirmed:** 2D board with 2.5D characters |
| Initial platform | **Confirmed:** Desktop |
| Later platform | **Confirmed direction:** Mobile, substantially later |
| Development scale | **Confirmed:** Solo-developer prototype |
| Engine release | **Proposed:** Pin a stable Godot 4 .NET release at project creation |

**Proposed art direction:** Industrial architecture, surveillance infrastructure, harsh artificial light, worn equipment, and strong faction silhouettes. Resistance uses improvised equipment and repurposed technology; security uses standardised equipment and institutional visual language.

For the prototype, interpret 2.5D as layered character art with depth sorting on a 2D board. Actual 3D models are an **open presentation choice**, not a requirement for proving combat.

## 3. Playable factions

**Confirmed:** Each run uses one leader. Both factions challenge the same district and share the core gameplay rules.

| Faction | Confirmed identity | Proposed mechanical emphasis |
|---|---|---|
| Resistance | Rebels challenging the status quo | Mobility, reconnaissance, improvised board control |
| District security | Security chief trying to make a name for themselves | Formation play, reinforcement, controlled advances |

Each leader has faction-specific card options alongside a shared card pool. Differences should be comparable to distinct deckbuilding characters, not separate games.

**Prototype:** Resistance only. Security's cards, narrative, and presentation follow after the shared combat loop is validated.

**Open:** The security campaign's exact opponents, political relationships, and reasons for confronting each district boss. Reuse the district structure without assuming both factions have identical enemies or events.

## 4. Run structure

**Confirmed full-game target:** Five main acts, followed by a short sixth-act finale.

- A main act covers an area of the same district.
- Each area is a branching tree of encounter nodes.
- Nodes are fights or events.
- Each main act ends with a boss encounter.
- The sixth act is a short final-boss-style conclusion; its precise encounter count is open.
- Organisation HP persists across encounters and acts.
- Healing can occur at events and after an act boss before entering the next act.
- Organisation HP reaching zero ends the run.
- Defeat requires starting another run.
- Permanent unlocks are desirable later, but excluded from the initial scope.

**Proposed thematic progression, not approved locations:**

| Act | Area concept | Design purpose |
|---|---|---|
| 1 | Outer blocks | Teach deployment, block, and simple routines |
| 2 | Industrial belt | Introduce more board effects |
| 3 | Transit and surveillance grid | Test information and positioning |
| 4 | Administrative core | Combine established enemy behaviours |
| 5 | Command precinct | Test complete deck strategies |
| 6 | District stronghold | Short finale using established rules |

No separate shop, rest, or elite node category is required for the prototype. Recovery and deck modification can be delivered through events.

## 5. Combat board

### Geometry and deployment

**Confirmed:**

- Boards are grid-based but may have different dimensions and shapes.
- Battles may contain one or many enemies.
- Troops deploy only into the player's starting area.
- Troops have piece-specific movement and attack patterns inspired by chess.
- Cards can modify tiles, deny access, extend movement, or alter damage and other board states.

**Proposed:** Use an orthogonal square grid represented by a set of valid coordinates, rather than assuming every board is a filled rectangle. Mark deployment tiles explicitly.

Display valid moves, deployment cells, and attack patterns before commitment. A troop may stay still.

### Obstacles: unresolved detail

**Confirmed invariant:** Obstacles, distance, and troop positioning never prevent the organisation-damage component of an enemy attack.

**Open:** The earlier description of obstacles being "fully in the way" and diagonals not blocking needs a concrete rule for troop attacks, incoming troop damage, movement, and vision. Do not silently interpret these as the same system.

**Proposed prototype restriction:** Begin with inaccessible terrain that affects movement only. Add attack-blocking terrain after deciding straight-line and diagonal examples. Keep organisation damage unchanged in every case.

## 6. Combat round

**Confirmed phase order:** Draw cards -> play cards -> troop actions -> enemy turn.

**Proposed precise resolution:**

1. **Round start:** Reset AP to the configured per-turn amount.
2. **Draw:** Attempt five draws, resolving hand overflow immediately.
3. **Card phase:** Spend AP and play legal cards in any order.
4. **Troop phase:** Choose movement or hold for each troop, then resolve automatic attacks.
5. **Hand cleanup:** Choose two cards to discard; discard all remaining cards if fewer than two are held.
6. **Enemy phase:** Resolve each surviving enemy's current routine step.
7. **Round end:** Expire organisation block and applicable temporary effects; advance surviving enemy routines.

The placement of mandatory discard after troop actions and before enemy resolution is **proposed**, since only the broad phase order and end-of-turn discard were confirmed.

**Proposed:** Card play ends when the troop phase begins. Extended movement and other action-card effects can grant explicit allowances consumed during the troop phase.

### Troop control and automatic attacks

**Confirmed:**

- The player may choose not to move a troop.
- Troop movement follows that piece's movement type.
- Attacks are automatic according to the troop's attack pattern unless a card overrides it.
- The UI highlights targets covered by the pattern.

**Proposed defaults:**

- Each troop may make one basic movement per round; holding uses no AP.
- The player completes movement decisions before automatic attacks begin.
- Every living enemy in a troop's highlighted pattern is hit once by its basic attack.
- Resolve troop attacks in deployment order; dead targets are skipped.
- Recalculate highlights after moves and relevant card effects.

**Open:** Confirm whether every covered target should be hit or whether some troop patterns select only one target. Multi-target attacks and ordering are prototype defaults, not established requirements.

## 7. Organisation HP, troop HP, and block

### Damage contract

**Confirmed:** Every enemy attack deals its attack damage to organisation HP and the same base amount to every troop that attack reaches. The organisation is an off-board HP pool.

Each recipient has an independent block pool:

`HP loss for a recipient = max(0, incoming damage - that recipient's block)`

**Proposed:** Block absorbs and is consumed by incoming damage. Organisation-targeted block protects only the organisation; troop-targeted block protects only its selected troop unless a card explicitly says otherwise.

Example: An enemy attacks for 6. Organisation block is 4, and an affected troop has 2 block. Organisation loses 2 HP and the troop loses 4 HP. A troop outside the pattern takes no damage; the organisation still loses 2 HP.

An enemy with no troop targets still attacks the organisation. An obstacle never redirects or cancels that organisation attack. Killing the enemy before its turn prevents its action; a future explicit attack-cancellation effect would need equally explicit card rules.

### Persistence and defeat

- **Confirmed:** Only organisation HP persists between fights.
- **Confirmed:** Troops remain deployed during a fight until defeated.
- **Confirmed:** A defeated troop is unavailable for the remainder of that fight unless a special effect changes this.
- **Confirmed:** After combat, all troop cards return to the deck and the board is prepared afresh next fight.
- **Proposed:** Every troop begins a new fight at its normal full HP.
- **Confirmed:** Organisation block expires at the end of the turn.
- **Proposed timing:** Expire organisation block after enemy actions, not before.
- **Proposed:** Troop block also expires after the enemy phase.
- **Proposed:** On organisation HP reaching zero, defeat resolves immediately, even if a simultaneous effect would otherwise complete the fight.

## 8. Cards, AP, and deck lifecycle

### Card categories

| Category | Confirmed purpose |
|---|---|
| Troop | Place a persistent combat piece in the deployment area |
| Action | Perform tactical actions, including intel or modified movement |
| Block | Protect the organisation or troop recipients |
| Board | Change tiles and board conditions |

Cards can be upgraded and the deck can be modified.

### Hand and draw rules

**Confirmed:**

- Five cards are drawn by default at the beginning of a turn.
- Maximum hand size is nine.
- Excess drawn cards go to discard, not permanent removal.
- When the draw pile runs out, the discard pile is shuffled into a new draw pile.
- Played cards go to discard unless represented by a piece in play or an explicit card rule.
- Two cards must be discarded at turn end; if fewer than two remain, discard the remaining hand.
- Unplayed cards not selected for discard remain in hand, subject to the timing proposal above.

**Proposed AP default:** Three AP per round, no carryover. Tune costs and AP together rather than treating this as final balance.

### Card state rules

**Proposed implementation contract:**

- A card instance occupies exactly one encounter zone: draw, hand, discard, in play, or defeated/unavailable.
- Deploying a troop moves its card to the in-play zone; it cannot be drawn while deployed.
- Defeating that troop moves its card to the unavailable zone, not ordinary discard.
- An explicit recovery effect may return an unavailable troop according to its own rules.
- At combat end, rebuild from the run's deck, preserving card upgrades and permanent deck changes but clearing encounter-only state.
- Do not duplicate cards when rebuilding the deck.
- If both draw and discard are empty, further draws produce no card.
- Overflow is handled on each draw; an overflowed card may enter a later reshuffle under the normal draw rules.

**Open:** Starting hand, opening-turn draw, opening deployment opportunities, and whether the opening enemy routine step varies. Prototype recommendation: start with an empty hand and use the normal five-card first draw, avoiding a hidden extra opening draw.

## 9. Enemies and information

### Enemy routines

**Confirmed:** Each enemy follows a fixed repeating sequence. Available steps include:

- Attack.
- Reinforce, such as gaining guard/block.
- Affect tiles.
- Rest.

Different enemy types have their own routines.

**Proposed:** Only attack steps apply automatic organisation damage. Non-attack steps do not deal damage unless explicitly designed as attacks. Enemy block is a separate stat from organisation and troop block.

### Intel

**Confirmed:**

- Proximity/visibility from a troop or an intel card reveals enemy action detail.
- Without intel, show a rough intention, not exact damage or exact action detail.
- Revealed information is visible only while the visibility condition or intel-card effect applies.
- A large incoming-damage threat triggers a red haze or another warning.
- The warning threshold is prototype tuning.

**Proposed information tiers:**

| State | Visible information |
|---|---|
| Not observed | Enemy piece, broad next-action category, applicable danger warning |
| Observed | Exact upcoming action, damage, and affected troop tiles |
| Intel effect | Exact upcoming information for the targets and duration stated on the card |

Exact full-routine previews are not included by default. Losing sight hides detailed intentions again; player memory does not need to be prevented.

**Proposed vision:** A troop reveals enemies within two Manhattan-distance cells, without obstacle occlusion for the first prototype. This is a placeholder pending the obstacle/vision decision.

**Proposed warning:** Flag a round when total raw organisation attack damage due that enemy phase is at least 25% of current organisation HP. Make the threshold configurable and use both icon/text and optional haze. Do not expose hidden numeric damage through tooltips, logs visible to players, or an exact warning meter.

**Open:** Whether the warning should reflect raw damage or damage after current organisation block. Use raw damage for the first prototype, clearly label it as an incoming-threat warning, and test whether it is useful rather than noisy.

## 10. Suggested first-playable content

Everything in this section is **proposed** content, not a balance commitment.

### Initial card set

| Card | Category | Prototype role |
|---|---|---|
| Runner | Troop | Orthogonal one-cell movement; adjacent single-pattern pressure |
| Marksman | Troop | Limited movement; directional ranged pattern |
| Breacher | Troop | Distinct movement; close-range multi-cell pattern |
| Emergency Network | Block | Organisation-only protection |
| Field Armour | Block | Protection for one selected troop |
| Intercept Package | Block | Smaller protection split between organisation and one troop |
| Recon Sweep | Action | Temporary exact intel |
| Forced March | Action | Extra troop movement allowance |
| Coordinated Strike | Action | Explicit automatic-attack modification |
| Barricade | Board | Temporarily deny movement through a valid tile |
| Exposed Ground | Board | Increase damage to occupants under an explicit duration rule |

Use a small starting deck with duplicate cards drawn from this set. Choose exact counts, costs, and numbers through early tests; ensure opening hands can deploy and protect without making either guaranteed.

### Enemy set

- **Patrol:** Attack -> reinforce -> attack -> rest.
- **Enforcer:** Reinforce -> heavy attack -> rest.
- **Controller:** Tile effect -> attack -> reinforce.
- **Boss:** A longer, authored repeating routine that combines known mechanics.

The boss must not require undisclosed new rules to survive.

### Short area

Proposed map: entry fight -> choice of fight or event -> convergence fight -> boss -> recovery/area-complete screen.

Events should offer at least one healing choice and one deck-modification choice across the prototype's event set. Show costs and consequences before confirmation.

## 11. Prototype success criteria

- A player can complete a short resistance run from map entry to boss victory or organisation defeat.
- The player understands why an attack damages the organisation even with no nearby troop.
- At least some turns present a meaningful choice between protecting troops and organisation HP.
- Moving into vision creates useful information without becoming mandatory busywork.
- Chess-like movement and automatic attack previews are reliable.
- Organisation HP persists correctly; troop combat state does not.
- Irregular boards and at least one board-modifying card work without special-case combat rules.
- A player can explain an outcome using visible state and the rules rather than guessing at resolution order.

**Proposed playtest questions:** Was organisation damage avoidable through planning and block? Did troop survival matter? Were intel cards worth their cost? Did hidden damage create tension or feel arbitrary? Did retained hands create decisions or hand congestion?

## 12. Decisions to resolve before the affected milestone

| Decision | Needed before |
|---|---|
| Accept or adjust AP, movement, attack ordering, discard timing, and block defaults | Combat rules implementation |
| Attack patterns hit all covered targets or select targets | Troop attack implementation |
| Obstacle rules for movement, attacks, diagonals, and vision | Attack-blocking terrain |
| Enemy block consumption/expiry and reinforcement stacking | Reinforce implementation |
| Warning threshold, aggregation, and accessibility behaviour | Intel UI validation |
| Starting deck counts, stats, card costs, and healing amounts | Balance playtests |
| Post-fight card reward structure and event choices | Run-loop implementation |
| Exact meaning of 2.5D assets | Presentation polish |
| Final-act length and security campaign opposition | Full-game expansion |

## 13. Explicitly deferred

- Security as a playable faction.
- Five full areas and the sixth-act finale.
- Permanent unlock progression.
- Mobile builds and touch-specific UX.
- Multiplayer, real-time combat, and live-service systems.
- Large procedural content generation, extensive narrative branching, and production art.

Author the small prototype before introducing systems intended only to support hypothetical scale.
