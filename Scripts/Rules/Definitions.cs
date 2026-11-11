namespace FutureDark.Rules;

public readonly record struct Cell(int X, int Y)
{
    public int Distance(Cell other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
    public override string ToString() => $"{(char)('A' + X)}{Y + 1}";
}

public enum CardKind { Troop, Action, Block, Board }
public enum TargetKind { None, Deployment, Troop, EmptyCell, Cell, Enemy }
public enum PieceKind { Runner, Marksman, Breacher, Sapper, Medic, Sentinel }
public enum EffectKind
{
    Deploy, Network, Armour, Intercept, Recon, March, Strike, Barricade, Exposed,
    Pulse, Mark, Jam, Corrode, Detonate, Push, Pull, Volley, Execute, Arc,
    Draw, Battery, Rally, Repair, Fortify, Smoke, Trap, Mine, Acid,
    Advance, Overwatch, RecoverSupply, Amplify, SupplyUse, Purge, Siege
}
public enum Zone { Draw, Hand, Discard, InPlay, Unavailable, Exhausted }
public enum Phase { Cards, Movement, Discard, Victory, Defeat }
public enum EnemyActionKind { Attack, Reinforce, Lockdown, Rest, Mark, Command, Summon, Hazard, Push }
public enum AttackPattern { Cross, Adjacent, Column, Row }
public enum TileKind { Barricade, Exposed, Lockdown, Trap, Mine, Acid, EnemyHazard }
public enum EnemyRole { Standard, Spotter, Sniper, Shield, Controller, Commander, Reinforcer, Pusher }
public enum EnemyMove { Hold, Step, Knight, Diagonal }

public sealed record CardDef(
    string Id, string Name, CardKind Kind, int Cost, TargetKind Target,
    EffectKind Effect, string Description, string Synergy, PieceKind? Piece = null,
    int Power = 0, int UpgradePower = 2, bool Exhaust = false);

public sealed record Card(int Id, CardDef Definition, bool Upgraded = false)
{
    public string Name => Definition.Name + (Upgraded ? " +" : "");
}

public sealed record PieceDef(string Name, int Hp, int Damage, string MoveText, string AttackText);
public sealed record EnemyStep(EnemyActionKind Kind, int Power, AttackPattern Pattern, string Name);
public sealed record EnemyDef(string Id, string Name, int Hp, EnemyStep[] Routine,
    EnemyRole Role = EnemyRole.Standard, EnemyMove Movement = EnemyMove.Hold);
public sealed record Spawn(EnemyDef Definition, Cell Cell);

public sealed class Board
{
    public HashSet<Cell> Cells { get; } = new();
    public HashSet<Cell> Terrain { get; } = new();
    public HashSet<Cell> Deployment { get; } = new();
    public int Width => 7;
    public int Height => 6;

    public Board(int variant = 0)
    {
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            var cell = new Cell(x, y);
            if ((x == 0 || x == 6) && (y == 0 || y == 5)) continue;
            if (variant == 1 && cell == new Cell(3, 2)) continue;
            Cells.Add(cell);
            if (y >= 4) Deployment.Add(cell);
        }
        Terrain.Add(new Cell(1, 2));
        Terrain.Add(new Cell(5, 2));
        if (variant == 2) Terrain.Add(new Cell(3, 3));
    }
}

public static partial class Content
{
    public static readonly IReadOnlyDictionary<PieceKind, PieceDef> Pieces = new Dictionary<PieceKind, PieceDef>
    {
        [PieceKind.Runner] = new("Runner", 11, 3, "1 orthogonal cell", "Adjacent orthogonal cells"),
        [PieceKind.Marksman] = new("Marksman", 8, 4, "1 orthogonal cell", "4 cells forward (toward row 1)"),
        [PieceKind.Breacher] = new("Breacher", 14, 4, "Knight leap (2 + 1)", "All 8 adjacent cells"),
        [PieceKind.Sapper] = new("Sapper", 10, 3, "1 diagonal cell", "2 cells in each orthogonal direction"),
        [PieceKind.Medic] = new("Field Medic", 10, 2, "1 cell in any direction", "Orthogonal neighbours; heals adjacent troops 1 each round"),
        [PieceKind.Sentinel] = new("Sentinel", 17, 2, "1 orthogonal cell", "All 8 neighbours; gives adjacent troops 2 block each round")
    };

    private static readonly CardDef[] CoreCards =
    [
        new("runner", "Runner", CardKind.Troop, 1, TargetKind.Deployment, EffectKind.Deploy,
            "Deploy 11 HP / 3 ATK.\nMove 1 orthogonal cell. Attack orthogonal neighbours.",
            "MOMENTUM: Forced March + 2 moves gives +3 damage.", PieceKind.Runner),
        new("marksman", "Marksman", CardKind.Troop, 2, TargetKind.Deployment, EffectKind.Deploy,
            "Deploy 8 HP / 4 ATK.\nMove 1 orthogonal cell. Fire 4 cells toward row 1.",
            "GHOST SIGNAL: Recon Sweep gives +3 damage.", PieceKind.Marksman),
        new("breacher", "Breacher", CardKind.Troop, 2, TargetKind.Deployment, EffectKind.Deploy,
            "Deploy 14 HP / 4 ATK.\nLeap like a knight. Attack all 8 neighbouring cells.",
            "KILL CIRCUIT: Strike into exposed ground for +3 more.", PieceKind.Breacher),
        new("network", "Emergency Network", CardKind.Block, 1, TargetKind.None, EffectKind.Network,
            "Give the organisation 7 block.\nDoes not protect troops.", "Silence the attack. Preserve the movement."),
        new("armour", "Field Armour", CardKind.Block, 1, TargetKind.Troop, EffectKind.Armour,
            "Give one troop 7 block.\nDoes not protect the organisation.", "A piece alive is a future combination."),
        new("intercept", "Intercept Package", CardKind.Block, 1, TargetKind.Troop, EffectKind.Intercept,
            "Give the organisation and one troop 4 block each.", "Two independent layers of protection."),
        new("recon", "Recon Sweep", CardKind.Action, 0, TargetKind.None, EffectKind.Recon,
            "Reveal all enemy intentions this round.\nMarksmen deal +3 damage while this intel lasts.",
            "GHOST SIGNAL / Recon + Marksman"),
        new("march", "Forced March", CardKind.Action, 1, TargetKind.Troop, EffectKind.March,
            "Give one troop +1 move this round.\nA Runner that moves twice deals +3 damage.",
            "MOMENTUM / March + Runner + 2 moves"),
        new("strike", "Coordinated Strike", CardKind.Action, 1, TargetKind.None, EffectKind.Strike,
            "All troops gain +2 damage this round.\nHits on exposed enemies gain +3 more.",
            "KILL CIRCUIT / Strike + Exposed Ground"),
        new("barricade", "Barricade", CardKind.Board, 1, TargetKind.EmptyCell, EffectKind.Barricade,
            "Deny movement on an empty tile for 2 enemy phases.\nNever blocks organisation damage.",
            "Control movement, not fire or vision."),
        new("exposed", "Exposed Ground", CardKind.Board, 1, TargetKind.Cell, EffectKind.Exposed,
            "Mark a tile for 2 enemy phases.\nTroop attacks against enemies there deal +2 damage.",
            "KILL CIRCUIT / Exposed Ground + Strike")
    ];

    public static readonly CardDef[] Cards = CoreCards.Concat(ExpansionCards()).ToArray();
    public static CardDef Card(string id) => Cards.Single(card => card.Id == id);

    public static string RulesText(Card card)
    {
        if (card.Definition.Power > 0)
            return card.Definition.Description.Replace("{p}", (card.Definition.Power + (card.Upgraded ? card.Definition.UpgradePower : 0)).ToString())
                + (card.Definition.Exhaust ? "\nExhaust for this encounter." : "");
        if (!card.Upgraded) return card.Definition.Description;
        return card.Definition.Effect switch
        {
            EffectKind.Deploy => card.Definition.Description + "\nUPGRADE: +2 HP, +1 ATK.",
            EffectKind.Network or EffectKind.Armour => card.Definition.Description.Replace("7 block", "10 block"),
            EffectKind.Intercept => card.Definition.Description.Replace("4 block", "7 block"),
            EffectKind.Recon => card.Definition.Description.Replace("this round", "this and next round"),
            EffectKind.March => card.Definition.Description.Replace("+1 move", "+2 moves"),
            EffectKind.Strike => card.Definition.Description.Replace("+2 damage", "+3 damage"),
            EffectKind.Barricade => card.Definition.Description.Replace("2 enemy phases", "3 enemy phases"),
            EffectKind.Exposed => card.Definition.Description.Replace("+2 damage", "+3 damage"),
            _ => throw new ArgumentOutOfRangeException(nameof(card))
        };
    }

    public static readonly EnemyDef Patrol = new("patrol", "Patrol", 14,
    [
        new(EnemyActionKind.Attack, 4, AttackPattern.Cross, "Suppression"),
        new(EnemyActionKind.Reinforce, 3, AttackPattern.Cross, "Shield protocol"),
        new(EnemyActionKind.Attack, 4, AttackPattern.Cross, "Suppression"),
        new(EnemyActionKind.Rest, 0, AttackPattern.Cross, "Reload")
    ], Movement: EnemyMove.Step);
    public static readonly EnemyDef Enforcer = new("enforcer", "Enforcer", 17,
    [
        new(EnemyActionKind.Reinforce, 4, AttackPattern.Adjacent, "Brace"),
        new(EnemyActionKind.Attack, 6, AttackPattern.Adjacent, "Breach order"),
        new(EnemyActionKind.Rest, 0, AttackPattern.Adjacent, "Vent")
    ], EnemyRole.Shield, EnemyMove.Step);
    public static readonly EnemyDef Controller = new("controller", "Controller", 13,
    [
        new(EnemyActionKind.Hazard, 3, AttackPattern.Column, "Live grid"),
        new(EnemyActionKind.Attack, 4, AttackPattern.Column, "Signal purge"),
        new(EnemyActionKind.Reinforce, 3, AttackPattern.Column, "Recalibrate")
    ], EnemyRole.Controller, EnemyMove.Diagonal);
    public static readonly EnemyDef Warden = new("warden", "The Warden", 48,
    [
        new(EnemyActionKind.Command, 2, AttackPattern.Row, "Command link"),
        new(EnemyActionKind.Attack, 8, AttackPattern.Column, "Compliance order"),
        new(EnemyActionKind.Lockdown, 0, AttackPattern.Cross, "District lockdown"),
        new(EnemyActionKind.Attack, 9, AttackPattern.Cross, "Final notice"),
        new(EnemyActionKind.Rest, 0, AttackPattern.Cross, "System recovery")
    ], EnemyRole.Commander, EnemyMove.Step);

    public static List<Card> StartingDeck()
    {
        string[] ids = ["runner", "marksman", "breacher", "sentinel",
            "network", "network", "armour", "intercept",
            "recon", "march", "strike", "exposed", "pulse", "mark", "corrode", "detonate"];
        return ids.Select((id, index) => new Card(index + 1, Card(id))).ToList();
    }
}
