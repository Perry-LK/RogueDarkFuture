namespace FutureDark.Rules;

public sealed record RelicDef(string Id, string Name, string Description, int Price = 100);
public sealed record SupplyDef(string Id, string Name, string Description, TargetKind Target, int Price = 25);

public static partial class Content
{
    private static CardDef[] ExpansionCards() =>
    [
        new("sapper", "Sapper", CardKind.Troop, 1, TargetKind.Deployment, EffectKind.Deploy,
            "Deploy 10 HP / 3 ATK. Move one diagonal cell. Fire two cells in each orthogonal direction.", "Trap and displace enemies into your crossfire.", PieceKind.Sapper),
        new("medic", "Field Medic", CardKind.Troop, 1, TargetKind.Deployment, EffectKind.Deploy,
            "Deploy 10 HP / 2 ATK. Move one cell in any direction. Heal adjacent troops 1 HP at round start.", "Sustain a small, specialised squad.", PieceKind.Medic),
        new("sentinel", "Sentinel", CardKind.Troop, 2, TargetKind.Deployment, EffectKind.Deploy,
            "Deploy 17 HP / 2 ATK. Move one orthogonal cell; attack all neighbours. Adjacent allies gain 2 block at round start.", "Hold a formation. Actions do the heavy lifting.", PieceKind.Sentinel),
        new("pulse", "Signal Spike", CardKind.Action, 1, TargetKind.Enemy, EffectKind.Pulse,
            "Deal {p} damage. Can also target destructible cover.", "BREACH / Marks amplify direct hits", Power: 6),
        new("mark", "Target Beacon", CardKind.Action, 0, TargetKind.Enemy, EffectKind.Mark,
            "Apply {p} marks. Each mark adds +3 to a direct hit and is consumed by it.", "Prepare a precision breach.", Power: 2, UpgradePower: 1),
        new("jam", "Signal Jam", CardKind.Action, 1, TargetKind.Enemy, EffectKind.Jam,
            "Jam this enemy for {p} enemy phases: no movement, special action or attack.", "Disrupt a squad's setup before it connects.", Power: 1, UpgradePower: 1, Exhaust: true),
        new("corrode", "Corrosive Payload", CardKind.Action, 1, TargetKind.Enemy, EffectKind.Corrode,
            "Apply {p} corrosion. At enemy phase start it deals its stack count through guard, then loses one stack.", "MELTDOWN / Corrosion + Detonate", Power: 3),
        new("detonate", "Detonate Circuit", CardKind.Action, 1, TargetKind.Enemy, EffectKind.Detonate,
            "Deal {p} damage plus twice the enemy's corrosion. Consume all corrosion.", "MELTDOWN / Convert a lasting status into burst", Power: 4),
        new("push", "Repulsor Burst", CardKind.Action, 1, TargetKind.Enemy, EffectKind.Push,
            "Deal {p} damage. Push the enemy one cell toward row 1 if legal.", "DEAD END / Push a target onto a trap", Power: 4),
        new("pull", "Magnetic Hook", CardKind.Action, 1, TargetKind.Enemy, EffectKind.Pull,
            "Deal {p} damage. Pull the enemy one cell toward the deployment edge if legal.", "Break enemy formation and feed your pieces.", Power: 4),
        new("volley", "Crossfire Order", CardKind.Action, 1, TargetKind.Troop, EffectKind.Volley,
            "The selected troop immediately attacks its pattern with +{p} damage. It still attacks during orders.", "A small squad can strike several times.", Power: 2),
        new("execute", "Finish Protocol", CardKind.Action, 2, TargetKind.Enemy, EffectKind.Execute,
            "Deal {p} damage; double it if the target has 10 HP or less.", "EXECUTION / Exploit a weakened target", Power: 10, UpgradePower: 3),
        new("arc", "Arc Cascade", CardKind.Action, 2, TargetKind.Enemy, EffectKind.Arc,
            "Deal {p} damage to the target and enemies within 1 Manhattan cell of it.", "Punish the enemy for maintaining formation.", Power: 6),
        new("draw", "Intercept Traffic", CardKind.Action, 0, TargetKind.None, EffectKind.Draw,
            "Draw {p} cards. Overflow still goes to discard.", "Find the missing connection.", Power: 2, UpgradePower: 1, Exhaust: true),
        new("battery", "Reserve Cell", CardKind.Action, 0, TargetKind.None, EffectKind.Battery,
            "Gain {p} AP this round.", "Power a longer action sequence.", Power: 1, UpgradePower: 1, Exhaust: true),
        new("rally", "Rally Formation", CardKind.Action, 1, TargetKind.None, EffectKind.Rally,
            "Troops with an adjacent ally gain +{p} attack this round.", "UNITED FRONT / Formation + attack", Power: 3, UpgradePower: 1),
        new("repair", "Field Repairs", CardKind.Action, 1, TargetKind.Troop, EffectKind.Repair,
            "Restore {p} HP to a troop, up to its maximum.", "Keep the anchor of your action deck alive.", Power: 6),
        new("fortify", "Shelter Network", CardKind.Block, 2, TargetKind.None, EffectKind.Fortify,
            "Gain {p} organisation block and give every troop 3 block.", "Cover the entire organisation without conflating its pools.", Power: 12, UpgradePower: 4),
        new("smoke", "Smoke Screen", CardKind.Block, 1, TargetKind.None, EffectKind.Smoke,
            "Gain {p} organisation block. Remove all marks from friendly troops.", "Break a spotter's precision setup.", Power: 5),
        new("trap", "Shock Snare", CardKind.Board, 1, TargetKind.EmptyCell, EffectKind.Trap,
            "Place a trap for 3 enemy phases. An enemy entering it takes {p} damage and is rooted for that phase.", "DEAD END / Displacement + trap", Power: 7),
        new("mine", "Induction Mine", CardKind.Board, 1, TargetKind.EmptyCell, EffectKind.Mine,
            "Place a mine for 3 enemy phases. An enemy entering it takes {p} damage; adjacent enemies take half.", "DEAD END / Intercept a committed move", Power: 9, UpgradePower: 3),
        new("acid", "Acid Spill", CardKind.Board, 1, TargetKind.Cell, EffectKind.Acid,
            "For 3 enemy phases, enemies entering this tile gain {p} corrosion. Its current enemy occupant gains it now.", "Seed corrosion, then detonate.", Power: 3, UpgradePower: 1),
        new("advance", "Advance Order", CardKind.Action, 1, TargetKind.None, EffectKind.Advance,
            "All troops gain {p} extra moves this round. Counts as Forced March for Runners.", "MOMENTUM / Squad-wide repositioning", Power: 1, UpgradePower: 1),
        new("overwatch", "Entrenched Fire", CardKind.Action, 1, TargetKind.Troop, EffectKind.Overwatch,
            "This troop gains +{p} attack this round if it makes no moves.", "HOLD FAST / Defend the firing line", Power: 4),
        new("recover-supply", "Scavenge Casing", CardKind.Action, 1, TargetKind.None, EffectKind.RecoverSupply,
            "Return the last supply used this encounter to an empty slot. Gain {p} organisation block.", "Resupply without an infinite recovery loop.", Power: 3, Exhaust: true),
        new("amplify", "Hotwired Injector", CardKind.Action, 0, TargetKind.None, EffectKind.Amplify,
            "Amplify the next supply by {p} potency. Damage, block, healing and corrosion gain the full bonus; AP half, jam duration one quarter (rounded down).",
            "HOTWIRED / Amplify then use a supply", Power: 4, Exhaust: true),
        new("supply-use", "Rapid Logistics", CardKind.Action, 0, TargetKind.None, EffectKind.SupplyUse,
            "Gain {p} additional supply uses this round.", "Deploy a stored answer at the right time.", Power: 1, UpgradePower: 1, Exhaust: true),
        new("purge", "Purge Defences", CardKind.Action, 1, TargetKind.Enemy, EffectKind.Purge,
            "Remove all guard from an enemy. Deal {p} damage.", "Expose protected targets before your main hit.", Power: 3),
        new("siege", "Breach Charge", CardKind.Action, 2, TargetKind.Enemy, EffectKind.Siege,
            "Deal {p} damage. Deal 5 more if a friendly troop is within 2 Manhattan cells.", "FIRE SUPPORT / Troop proximity + action", Power: 9, UpgradePower: 3)
    ];

    public static readonly RelicDef[] Relics =
    [
        new("capacitor", "Cracked Capacitor", "Gain +1 AP on the first round of every fight."),
        new("manual", "Field Manual", "Draw one extra card at the start of every fight."),
        new("plating", "Salvaged Plating", "Gain 2 organisation block at the start of each round."),
        new("longwire", "Longwave Antenna", "Ghost Signal adds one additional damage."),
        new("catalyst", "Chemical Catalyst", "Your cards apply one additional corrosion."),
        new("bandolier", "Utility Bandolier", "Use two supplies per round instead of one."),
        new("triage", "Triage Protocol", "Recover 3 organisation HP after each victory."),
        new("bounty", "Encrypted Ledger", "Earn 8 extra funds after each battle."),
        new("command", "Broken Officer's Seal", "All your troops deal +1 attack damage.", 120),
        new("rigger", "Rigger's Tools", "Your traps and mines deal +2 damage."),
        new("prism", "Targeting Prism", "Consuming a mark adds +5 damage instead of +3."),
        new("reserve", "Emergency Reserve", "Start each encounter with 5 organisation block.")
    ];
    public static readonly SupplyDef[] Supplies =
    [
        new("shield", "Portable Shelter", "Gain 10 organisation block.", TargetKind.None),
        new("stim", "Stim Cell", "Gain 1 AP this round.", TargetKind.None, 35),
        new("emp", "EMP Capsule", "Jam one enemy for one enemy phase.", TargetKind.Enemy, 35),
        new("charge", "Breach Grenade", "Deal 8 damage to one enemy.", TargetKind.Enemy),
        new("medkit", "Field Dressing", "Restore 8 HP to one troop.", TargetKind.Troop),
        new("acid-vial", "Corrosive Vial", "Apply 4 corrosion to an enemy.", TargetKind.Enemy)
    ];
    public static RelicDef Relic(string id) => Relics.Single(r => r.Id == id);
    public static SupplyDef Supply(string id) => Supplies.Single(s => s.Id == id);

    public static readonly EnemyDef Spotter = new("spotter", "Signal Spotter", 13,
    [
        new(EnemyActionKind.Mark, 1, AttackPattern.Cross, "Paint target"),
        new(EnemyActionKind.Attack, 3, AttackPattern.Cross, "Tagged burst"),
        new(EnemyActionKind.Rest, 0, AttackPattern.Cross, "Retune")
    ], EnemyRole.Spotter, EnemyMove.Step);
    public static readonly EnemyDef Sniper = new("sniper", "Rail Sniper", 17,
    [
        new(EnemyActionKind.Rest, 0, AttackPattern.Column, "Acquire"),
        new(EnemyActionKind.Attack, 5, AttackPattern.Column, "Precision shot"),
        new(EnemyActionKind.Reinforce, 3, AttackPattern.Column, "Reload")
    ], EnemyRole.Sniper, EnemyMove.Step);
    public static readonly EnemyDef Commander = new("commander", "Command Officer", 24,
    [
        new(EnemyActionKind.Command, 2, AttackPattern.Adjacent, "Synchronise"),
        new(EnemyActionKind.Attack, 4, AttackPattern.Cross, "Execute order"),
        new(EnemyActionKind.Reinforce, 4, AttackPattern.Adjacent, "Regroup")
    ], EnemyRole.Commander, EnemyMove.Step);
    public static readonly EnemyDef Reinforcer = new("reinforcer", "Relay Carrier", 22,
    [
        new(EnemyActionKind.Reinforce, 4, AttackPattern.Cross, "Open channel"),
        new(EnemyActionKind.Rest, 0, AttackPattern.Cross, "Transmit"),
        new(EnemyActionKind.Summon, 1, AttackPattern.Cross, "Deploy drone"),
        new(EnemyActionKind.Attack, 3, AttackPattern.Cross, "Covering fire")
    ], EnemyRole.Reinforcer, EnemyMove.Diagonal);
    public static readonly EnemyDef Pusher = new("pusher", "Riot Driver", 21,
    [
        new(EnemyActionKind.Push, 4, AttackPattern.Adjacent, "Drive back"),
        new(EnemyActionKind.Attack, 5, AttackPattern.Adjacent, "Shock baton"),
        new(EnemyActionKind.Rest, 0, AttackPattern.Adjacent, "Recharge")
    ], EnemyRole.Pusher, EnemyMove.Knight);
    public static readonly EnemyDef Drone = new("drone", "Response Drone", 7,
    [
        new(EnemyActionKind.Rest, 0, AttackPattern.Adjacent, "Initialise"),
        new(EnemyActionKind.Attack, 2, AttackPattern.Adjacent, "Pulse")
    ], Movement: EnemyMove.Step);
    public static readonly EnemyDef Architect = new("architect", "The Architect", 65,
    [
        new(EnemyActionKind.Command, 2, AttackPattern.Cross, "Network orders"),
        new(EnemyActionKind.Attack, 7, AttackPattern.Column, "Erase signal"),
        new(EnemyActionKind.Hazard, 4, AttackPattern.Cross, "Live streets"),
        new(EnemyActionKind.Summon, 1, AttackPattern.Cross, "Reserve detail"),
        new(EnemyActionKind.Attack, 8, AttackPattern.Cross, "Final directive"),
        new(EnemyActionKind.Rest, 0, AttackPattern.Cross, "Rebuild")
    ], EnemyRole.Commander, EnemyMove.Step);
}
