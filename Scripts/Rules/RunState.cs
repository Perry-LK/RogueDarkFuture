namespace FutureDark.Rules;

public enum RunScreen { City, Map, Combat, Reward, Event, Vendor, Complete, Defeat }
public enum RoomKind { Battle, Event, Elite, Vendor, Boss }
public sealed record RouteNode(string Id, string Name, string Subtitle, RoomKind Kind, int Column, int Row, string[] Next)
{
    public bool IsEvent => Kind == RoomKind.Event;
    public bool IsBoss => Kind == RoomKind.Boss;
}
public sealed record ShopOffer(string Kind, string DefinitionId, int Price);
public sealed record EventChoice(string Id, string Title, string Detail, int Cost = 0);

public sealed class RunState
{
    public const int MaxHp = 64;
    public const int MainActCount = 5;
    public const int FinaleAct = MainActCount + 1;
    public static string ActTitle(int act) => act switch
    {
        1 => "The Outer Blocks", 2 => "The Foundry Belt", 3 => "The Signal Ward",
        4 => "The Civic Spire", 5 => "The Glass Ward", 6 => "Central Command",
        _ => throw new ArgumentOutOfRangeException(nameof(act))
    };
    public int UnlockTier { get; }
    private readonly HashSet<string> _storyFlags = [];
    public IReadOnlySet<string> StoryFlags => _storyFlags;
    public static readonly string[] KnownStoryFlags = ["couriers", "relay", "sabotage", "broker", "pursuit", "dividend"];
    public string RunId { get; private set; } = Guid.NewGuid().ToString("N");
    public int Seed { get; }
    public int Act { get; private set; } = 1;
    public string ActName => ActTitle(Act);
    private int _hp = MaxHp;
    private readonly List<Card> _deck = Content.StartingDeck();
    private readonly HashSet<string> _completed = new();
    private readonly HashSet<int> _completedActs = new();
    private readonly List<string> _relics = new();
    private readonly List<string> _supplies = ["shield"];
    private int _nextId = 11;
    private string[] _available = ["entry"];
    private string? _current;
    private RunSnapshot? _roomEntry;
    private int _removals;
    private readonly HashSet<int> _purchased = new();
    public RunScreen Screen { get; private set; } = RunScreen.City;
    public Combat? Battle { get; private set; }
    public int Hp => Screen == RunScreen.Combat && Battle is not null ? Battle.OrganisationHp : _hp;
    public int Funds { get; private set; } = 65;
    public int TotalCombos { get; private set; }
    public int TotalEnemyCombos { get; private set; }
    public int Victories { get; private set; }
    public IReadOnlyList<Card> Deck => _deck;
    public IReadOnlySet<string> Completed => _completed;
    public IReadOnlySet<int> CompletedActs => _completedActs;
    public IReadOnlyList<string> Relics => _relics;
    public IReadOnlyList<string> Supplies => Battle?.Supplies ?? _supplies;
    public IReadOnlyList<string> Available => _available;
    public IReadOnlyList<CardDef> Rewards { get; private set; } = Array.Empty<CardDef>();
    public IReadOnlyList<ShopOffer> Shop { get; private set; } = Array.Empty<ShopOffer>();
    public IReadOnlySet<int> Purchased => _purchased;
    public int RemovalPrice => 45 + _removals * 15;
    public string RewardNote { get; private set; } = "";
    public RouteNode? CurrentNode => Nodes.SingleOrDefault(n => n.Id == _current);
    public IReadOnlyList<RouteNode> Nodes { get; private set; }
    public bool CentreUnlocked => _completedActs.Count * 3 >= MainActCount * 2;
    public string EventId => CurrentNode is not { } node ? "" :
        node.Column == 2 && Act <= 3 ? new[] { "courier", "relay", "aftermath" }[Act - 1] :
        node.Column == 7 && Act == 1 ? "broker" :
        node.Column == 7 && Act >= 3 && _storyFlags.Contains("broker") && !_storyFlags.Contains("dividend") ? "dividend" :
        EventIds[(NodeSeed(node) & int.MaxValue) % EventIds.Length];
    private static readonly string[] EventIds = ["safehouse", "salvage", "deal", "workshop", "dilemma"];

    public RunState(int seed, int unlockTier = 0)
    {
        if (unlockTier is < 0 or > 4) throw new ArgumentOutOfRangeException(nameof(unlockTier));
        UnlockTier = unlockTier;
        Seed = seed;
        Nodes = GenerateAct(seed, 1);
    }

    public static IReadOnlyList<RouteNode> GenerateAct(int seed, int act)
    {
        if (act is < 1 or > FinaleAct) throw new ArgumentOutOfRangeException(nameof(act));
        if (act == FinaleAct) return [new("finale", "The Architect", "The last transmission", RoomKind.Boss, 0, 1, [])];
        var random = new Random(unchecked(seed * 31 + act * 1009));
        var nodes = new List<RouteNode>();
        int[] counts = Enumerable.Range(0, 9).Select(depth => depth is 0 or 8 ? 1 : random.Next(2, 4)).ToArray();
        var connections = new Dictionary<(int Depth, int Lane), List<int>>();
        var edgeRandom = new Random(unchecked(seed * 149 + act * 4099));
        for (int depth = 0; depth < counts.Length - 1; depth++)
        {
            for (int lane = 0; lane < counts[depth]; lane++) connections[(depth, lane)] = [];
            int from = 0, to = 0;
            while (true)
            {
                connections[(depth, from)].Add(to);
                if (from == counts[depth] - 1 && to == counts[depth + 1] - 1) break;
                if (from == counts[depth] - 1) to++;
                else if (to == counts[depth + 1] - 1) from++;
                else
                {
                    // Advancing monotonically covers both layers without reversing lane order.
                    int step = edgeRandom.Next(3);
                    if (step != 1) from++;
                    if (step != 0) to++;
                }
            }
        }
        string[] battleNames = act switch
        {
            1 => ["Abandoned tram", "Flooded arcade", "Service alley", "Rust market", "Checkpoint 09"],
            2 => ["Cooling yard", "Slag channel", "Assembly line", "Freight gantry", "Furnace approach"],
            3 => ["Relay station", "Signal trench", "Transit junction", "Camera nest", "Dead broadcast"],
            4 => ["Archive steps", "Tribunal square", "Records annex", "Ministry bridge", "Civic checkpoint"],
            _ => ["Skybridge", "Executive plaza", "Glass concourse", "Tower lobby", "Security exchange"]
        };
        for (int depth = 0; depth < 9; depth++)
        for (int lane = 0; lane < counts[depth]; lane++)
        {
            RoomKind kind;
            if (depth == 8) kind = RoomKind.Boss;
            else if (depth == 0) kind = RoomKind.Battle;
            else if (depth == 3) kind = RoomKind.Vendor;
            else if (depth is 2 or 7) kind = RoomKind.Event;
            else
            {
                int roll = random.Next(100);
                kind = roll < 22 ? RoomKind.Event : roll < 42 && depth >= 4 ? RoomKind.Elite : RoomKind.Battle;
            }
            string id = depth == 0 ? "entry" : $"a{act}-{depth}-{lane}";
            string name = kind switch
            {
                RoomKind.Boss => new[] { "The Warden", "The Furnace Marshal", "The Listener", "The Prefect", "The Glass Marshal" }[act - 1],
                RoomKind.Vendor => "The Night Exchange",
                RoomKind.Event => act == 1 ? "A light in the ruins" : "A crack in the glass",
                RoomKind.Elite => "Red zone / " + battleNames[random.Next(battleNames.Length)],
                _ => battleNames[random.Next(battleNames.Length)]
            };
            var next = depth == 8 ? [] : connections[(depth, lane)]
                .Select(nextLane => $"a{act}-{depth + 1}-{nextLane}").ToArray();
            nodes.Add(new RouteNode(id, name, kind switch
            {
                RoomKind.Elite => "Coordinated squad / relic and increased funds",
                RoomKind.Vendor => "Buy cards, supplies and relics / sell supplies / remove cards",
                RoomKind.Event => "A choice with lasting consequences",
                RoomKind.Boss => "District command / permanent run reward",
                _ => depth < 3 ? "Patrol detail / card and funds" : "Tactical squad / card and funds"
            }, kind, depth, counts[depth] == 1 ? 1 : lane, next));
        }
        return nodes;
    }

    private int NodeSeed(RouteNode node) => unchecked(Seed * 397 + Act * 7919 + node.Column * 101 + node.Row * 17);

    public CommandResult StartAct(int act)
    {
        if (Screen != RunScreen.City) return CommandResult.Reject("Return to the city overview first.");
        if (act == FinaleAct ? !CentreUnlocked : act is < 1 or > MainActCount || _completedActs.Contains(act) ||
            (act > 1 && !_completedActs.Contains(act - 1)))
            return CommandResult.Reject("This district is not available yet.");
        Act = act;
        Nodes = GenerateAct(Seed, act);
        _current = null;
        _completed.Clear();
        _available = [act == FinaleAct ? "finale" : "entry"];
        Screen = RunScreen.Map;
        _roomEntry = null;
        return CommandResult.Success();
    }

    public CommandResult Enter(string nodeId)
    {
        if (Screen != RunScreen.Map || !_available.Contains(nodeId))
            return CommandResult.Reject("Choose a connected encounter on the route.");
        var node = Nodes.Single(n => n.Id == nodeId);
        _roomEntry = Capture() with { ReplayNode = nodeId };
        _current = nodeId;
        _purchased.Clear();
        RewardNote = "";
        if (node.IsEvent) { Screen = RunScreen.Event; return CommandResult.Success(); }
        if (node.Kind == RoomKind.Vendor)
        {
            var random = new Random(NodeSeed(node));
            Shop = Unlocks.Cards(UnlockTier).OrderBy(_ => random.Next()).Take(6)
                .Select(c => new ShopOffer("card", c.Id, (c.Cost == 0 ? 35 : c.Kind == CardKind.Troop ? 45 : 40) - (_storyFlags.Contains("broker") ? 8 : 0)))
                .Concat(Content.Supplies.OrderBy(_ => random.Next()).Take(3).Select(s => new ShopOffer("supply", s.Id, s.Price)))
                .Concat(Unlocks.Relics(UnlockTier).Where(r => !_relics.Contains(r.Id)).OrderBy(_ => random.Next()).Take(2)
                    .Select(r => new ShopOffer("relic", r.Id, r.Price))).ToArray();
            Screen = RunScreen.Vendor;
            return CommandResult.Success();
        }
        BeginFight(node);
        return CommandResult.Success();
    }

    private void BeginFight(RouteNode node)
    {
        var random = new Random(NodeSeed(node));
        EnemyDef[] squad;
        if (Act == FinaleAct) squad = [Content.Architect, Content.Commander, Content.Sniper, Content.Spotter];
        else if (node.IsBoss) squad = Act switch
        {
            1 => [Content.Warden, Content.Enforcer, Content.Spotter],
            2 => [Content.Controller with { Id = "furnace", Hp = 48 }, Content.Pusher, Content.Enforcer],
            3 => [Content.Commander with { Id = "listener", Hp = 52 }, Content.Spotter, Content.Sniper],
            4 => [Content.Reinforcer with { Id = "prefect", Hp = 54 }, Content.Commander, Content.Enforcer],
            _ => [Content.Warden with { Id = "marshal", Hp = 60 }, Content.Commander, Content.Sniper]
        };
        else if (node.Kind == RoomKind.Elite) squad = Act == 1 ? [Content.Commander, Content.Enforcer, Content.Patrol] :
            [Content.Controller, Content.Pusher, Content.Reinforcer];
        else if (Act == 1 && node.Column < 2) squad = [Content.Patrol, Content.Spotter];
        else
        {
            EnemyDef[][] choices = Act == 1
                ? [[Content.Spotter, Content.Sniper], [Content.Enforcer, Content.Patrol], [Content.Controller, Content.Pusher]]
                : [[Content.Spotter, Content.Sniper, Content.Enforcer], [Content.Commander, Content.Patrol, Content.Sniper],
                   [Content.Controller, Content.Pusher, Content.Patrol], [Content.Reinforcer, Content.Enforcer, Content.Patrol]];
            squad = choices[random.Next(choices.Length)];
        }
        if (Act >= 4 && Act != FinaleAct && (node.IsBoss || node.Kind == RoomKind.Elite || node.Column >= 5))
            squad = [.. squad, Content.Patrol];
        int rank = Math.Min(Act, MainActCount) - 1;
        squad = squad.Select((definition, i) => definition with
        {
            Name = node.IsBoss && i == 0 ? node.Name : definition.Name,
            Hp = definition.Hp + rank * (i == 0 && node.IsBoss ? 5 : 2) +
                (_storyFlags.Contains("pursuit") && node.Kind == RoomKind.Elite ? 3 : 0) -
                (_storyFlags.Contains("sabotage") && node.IsBoss && i == 0 ? 10 : 0),
            Routine = definition.Routine.Select(step => step.Kind is EnemyActionKind.Attack or EnemyActionKind.Push
                ? step with { Power = step.Power + (rank >= 3 ? 1 : 0) } : step).ToArray()
        }).ToArray();
        var board = Board.Generate(NodeSeed(node), Act, node.Column);
        var positions = new List<Cell>();
        Cell anchor = new(board.Width / 2, 1);
        foreach (var definition in squad)
        {
            var options = board.Cells.Where(board.Accessible).Where(c => c.Y < board.Height - 2 &&
                !positions.Contains(c) && !board.Hazards.ContainsKey(c)).ToArray();
            Cell position = positions.Count == 0 ? anchor : options.OrderBy(c => c.Distance(anchor) + random.NextDouble() * 2).First();
            positions.Add(position);
        }
        Battle = new Combat(_deck, _hp, board,
            squad.Select((definition, i) => new Spawn(definition, positions[i])), NodeSeed(node), _relics, _supplies);
        Screen = RunScreen.Combat;
    }

    public CommandResult FinishBattle()
    {
        if (Screen != RunScreen.Combat || Battle is null || Battle.Phase is not (Phase.Victory or Phase.Defeat))
            return CommandResult.Reject("There is no completed encounter to claim.");
        _hp = Battle.OrganisationHp;
        TotalCombos += Battle.ComboHits;
        TotalEnemyCombos += Battle.EnemyComboHits;
        _supplies.Clear();
        _supplies.AddRange(Battle.Supplies);
        if (Battle.Phase == Phase.Defeat)
        {
            Screen = RunScreen.Defeat;
            Battle = null;
            _roomEntry = null;
            return CommandResult.Success();
        }
        Victories++;
        var node = CurrentNode!;
        int reward = (node.IsBoss ? 65 : node.Kind == RoomKind.Elite ? 42 : 20) + (_relics.Contains("bounty") ? 8 : 0);
        Funds += reward;
        _hp = Math.Min(MaxHp, _hp + (node.IsBoss ? 18 : 3) + (_relics.Contains("triage") ? 3 : 0));
        RewardNote = $"+{reward} funds. Recovered {(node.IsBoss ? 18 : 3) + (_relics.Contains("triage") ? 3 : 0)} HP (up to maximum).";
        var random = new Random(NodeSeed(node) ^ 0x4a19);
        if (node.IsBoss || node.Kind == RoomKind.Elite)
        {
            var relic = Unlocks.Relics(UnlockTier).Where(r => !_relics.Contains(r.Id)).OrderBy(_ => random.Next()).FirstOrDefault();
            if (relic is not null) { _relics.Add(relic.Id); RewardNote += $"\nRelic acquired: {relic.Name}. {relic.Description}"; }
        }
        if (_supplies.Count < 3 && (node.Kind == RoomKind.Elite || random.Next(3) == 0))
        {
            var supply = Content.Supplies[random.Next(Content.Supplies.Length)];
            _supplies.Add(supply.Id);
            RewardNote += $"\nSupply acquired: {supply.Name}.";
        }
        Rewards = Unlocks.Cards(UnlockTier).OrderBy(_ => random.Next()).Take(3).ToArray();
        Screen = RunScreen.Reward;
        Battle = null;
        return CommandResult.Success();
    }

    public CommandResult ClaimReward(string? definitionId)
    {
        if (Screen != RunScreen.Reward) return CommandResult.Reject("No reward is pending.");
        if (definitionId is not null)
        {
            var definition = Rewards.SingleOrDefault(r => r.Id == definitionId);
            if (definition is null) return CommandResult.Reject("Choose one of the offered cards.");
            _deck.Add(new Card(_nextId++, definition));
        }
        Rewards = Array.Empty<CardDef>();
        CompleteNode();
        return CommandResult.Success();
    }

    public CommandResult Purchase(int index)
    {
        if (Screen != RunScreen.Vendor || index < 0 || index >= Shop.Count || _purchased.Contains(index))
            return CommandResult.Reject("That offer is not available.");
        var offer = Shop[index];
        if (Funds < offer.Price) return CommandResult.Reject("Insufficient funds.");
        if (offer.Kind == "supply" && _supplies.Count >= 3) return CommandResult.Reject("Supply belt full. Sell a supply first.");
        if (offer.Kind == "relic" && _relics.Contains(offer.DefinitionId)) return CommandResult.Reject("You already own this relic.");
        switch (offer.Kind)
        {
            case "card": _deck.Add(new Card(_nextId++, Content.Card(offer.DefinitionId))); break;
            case "supply": _supplies.Add(offer.DefinitionId); break;
            case "relic": _relics.Add(offer.DefinitionId); break;
            default: throw new InvalidOperationException("Unknown vendor offer.");
        }
        Funds -= offer.Price;
        _purchased.Add(index);
        return CommandResult.Success("Purchased. Your run inventory has been updated.");
    }

    public CommandResult SellSupply(int slot)
    {
        if (Screen != RunScreen.Vendor || slot < 0 || slot >= _supplies.Count)
            return CommandResult.Reject("Select an owned supply at the vendor.");
        int amount = Content.Supply(_supplies[slot]).Price / 2;
        _supplies.RemoveAt(slot);
        Funds += amount;
        return CommandResult.Success($"Supply sold for {amount} funds.");
    }

    public CommandResult RemoveCard(int cardId)
    {
        if (Screen != RunScreen.Vendor) return CommandResult.Reject("Card removal requires a vendor.");
        if (_deck.Count <= 8) return CommandResult.Reject("Keep at least eight cards in your deck.");
        if (Funds < RemovalPrice) return CommandResult.Reject("Insufficient funds for card removal.");
        var card = _deck.SingleOrDefault(c => c.Id == cardId);
        if (card is null) return CommandResult.Reject("That card is not in this run's deck.");
        Funds -= RemovalPrice;
        _removals++;
        _deck.Remove(card);
        return CommandResult.Success("Card removed permanently from this run.");
    }

    public CommandResult LeaveVendor()
    {
        if (Screen != RunScreen.Vendor) return CommandResult.Reject("You are not at a vendor.");
        CompleteNode();
        return CommandResult.Success();
    }

    public string EventStory => EventId switch
    {
        "courier" => "A courier cell is trapped below a checkpoint. Shelter them now and they promise help in the next district.",
        "relay" => _storyFlags.Contains("couriers") ? "The couriers kept their word. They can restore a relay that reaches deep into district command." :
            "A damaged relay could give your movement a lasting advantage, if you can fund its repair.",
        "aftermath" => _storyFlags.Contains("relay") ? "Your relay crew has found a back door into command logistics. Their next transmission could weaken every remaining district boss." :
            "An independent relay crew offers access to command logistics. Their help is not free.",
        "broker" => "A broker needs seed money for a hidden supply route. Invest now for cheaper cards at later vendors, or steal the ledger and attract elite pursuers.",
        "dividend" => "The supply route you funded is thriving. Your broker has kept a share aside. This payment is available once.",
        "safehouse" => "Behind a sealed door, volunteers keep a generator alive. There are beds, tools, and barely enough time for one.",
        "salvage" => "A transport lies abandoned across the street. Its cargo is intact. Its silent alarm may not be.",
        "deal" => "A broker offers a device stripped of every identifying mark. No questions. Payment before inspection.",
        "workshop" => "An old technician offers to make your equipment lighter or better. The workshop will move before dawn.",
        _ => "A trapped courier carries medical supplies and encrypted funds. Saving both will take resources you may need later."
    };
    public IReadOnlyList<EventChoice> EventChoices => EventId switch
    {
        "courier" => [new("shelter", "Shelter the couriers", "Pay 15 funds. Heal 10 HP. Their crew repairs the next district's relay for free.", 15), new("cash", "Recover their cargo", "Take 28 funds instead. The relay will require funding.")],
        "relay" => [new("relay", "Restore the relay", "Heal 12 HP and establish a crew. Later command sabotage becomes free.", _storyFlags.Contains("couriers") ? 0 : 25), new("cash", "Strip the equipment", "Gain 28 funds. Leave the relay offline.")],
        "aftermath" => [new("sabotage", "Undermine command", "All remaining district bosses start with 10 less HP. Terrain never cancels organisation damage.", _storyFlags.Contains("relay") ? 0 : 45), new("heal", "Extract the volunteers", "Heal 20 HP instead.")],
        "broker" => [new("invest", "Fund the supply route", "Pay 20 funds. Cards at later vendors cost 8 less; a later event pays a dividend.", 20), new("steal", "Steal the ledger", "Gain 40 funds. Future elite enemies gain 3 HP.")],
        "dividend" => [new("dividend", "Collect the dividend", "Gain 60 funds. Collect once per run."), new("donate", "Fund the safehouses", "Heal 25 HP instead. The dividend is consumed.")],
        "safehouse" => [new("heal", "Treat the wounded", "Restore 20 organisation HP."), new("upgrade", "Refit equipment", "Permanently upgrade one card.")],
        "salvage" => [new("cash", "Take the loose salvage", "Gain 28 funds; no fight."), new("risk", "Open the sealed hold", "Fight a tactical squad. Gain 25 funds now plus normal victory rewards.")],
        "deal" => [new("relic", "Buy the unmarked device", "Gain an unowned relic. Pay 60 funds.", 60), new("supply", "Trade for supplies", "Gain a deterministic random supply if a slot is free. Pay 12 funds.", 12), new("leave", "Walk away", "Keep your funds. Nothing else changes.")],
        "workshop" => [new("upgrade", "Improve a card", "Choose a permanent upgrade."), new("remove", "Travel lighter", "Remove one card for free; keep at least eight.")],
        _ => [new("heal", "Rescue the medics", "Restore 20 HP. Leave the funds behind."), new("cash", "Recover the ledger", "Gain 28 funds. Leave the medical cache behind."), new("both", "Arrange extraction", "Pay 20 funds. Restore 16 HP and gain a supply if a slot is free.", 20)]
    };

    public CommandResult ResolveEvent(string choiceId, int? cardId = null)
    {
        if (Screen != RunScreen.Event) return CommandResult.Reject("This event has already resolved.");
        var choice = EventChoices.SingleOrDefault(c => c.Id == choiceId);
        if (choice is null) return CommandResult.Reject("Choose an offered event option.");
        if (Funds < choice.Cost) return CommandResult.Reject("Insufficient funds for that option.");
        var random = new Random(NodeSeed(CurrentNode!) ^ 0x1673);
        switch (choiceId)
        {
            case "shelter": _storyFlags.Add("couriers"); _hp = Math.Min(MaxHp, _hp + 10); break;
            case "relay": _storyFlags.Add("relay"); _hp = Math.Min(MaxHp, _hp + 12); break;
            case "sabotage": _storyFlags.Add("sabotage"); break;
            case "invest": _storyFlags.Add("broker"); break;
            case "steal": _storyFlags.Add("pursuit"); Funds += 40; break;
            case "dividend": _storyFlags.Add("dividend"); Funds += 60; break;
            case "donate": _storyFlags.Add("dividend"); _hp = Math.Min(MaxHp, _hp + 25); break;
            case "heal": _hp = Math.Min(MaxHp, _hp + 20); break;
            case "cash": Funds += 28; break;
            case "upgrade":
                int index = _deck.FindIndex(c => c.Id == cardId && !c.Upgraded);
                if (index < 0) return CommandResult.Reject("Choose a card that has not been upgraded.");
                _deck[index] = _deck[index] with { Upgraded = true };
                break;
            case "remove":
                var card = _deck.SingleOrDefault(c => c.Id == cardId);
                if (card is null || _deck.Count <= 8) return CommandResult.Reject("Choose an owned card and keep at least eight.");
                _deck.Remove(card);
                break;
            case "relic":
                var relic = Unlocks.Relics(UnlockTier).Where(r => !_relics.Contains(r.Id)).OrderBy(_ => random.Next()).FirstOrDefault();
                if (relic is null) return CommandResult.Reject("You already own every relic in this prototype.");
                _relics.Add(relic.Id);
                break;
            case "supply":
                if (_supplies.Count >= 3) return CommandResult.Reject("Supply belt full. Choose another option.");
                _supplies.Add(Content.Supplies[random.Next(Content.Supplies.Length)].Id);
                break;
            case "both":
                _hp = Math.Min(MaxHp, _hp + 16);
                if (_supplies.Count < 3) _supplies.Add(Content.Supplies[random.Next(Content.Supplies.Length)].Id);
                break;
            case "risk":
                Funds += 25;
                BeginFight(CurrentNode!);
                return CommandResult.Success();
            case "leave": break;
            default: throw new InvalidOperationException("Unknown event choice.");
        }
        Funds -= choice.Cost;
        CompleteNode();
        return CommandResult.Success("Event resolved.");
    }

    public CommandResult EventHeal() => ResolveEvent("heal");
    public CommandResult EventUpgrade(int cardId) => ResolveEvent("upgrade", cardId);

    private void CompleteNode()
    {
        var node = CurrentNode ?? throw new InvalidOperationException("No active room.");
        _completed.Add(node.Id);
        _available = node.Next;
        _roomEntry = null;
        if (node.IsBoss)
        {
            if (Act == FinaleAct) Screen = RunScreen.Complete;
            else { _completedActs.Add(Act); Screen = RunScreen.City; }
        }
        else Screen = RunScreen.Map;
    }

    public RunSnapshot SnapshotForSave()
    {
        if (Screen is RunScreen.Defeat or RunScreen.Complete || Battle?.Phase == Phase.Defeat)
            throw new InvalidOperationException("A finished run cannot be saved.");
        return _roomEntry ?? Capture();
    }

    private RunSnapshot Capture() => new()
    {
        RunId = RunId, Seed = Seed, Act = Act, Hp = _hp, Funds = Funds, Screen = Screen, UnlockTier = UnlockTier, StoryFlags = _storyFlags.ToArray(),
        Cards = _deck.Select(c => new SavedCard(c.Id, c.Definition.Id, c.Upgraded)).ToArray(),
        Relics = _relics.ToArray(), Supplies = _supplies.ToArray(), Completed = _completed.ToArray(),
        CompletedActs = _completedActs.ToArray(), Available = _available.ToArray(), Current = _current,
        TotalCombos = TotalCombos, TotalEnemyCombos = TotalEnemyCombos, Victories = Victories, Removals = _removals
    };

    public static RunState Restore(RunSnapshot snapshot)
    {
        snapshot.Validate();
        var run = new RunState(snapshot.Seed, snapshot.UnlockTier)
        {
            RunId = snapshot.RunId, Act = snapshot.Act, _hp = snapshot.Hp, Funds = snapshot.Funds,
            Screen = snapshot.Screen, TotalCombos = snapshot.TotalCombos, TotalEnemyCombos = snapshot.TotalEnemyCombos,
            Victories = snapshot.Victories, _removals = snapshot.Removals,
            _available = snapshot.Available.ToArray(), _current = snapshot.Current
        };
        run.Nodes = GenerateAct(snapshot.Seed, snapshot.Act);
        run._deck.Clear();
        run._deck.AddRange(snapshot.Cards.Select(c => new Card(c.Id, Content.Card(c.DefinitionId), c.Upgraded)));
        run._nextId = run._deck.Max(c => c.Id) + 1;
        run._relics.AddRange(snapshot.Relics);
        run._supplies.Clear();
        run._supplies.AddRange(snapshot.Supplies);
        run._completed.UnionWith(snapshot.Completed);
        run._completedActs.UnionWith(snapshot.CompletedActs);
        run._storyFlags.UnionWith(snapshot.StoryFlags);
        if (snapshot.ReplayNode is not null)
        {
            var result = run.Enter(snapshot.ReplayNode);
            if (!result.Ok) throw new InvalidDataException(result.Message);
        }
        return run;
    }
}
