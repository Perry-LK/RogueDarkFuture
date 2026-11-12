namespace FutureDark.Rules;

public sealed class Troop
{
    public Card Card { get; }
    public PieceKind Kind => Card.Definition.Piece ?? throw new InvalidOperationException("Troop has no piece.");
    public Cell Cell { get; internal set; }
    public int Hp { get; internal set; }
    public int MaxHp => Content.Pieces[Kind].Hp + (Card.Upgraded ? 2 : 0);
    public int Damage => Content.Pieces[Kind].Damage + (Card.Upgraded ? 1 : 0);
    public int Block { get; internal set; }
    public int MovesLeft { get; internal set; } = 1;
    public int MovesMade { get; internal set; }
    public bool Marched { get; internal set; }
    public int AttackBonus { get; internal set; }
    public int HoldBonus { get; internal set; }
    public int MarkTurns { get; internal set; }
    public int RootedRounds { get; internal set; }

    public Troop(Card card, Cell cell) { Card = card; Cell = cell; Hp = MaxHp; }
}

public sealed class Enemy
{
    public int Id { get; }
    public EnemyDef Definition { get; }
    public Cell Cell { get; internal set; }
    public int Hp { get; internal set; }
    public int Block { get; internal set; }
    public int RoutineIndex { get; internal set; }
    public int Mark { get; internal set; }
    public int Corrosion { get; internal set; }
    public int Jammed { get; internal set; }
    public int Rooted { get; internal set; }
    public int CommandTurns { get; internal set; }
    public EnemyPlan Plan { get; internal set; } = new(spawnCell: default, destination: default, target: default, pushTo: default);
    public EnemyStep Step => Definition.Routine[RoutineIndex];
    public Enemy(int id, Spawn spawn)
    { Id = id; Definition = spawn.Definition; Cell = spawn.Cell; Hp = Definition.Hp; }
}

public sealed class TileEffect(TileKind kind, Cell cell, int duration, int power = 0)
{
    public TileKind Kind { get; } = kind;
    public Cell Cell { get; } = cell;
    public int Duration { get; internal set; } = duration;
    public int Power { get; } = power;
}

public sealed record EnemyPlan(Cell spawnCell, Cell destination, Cell target, Cell pushTo);
public sealed record Impact(Cell From, Cell To, int Damage, string[] Combos, bool Hostile = false);
public sealed record Intent(bool Observed, string Label, string Detail, IReadOnlySet<Cell> Cells, Cell? MoveTo = null, Cell? Target = null, int? Power = null);
public readonly record struct CommandResult(bool Ok, string Message)
{
    public static CommandResult Success(string message = "") => new(true, message);
    public static CommandResult Reject(string message) => new(false, message);
}
public readonly record struct DamageResult(int Hp, int Block, int Lost);

public sealed partial class Combat
{
    private readonly Random _random;
    private readonly Dictionary<int, Card> _cards;
    private readonly Dictionary<int, Zone> _zones;
    private readonly List<int> _draw = new();
    private readonly List<int> _hand = new();
    private readonly List<int> _discard = new();
    private readonly List<Troop> _troops = new();
    private readonly List<Enemy> _enemies = new();
    private readonly List<TileEffect> _tiles = new();
    private readonly List<string> _log = new();
    private readonly List<Impact> _impacts = new();
    private readonly HashSet<string> _relics;
    private readonly List<string> _supplies;
    private string? _lastSupply;
    private int _supplyAmplify;
    public IReadOnlyList<string> Supplies => _supplies;
    public int SupplyUses { get; private set; }
    public int EnemyComboHits { get; private set; }

    public Board Board { get; }
    public Phase Phase { get; private set; }
    public int Round { get; private set; }
    public int Ap { get; private set; }
    public int OrganisationHp { get; private set; }
    public int OrganisationBlock { get; private set; }
    public int IntelRounds { get; private set; }
    public int StrikeBonus { get; private set; }
    public int ComboHits { get; private set; }
    public IReadOnlyList<int> DrawPile => _draw;
    public IReadOnlyList<int> Hand => _hand;
    public IReadOnlyList<int> DiscardPile => _discard;
    public IReadOnlyList<Troop> Troops => _troops;
    public IReadOnlyList<Enemy> Enemies => _enemies;
    public IReadOnlyList<TileEffect> Tiles => _tiles;
    public IReadOnlyList<string> Log => _log;
    public IReadOnlyList<Impact> Impacts => _impacts;
    public IReadOnlyDictionary<int, Zone> Zones => _zones;
    public int RequiredDiscards => Math.Min(2, _hand.Count);
    public int RawThreat => _enemies.Where(e => IsAttack(e.Step.Kind) && e.Jammed == 0).Sum(PlannedEnemyPower);
    public bool Danger => RawThreat > 0 && RawThreat * 4 >= OrganisationHp;

    public Combat(IEnumerable<Card> deck, int hp, Board board, IEnumerable<Spawn> spawns, int seed,
        IEnumerable<string>? relics = null, IEnumerable<string>? supplies = null)
    {
        if (hp <= 0) throw new ArgumentOutOfRangeException(nameof(hp));
        _random = new Random(seed);
        _relics = (relics ?? []).ToHashSet();
        _supplies = (supplies ?? []).ToList();
        if (_supplies.Count > 3 || _supplies.Any(id => !Content.Supplies.Any(s => s.Id == id)))
            throw new ArgumentException("Invalid supply inventory.", nameof(supplies));
        _cards = deck.ToDictionary(c => c.Id);
        if (_cards.Count == 0) throw new ArgumentException("A combat deck cannot be empty.", nameof(deck));
        _zones = _cards.Keys.ToDictionary(id => id, _ => Zone.Draw);
        Board = board;
        OrganisationHp = hp;
        _draw.AddRange(_cards.Keys);
        Shuffle(_draw);
        int enemyId = 1;
        foreach (var spawn in spawns)
        {
            if (!board.Accessible(spawn.Cell) ||
                _enemies.Any(e => e.Cell == spawn.Cell))
                throw new ArgumentException("Enemy spawn must be on an unoccupied accessible cell.", nameof(spawns));
            _enemies.Add(new Enemy(enemyId++, spawn));
        }
        if (_enemies.Count == 0) throw new ArgumentException("A fight requires an enemy.", nameof(spawns));
        StartRound();
    }

    public Card GetCard(int id) => _cards[id];

    public static DamageResult Damage(int hp, int block, int incoming)
    {
        if (hp < 0 || block < 0 || incoming < 0) throw new ArgumentOutOfRangeException(nameof(incoming));
        int absorbed = Math.Min(block, incoming);
        int lost = Math.Min(hp, incoming - absorbed);
        return new DamageResult(hp - lost, block - absorbed, lost);
    }

    private void Shuffle(List<int> pile)
    {
        for (int i = pile.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (pile[i], pile[j]) = (pile[j], pile[i]);
        }
    }

    private void StartRound()
    {
        Round++;
        Ap = 3 + (Round == 1 && HasRelic("capacitor") ? 1 : 0);
        SupplyUses = HasRelic("bandolier") ? 2 : 1;
        OrganisationBlock += (HasRelic("plating") ? 2 : 0) + (Round == 1 && HasRelic("reserve") ? 5 : 0);
        StrikeBonus = 0;
        foreach (var troop in _troops)
        {
            troop.MovesLeft = troop.RootedRounds > 0 ? 0 : 1;
            troop.RootedRounds = Math.Max(0, troop.RootedRounds - 1);
            troop.MovesMade = 0;
            troop.Marched = false;
            troop.AttackBonus = 0;
            troop.HoldBonus = 0;
            foreach (var support in _troops.Where(t => t != troop && t.Cell.Distance(troop.Cell) <= 1))
            {
                if (support.Kind == PieceKind.Medic) troop.Hp = Math.Min(troop.MaxHp, troop.Hp + 1);
                if (support.Kind == PieceKind.Sentinel) troop.Block += 2;
            }
        }
        DrawCards(5 + (Round == 1 && HasRelic("manual") ? 1 : 0));
        Phase = Phase.Cards;
        PlanEnemies();
        AddLog($"Round {Round:00} / {Ap} AP. Enemy plans committed.");
    }

    internal void DrawCards(int attempts)
    {
        for (int i = 0; i < attempts; i++)
        {
            if (_draw.Count == 0)
            {
                if (_discard.Count == 0) break;
                _draw.AddRange(_discard);
                foreach (int id in _draw) _zones[id] = Zone.Draw;
                _discard.Clear();
                Shuffle(_draw);
            }
            int cardId = _draw[^1];
            _draw.RemoveAt(_draw.Count - 1);
            if (_hand.Count < 9)
            {
                _hand.Add(cardId);
                _zones[cardId] = Zone.Hand;
            }
            else
            {
                _discard.Add(cardId);
                _zones[cardId] = Zone.Discard;
                AddLog($"Hand full / {GetCard(cardId).Name} overflowed to discard.");
            }
        }
    }

    public bool IsDenied(Cell cell) => Board.Terrain.Contains(cell) || Board.Walls.Contains(cell) || Board.Cover.ContainsKey(cell) ||
        _tiles.Any(t => t.Cell == cell && t.Kind is TileKind.Barricade or TileKind.Lockdown);
    public bool IsOccupied(Cell cell) => _troops.Any(t => t.Cell == cell) || _enemies.Any(e => e.Cell == cell);
    public bool CanEnter(Cell cell) => Board.Cells.Contains(cell) && !IsDenied(cell) && !IsOccupied(cell);

    public HashSet<Cell> CardTargets(int id)
    {
        var card = GetCard(id);
        if (card.Definition.Effect == EffectKind.Pulse)
            return _enemies.Select(e => e.Cell).Concat(Board.Cover.Keys).ToHashSet();
        if (card.Definition.Effect == EffectKind.Repair)
            return _troops.Where(t => t.Hp < t.MaxHp).Select(t => t.Cell).ToHashSet();
        return card.Definition.Target switch
        {
            TargetKind.Deployment => Board.Deployment.Where(CanEnter).ToHashSet(),
            TargetKind.Troop => _troops.Select(t => t.Cell).ToHashSet(),
            TargetKind.Enemy => _enemies.Select(e => e.Cell).ToHashSet(),
            TargetKind.EmptyCell => Board.Cells.Where(CanEnter).ToHashSet(),
            TargetKind.Cell => Board.Cells.Where(c => !Board.Terrain.Contains(c)).ToHashSet(),
            TargetKind.None => new(),
            _ => throw new ArgumentOutOfRangeException(nameof(id))
        };
    }

    public CommandResult Play(int id, Cell? target = null)
    {
        if (Phase != Phase.Cards) return CommandResult.Reject("Cards can only be played during the card phase.");
        if (!_hand.Contains(id)) return CommandResult.Reject("That card is not in your hand.");
        var card = GetCard(id);
        var def = card.Definition;
        if (Ap < def.Cost) return CommandResult.Reject($"Need {def.Cost} AP. Only {Ap} AP remains.");
        if (def.Effect == EffectKind.RecoverSupply && (_lastSupply is null || _supplies.Count >= 3))
            return CommandResult.Reject("Use a supply first and leave an inventory slot free.");
        if (def.Target != TargetKind.None && (!target.HasValue || !CardTargets(id).Contains(target.Value)))
            return CommandResult.Reject("Choose a highlighted legal target. AP has not been spent.");
        Troop? troop = def.Target == TargetKind.Troop ? _troops.Single(t => t.Cell == target) : null;
        int upgrade = card.Upgraded ? 1 : 0;
        _impacts.Clear();
        Ap -= def.Cost;
        _hand.Remove(id);
        _zones[id] = def.Effect == EffectKind.Deploy ? Zone.InPlay : def.Exhaust ? Zone.Exhausted : Zone.Discard;
        if (_zones[id] == Zone.Discard) _discard.Add(id);
        switch (def.Effect)
        {
            case EffectKind.Deploy:
                var deployed = new Troop(card, target!.Value);
                _troops.Add(deployed);
                TriggerFriendlyTile(deployed);
                break;
            case EffectKind.Network:
                OrganisationBlock += 7 + upgrade * 3;
                break;
            case EffectKind.Armour:
                troop!.Block += 7 + upgrade * 3;
                break;
            case EffectKind.Intercept:
                OrganisationBlock += 4 + upgrade * 3;
                troop!.Block += 4 + upgrade * 3;
                break;
            case EffectKind.Recon:
                IntelRounds = Math.Max(IntelRounds, 1 + upgrade);
                break;
            case EffectKind.March:
                troop!.MovesLeft += 1 + upgrade;
                troop.Marched = true;
                break;
            case EffectKind.Strike:
                StrikeBonus += 2 + upgrade;
                break;
            case EffectKind.Barricade:
                SetTile(new TileEffect(TileKind.Barricade, target!.Value, 2 + upgrade));
                break;
            case EffectKind.Exposed:
                SetTile(new TileEffect(TileKind.Exposed, target!.Value, 2, 2 + upgrade));
                break;
            default:
                ResolveAction(card, target);
                break;
        }
        AddLog($"{card.Name}{(target.HasValue ? " / " + target.Value : "")}.");
        CheckVictory();
        return CommandResult.Success();
    }

    public CommandResult BeginMovement()
    {
        if (Phase != Phase.Cards) return CommandResult.Reject("The card phase is already closed.");
        Phase = Phase.Movement;
        AddLog("ORDERS / Move each piece, or hold. Attacks resolve together.");
        return CommandResult.Success();
    }

    public HashSet<Cell> LegalMoves(Troop troop)
    {
        if (Phase != Phase.Movement || troop.MovesLeft == 0 || !_troops.Contains(troop)) return new();
        return Board.Cells.Where(cell =>
        {
            int dx = Math.Abs(cell.X - troop.Cell.X), dy = Math.Abs(cell.Y - troop.Cell.Y);
            bool matches = troop.Kind switch
            {
                PieceKind.Breacher => dx * dy == 2,
                PieceKind.Sapper => dx == 1 && dy == 1,
                PieceKind.Medic => Math.Max(dx, dy) == 1,
                _ => dx + dy == 1
            };
            return matches && CanEnter(cell) && Board.CanTraverse(troop.Cell, cell, troop.Kind == PieceKind.Breacher);
        }).ToHashSet();
    }

    public CommandResult Move(int cardId, Cell cell)
    {
        if (Phase != Phase.Movement) return CommandResult.Reject("Movement is only available during troop orders.");
        var troop = _troops.SingleOrDefault(t => t.Card.Id == cardId);
        if (troop is null) return CommandResult.Reject("That troop is not deployed.");
        if (!LegalMoves(troop).Contains(cell)) return CommandResult.Reject("Illegal movement. Choose a marked cell or Hold.");
        _impacts.Clear();
        troop.Cell = cell;
        troop.MovesLeft--;
        troop.MovesMade++;
        TriggerFriendlyTile(troop);
        if (_troops.Contains(troop) && troop.RootedRounds > 0) troop.MovesLeft = 0;
        return CommandResult.Success();
    }

    public CommandResult Hold(int cardId)
    {
        if (Phase != Phase.Movement) return CommandResult.Reject("Hold is only available during troop orders.");
        var troop = _troops.SingleOrDefault(t => t.Card.Id == cardId);
        if (troop is null) return CommandResult.Reject("That troop is not deployed.");
        troop.MovesLeft = 0;
        return CommandResult.Success();
    }

    public HashSet<Cell> AttackCells(PieceKind kind, Cell from) => Board.Cells.Where(cell =>
    {
        int dx = Math.Abs(cell.X - from.X), dy = Math.Abs(cell.Y - from.Y);
        return Board.ClearFire(from, cell) && (kind switch
        {
            PieceKind.Runner => dx + dy == 1,
            PieceKind.Marksman => dx == 0 && cell.Y < from.Y && dy <= 4,
            PieceKind.Breacher => Math.Max(dx, dy) == 1,
            PieceKind.Sapper => (dx == 0 || dy == 0) && dx + dy is > 0 and <= 2,
            PieceKind.Medic => dx + dy == 1,
            PieceKind.Sentinel => Math.Max(dx, dy) == 1,
            _ => false
        });
    }).ToHashSet();

    public (int Damage, string[] Combos) PreviewAttack(Troop troop, Enemy enemy)
    {
        int amount = troop.Damage + StrikeBonus + (HasRelic("command") ? 1 : 0) +
            HeightBonus(troop.Cell, enemy.Cell, troop.Kind is PieceKind.Marksman or PieceKind.Sapper);
        var combos = new List<string>();
        if (troop.AttackBonus > 0) { amount += troop.AttackBonus; combos.Add("UNITED FRONT"); }
        if (troop.HoldBonus > 0 && troop.MovesMade == 0) { amount += troop.HoldBonus; combos.Add("HOLD FAST"); }
        if (troop.Kind == PieceKind.Marksman && IntelRounds > 0)
        { amount += HasRelic("longwire") ? 4 : 3; combos.Add("GHOST SIGNAL"); }
        if (troop.Kind == PieceKind.Runner && troop.Marched && troop.MovesMade >= 2)
        { amount += 3; combos.Add("MOMENTUM"); }
        var exposed = _tiles.FirstOrDefault(t => t.Cell == enemy.Cell && t.Kind == TileKind.Exposed);
        if (exposed is not null)
        {
            amount += exposed.Power;
            if (StrikeBonus > 0) { amount += 3; combos.Add("KILL CIRCUIT"); }
        }
        if (enemy.Mark > 0) { amount += HasRelic("prism") ? 5 : 3; combos.Add("BREACH"); }
        return (amount, combos.ToArray());
    }

    public CommandResult ResolveTroops()
    {
        if (Phase != Phase.Movement) return CommandResult.Reject("Issue troop orders before resolving attacks.");
        _impacts.Clear();
        foreach (var troop in _troops.ToArray())
        {
            troop.MovesLeft = 0;
            TroopAttack(troop);
        }
        if (_enemies.Count == 0)
        {
            Phase = Phase.Victory;
            AddLog("SECTOR SECURED / Organisation HP carries forward.");
        }
        else
        {
            Phase = Phase.Discard;
            AddLog($"CLEANUP / Discard {RequiredDiscards}. Other cards stay in hand.");
        }
        return CommandResult.Success();
    }

    public CommandResult ConfirmDiscard(IEnumerable<int> selected)
    {
        if (Phase != Phase.Discard) return CommandResult.Reject("Discard is not available in this phase.");
        var ids = selected.ToArray();
        if (ids.Distinct().Count() != ids.Length || ids.Length != RequiredDiscards || ids.Any(id => !_hand.Contains(id)))
            return CommandResult.Reject($"Select exactly {RequiredDiscards} different cards from your hand.");
        foreach (int id in ids)
        {
            _hand.Remove(id);
            _discard.Add(id);
            _zones[id] = Zone.Discard;
        }
        ResolveEnemies();
        return CommandResult.Success();
    }

    private void SetTile(TileEffect tile)
    {
        _tiles.RemoveAll(t => t.Cell == tile.Cell && t.Kind == tile.Kind);
        _tiles.Add(tile);
    }

    internal HashSet<Cell> EnemyAttackCells(Enemy enemy) => PatternCells(enemy.Step.Pattern, enemy.Cell)
        .Intersect(PatternCells(enemy.Step.Pattern, enemy.Plan.destination)).ToHashSet();

    private HashSet<Cell> PatternCells(AttackPattern pattern, Cell origin) => Board.Cells.Where(cell =>
    {
        int dx = Math.Abs(cell.X - origin.X), dy = Math.Abs(cell.Y - origin.Y);
        return Board.ClearFire(origin, cell) && (pattern switch
        {
            AttackPattern.Cross => (dx == 0 || dy == 0) && dx + dy <= 2,
            AttackPattern.Adjacent => Math.Max(dx, dy) <= 1,
            AttackPattern.Column => dx == 0,
            AttackPattern.Row => dy == 0,
            _ => false
        });
    }).ToHashSet();

    public bool IsObserved(Enemy enemy) => IntelRounds > 0 || _troops.Any(t => t.Cell.Distance(enemy.Cell) <= 2);

    public Intent GetIntent(Enemy enemy)
    {
        bool observed = IsObserved(enemy);
        string label = enemy.Step.Kind switch
        {
            EnemyActionKind.Attack => "ATTACK",
            EnemyActionKind.Reinforce => "REINFORCE",
            EnemyActionKind.Lockdown => "TILE CONTROL",
            EnemyActionKind.Rest => "REST",
            EnemyActionKind.Mark => "MARK",
            EnemyActionKind.Command => "COMMAND",
            EnemyActionKind.Summon => "SUMMON",
            EnemyActionKind.Hazard => "HAZARD",
            EnemyActionKind.Push => "PUSH",
            _ => throw new InvalidOperationException("Invalid intent.")
        };
        string detail = !observed ? "SIGNAL OBSCURED" : enemy.Step.Kind switch
        {
            EnemyActionKind.Attack or EnemyActionKind.Push => $"{enemy.Step.Name} / {PlannedEnemyPower(enemy)} organisation damage",
            EnemyActionKind.Reinforce => $"{enemy.Step.Name} / +{enemy.Step.Power} guard",
            EnemyActionKind.Lockdown => $"{enemy.Step.Name} / deny marked tiles",
            _ => enemy.Step.Name
        };
        if (enemy.Jammed > 0) { label = "JAMMED"; detail = "Plan interrupted for this enemy phase."; }
        IReadOnlySet<Cell> cells = !observed || enemy.Jammed > 0 ? new HashSet<Cell>() :
            IsAttack(enemy.Step.Kind) || enemy.Step.Kind == EnemyActionKind.Lockdown
                ? PatternCells(enemy.Step.Pattern, enemy.Plan.destination)
                    .Intersect(PatternCells(enemy.Step.Pattern, ForecastPosition(enemy))).ToHashSet()
                : new HashSet<Cell> { enemy.Plan.target };
        int? power = observed && enemy.Jammed == 0 ? IsAttack(enemy.Step.Kind) ? PlannedEnemyPower(enemy) :
            enemy.Step.Kind == EnemyActionKind.Reinforce ? enemy.Step.Power : null : null;
        if (observed && enemy.Jammed == 0 && enemy.Plan.destination != enemy.Cell && ForecastPosition(enemy) == enemy.Cell)
            detail += " / move blocked";
        return new Intent(observed, label, detail, cells,
            observed && enemy.Jammed == 0 && enemy.Plan.destination != enemy.Cell ? enemy.Plan.destination : null,
            observed ? enemy.Plan.target : null, power);
    }

    public void AssertConservation()
    {
        var occurrences = _draw.Concat(_hand).Concat(_discard).Concat(_troops.Select(t => t.Card.Id))
            .Concat(_zones.Where(pair => pair.Value is Zone.Unavailable or Zone.Exhausted).Select(pair => pair.Key)).ToArray();
        if (occurrences.Length != _cards.Count || occurrences.Distinct().Count() != _cards.Count ||
            _draw.Any(id => _zones[id] != Zone.Draw) || _hand.Any(id => _zones[id] != Zone.Hand) ||
            _discard.Any(id => _zones[id] != Zone.Discard) || _troops.Any(t => _zones[t.Card.Id] != Zone.InPlay))
            throw new InvalidOperationException("Card conservation violated.");
    }

    private void AddLog(string text)
    {
        _log.Add(text);
        if (_log.Count > 80) _log.RemoveAt(0);
    }
}
