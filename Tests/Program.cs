using FutureDark.Rules;

int failures = 0, passed = 0;
var tests = new (string Name, Action Test)[]
{
    ("organisation damage ignores distance, terrain and zero troop targets", OrganisationDamage),
    ("each recipient independently consumes block and receives full attack", IndependentBlock),
    ("non-attacks do not damage and enemy guard persists through routines", Routines),
    ("defeated cards stay unavailable; next combat resets troops and upgrades", TroopLifecycle),
    ("overflow, reshuffles and exhausted piles conserve unique instances", DrawLifecycle),
    ("mandatory discard uses min(2, hand), preserves retained cards and rejects duplicates", Discards),
    ("invalid commands cannot spend AP, deploy twice or replay resolution", InvalidCommands),
    ("knight movement jumps obstacles but rejects holes and denied destinations", Movement),
    ("automatic attacks hit all covered enemies and skip killed targets", MultiTarget),
    ("intel appears and expires without leaking hidden upcoming details", Intel),
    ("raw danger warning switches exactly at 25 percent regardless of block", DangerBoundary),
    ("ordinary and fully guarded attacks never emit a combo effect", NoFalseCombos),
    ("recon + marksman produces Ghost Signal on positive damage", GhostSignal),
    ("Forced March requires two actual Runner moves to produce Momentum", Momentum),
    ("exposure and coordinated strike combine into Kill Circuit", KillCircuit),
    ("distinct combos stack; temporary strike and intel expire on schedule", StackedCombos),
    ("temporary board effects last exactly their stated enemy phases", TileExpiry),
    ("organisation defeat stops later enemies immediately", ImmediateDefeat),
    ("branching routes, rewards, healing and upgrades are applied once", RunProgression),
    ("long campaigns preserve districts and reach a terminal outcome", CompleteRun),
    ("enemy movement is committed, legal and interruptible without suppressing organisation damage", ExpansionChecks.CommittedMovement),
    ("commander forecasts and hazard displacement match their committed resolution", ExpansionChecks.CommandAndHazard),
    ("shield formation breaks on jam and precision depends on its spotter", ExpansionChecks.EnemyCombos),
    ("reinforcements wait until next round and obey blocked landing zones", ExpansionChecks.Reinforcements),
    ("marks, corrosion, detonation and displacement produce correct action combinations", ExpansionChecks.ActionCombos),
    ("supply limits, amplification, recovery and exhaustion conserve resources", ExpansionChecks.Supplies),
    ("the whole expanded card pool has legal normal and upgraded resolutions", ExpansionChecks.CardPool),
    ("seeded routes, vendor purchases, relics and removal obey economic invariants", ExpansionChecks.Economy),
    ("scrolling route trees have reachable branches without crossing or overlapping streets", RouteChecks.PlanarRoutes),
    ("district terrain remains connected and cover blocks then opens troop fire", NewSystemsChecks.Terrain),
    ("height and persistent hazards damage board units independently of organisation HP", NewSystemsChecks.HeightDamage),
    ("XP unlocks are idempotent and three autosave slots preserve frozen reward pools", NewSystemsChecks.ProgressAndSlots),
    ("linked narrative choices persist and change later district encounters", NewSystemsChecks.Narrative),
    ("manual saves use unique run identities and restore deterministic room-entry state", ExpansionChecks.Saves)
};

foreach (var (name, test) in tests)
{
    if (args.Length > 0 && !args.Any(arg => name.Contains(arg, StringComparison.OrdinalIgnoreCase))) continue;
    try { test(); Console.WriteLine($"PASS {name}"); passed++; }
    catch (Exception error) { Console.Error.WriteLine($"FAIL {name}\n{error}"); failures++; }
}
Console.WriteLine($"{passed} passed; {failures} failed.");
return failures == 0 && passed > 0 ? 0 : 1;

static void Check(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
}
static void Equal<T>(T expected, T actual, string message = "") where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{message} Expected {expected}; got {actual}.");
}
static void Ok(CommandResult result) => Check(result.Ok, result.Message);
static EnemyDef Dummy(int power = 0, int hp = 100, AttackPattern pattern = AttackPattern.Cross)
    => new("test", "Test unit", hp, [new(power == 0 ? EnemyActionKind.Rest : EnemyActionKind.Attack, power, pattern, "Test routine")]);
static Card[] Deck(params string[] ids) => ids.Select((id, index) => new Card(index + 1, Content.Card(id))).ToArray();
static Combat Fight(Card[] cards, EnemyDef? enemy = null, Cell? enemyCell = null, int hp = 56, Board? board = null)
    => new(cards, hp, board ?? new Board(), [new Spawn(enemy ?? Dummy(), enemyCell ?? new Cell(2, 1))], 42);
static int Id(Combat battle, string definition) => battle.Hand.First(id => battle.GetCard(id).Definition.Id == definition);
static void Play(Combat battle, string definition, Cell? target = null) => Ok(battle.Play(Id(battle, definition), target));
static void EndRound(Combat battle)
{
    if (battle.Phase == Phase.Cards) Ok(battle.BeginMovement());
    if (battle.Phase == Phase.Movement) Ok(battle.ResolveTroops());
    if (battle.Phase == Phase.Discard) Ok(battle.ConfirmDiscard(battle.Hand.Take(battle.RequiredDiscards).ToArray()));
    battle.AssertConservation();
}

static void OrganisationDamage()
{
    var battle = Fight(Deck("network"), Dummy(6), new Cell(6, 3), hp: 20, board: new Board(2));
    EndRound(battle);
    Equal(14, battle.OrganisationHp);
    Equal(0, battle.Troops.Count);
}

static void IndependentBlock()
{
    var battle = Fight(Deck("runner", "runner", "intercept"), Dummy(6), new Cell(3, 4), hp: 20);
    Play(battle, "runner", new Cell(2, 4));
    Play(battle, "runner", new Cell(4, 4));
    Play(battle, "intercept", new Cell(2, 4));
    Equal(4, battle.OrganisationBlock);
    EndRound(battle);
    Equal(18, battle.OrganisationHp, "Only organisation's block protects organisation.");
    Equal(9, battle.Troops[0].Hp);
    Equal(5, battle.Troops[1].Hp, "Second troop receives full six, not split damage.");
    Equal(0, battle.OrganisationBlock);
    Check(battle.Troops.All(t => t.Block == 0), "Temporary troop block must expire.");

    var separate = Fight(Deck("runner", "armour", "network"), Dummy(6), new Cell(3, 4));
    Play(separate, "runner", new Cell(2, 4));
    Play(separate, "armour", new Cell(2, 4));
    Play(separate, "network");
    EndRound(separate);
    Equal(56, separate.OrganisationHp);
    Equal(11, separate.Troops.Single().Hp);
    Equal(0, separate.Troops.Single().Block, "Leftover troop block expires after enemies.");
    Equal(0, separate.OrganisationBlock, "Leftover organisation block expires after enemies.");
}

static void Routines()
{
    var enemy = new EnemyDef("routine", "Routine", 100,
    [
        new(EnemyActionKind.Reinforce, 3, AttackPattern.Cross, "Brace"),
        new(EnemyActionKind.Attack, 4, AttackPattern.Cross, "Fire"),
        new(EnemyActionKind.Rest, 0, AttackPattern.Cross, "Rest")
    ]);
    var battle = Fight(Deck("network"), enemy);
    EndRound(battle);
    Equal(56, battle.OrganisationHp);
    Equal(3, battle.Enemies[0].Block);
    Equal(1, battle.Enemies[0].RoutineIndex);
    EndRound(battle);
    Equal(52, battle.OrganisationHp);
    Equal(3, battle.Enemies[0].Block);
    EndRound(battle);
    Equal(52, battle.OrganisationHp);
    Equal(0, battle.Enemies[0].RoutineIndex);
    EndRound(battle);
    Equal(6, battle.Enemies[0].Block, "Unconsumed enemy guard stacks.");
}

static void TroopLifecycle()
{
    Card[] deck = [new Card(1, Content.Card("runner"), true)];
    var battle = Fight(deck, Dummy(20), new Cell(3, 4));
    Play(battle, "runner", new Cell(2, 4));
    Equal(13, battle.Troops[0].Hp);
    EndRound(battle);
    Equal(Zone.Unavailable, battle.Zones[1]);
    Equal(0, battle.Hand.Count);
    battle.DrawCards(20);
    Equal(0, battle.Hand.Count);
    battle.AssertConservation();
    var next = Fight(deck, hp: battle.OrganisationHp);
    Play(next, "runner", new Cell(2, 4));
    Equal(13, next.Troops[0].Hp);
    Equal(4, next.Troops[0].Damage, "Upgrade applied exactly once.");
    Equal(36, next.OrganisationHp);
}

static void DrawLifecycle()
{
    var deck = Enumerable.Range(1, 12).Select(id => new Card(id, Content.Card("network"))).ToArray();
    var battle = Fight(deck);
    Equal(5, battle.Hand.Count);
    battle.DrawCards(40);
    Equal(9, battle.Hand.Count);
    Equal(3, battle.DiscardPile.Count);
    battle.AssertConservation();
    var tiny = Fight(Deck("marksman"));
    Play(tiny, "marksman", new Cell(2, 4));
    tiny.DrawCards(100);
    Equal(0, tiny.Hand.Count);
    Equal(Zone.InPlay, tiny.Zones[1]);
    tiny.AssertConservation();
    var sameSeed = Fight(deck);
    var sameSeedAgain = Fight(deck);
    Check(sameSeed.Hand.SequenceEqual(sameSeedAgain.Hand), "Equal seeds must reproduce draws.");
}

static void Discards()
{
    for (int count = 0; count <= 3; count++)
    {
        var battle = Fight(Enumerable.Range(1, Math.Max(1, count)).Select(id => new Card(id, Content.Card("network"))).ToArray());
        if (count == 0) Play(battle, "network");
        Ok(battle.BeginMovement());
        Ok(battle.ResolveTroops());
        Equal(Math.Min(count, 2), battle.RequiredDiscards);
        int[] selection = battle.Hand.Take(battle.RequiredDiscards).ToArray();
        int[] retained = battle.Hand.Except(selection).ToArray();
        if (count >= 2)
        {
            Check(!battle.ConfirmDiscard([selection[0], selection[0]]).Ok, "Duplicate discard must reject.");
            Equal(count, battle.Hand.Count);
        }
        Ok(battle.ConfirmDiscard(selection));
        Check(retained.All(id => battle.Hand.Contains(id)), "Unselected cards must be retained.");
        battle.AssertConservation();
    }
}

static void InvalidCommands()
{
    var battle = Fight(Deck("marksman", "runner", "network"));
    int marksman = Id(battle, "marksman");
    Check(!battle.Play(marksman, new Cell(2, 1)).Ok, "Outside deployment must reject.");
    Equal(3, battle.Ap);
    Equal(Zone.Hand, battle.Zones[marksman]);
    Ok(battle.Play(marksman, new Cell(2, 4)));
    Check(!battle.Play(marksman, new Cell(3, 4)).Ok, "Cannot deploy card twice.");
    Equal(1, battle.Ap);
    Check(!battle.Move(marksman, new Cell(2, 3)).Ok, "Cannot move during cards.");
    Ok(battle.BeginMovement());
    Check(!battle.Play(Id(battle, "network")).Ok, "Cannot play cards after phase closes.");
    Check(!battle.Move(marksman, new Cell(0, 0)).Ok, "Illegal move must reject.");
    Equal(new Cell(2, 4), battle.Troops[0].Cell);
    Ok(battle.ResolveTroops());
    int hp = battle.Enemies[0].Hp;
    Check(!battle.ResolveTroops().Ok, "Repeated resolution must reject.");
    Equal(hp, battle.Enemies[0].Hp);
    battle.AssertConservation();
}

static void Movement()
{
    var battle = Fight(Deck("breacher", "barricade"), board: new Board(1));
    Play(battle, "breacher", new Cell(2, 4));
    Play(battle, "barricade", new Cell(4, 3));
    Ok(battle.BeginMovement());
    var troop = battle.Troops[0];
    Check(!battle.LegalMoves(troop).Contains(new Cell(3, 2)), "Missing cell must reject.");
    Check(!battle.LegalMoves(troop).Contains(new Cell(1, 2)), "Terrain destination must reject.");
    Check(!battle.LegalMoves(troop).Contains(new Cell(4, 3)), "Denied destination must reject.");
    Check(battle.LegalMoves(troop).Contains(new Cell(0, 3)), "Knight must leap over intervening terrain.");
    Ok(battle.Move(troop.Card.Id, new Cell(0, 3)));
    Equal(0, troop.MovesLeft);
    Check(!battle.Move(troop.Card.Id, new Cell(2, 4)).Ok, "Move allowance must be enforced.");
    Ok(battle.Hold(troop.Card.Id));
}

static void MultiTarget()
{
    var battle = new Combat(Deck("marksman"), 56, new Board(),
        [new Spawn(Dummy(hp: 4), new Cell(2, 1)), new Spawn(Dummy(hp: 4), new Cell(2, 2))], 1);
    Play(battle, "marksman", new Cell(2, 4));
    EndRound(battle);
    Equal(Phase.Victory, battle.Phase);
    Equal(2, battle.Impacts.Count);
    var skipped = Fight(Deck("runner", "runner"), Dummy(hp: 3), new Cell(3, 4));
    Play(skipped, "runner", new Cell(2, 4));
    Play(skipped, "runner", new Cell(4, 4));
    EndRound(skipped);
    Equal(1, skipped.Impacts.Count, "Dead targets must be skipped in deployment order.");
}

static void Intel()
{
    var battle = Fight(Deck("runner", "recon"), Dummy(1));
    Play(battle, "runner", new Cell(2, 4));
    var enemy = battle.Enemies[0];
    var hidden = battle.GetIntent(enemy);
    Check(!hidden.Observed && hidden.Cells.Count == 0, "Hidden intent must not expose target cells.");
    Equal("SIGNAL OBSCURED", hidden.Detail);
    Play(battle, "recon");
    Check(battle.GetIntent(enemy).Observed, "Recon must reveal exact intent.");
    EndRound(battle);
    Check(!battle.GetIntent(enemy).Observed, "Expired recon must hide distant enemy again.");
    Ok(battle.BeginMovement());
    Ok(battle.Move(battle.Troops[0].Card.Id, new Cell(2, 3)));
    Check(battle.GetIntent(enemy).Observed, "Two-cell proximity must reveal.");
    EndRound(battle);
    Ok(battle.BeginMovement());
    Ok(battle.Move(battle.Troops[0].Card.Id, new Cell(2, 4)));
    Check(!battle.GetIntent(enemy).Observed, "Leaving proximity must hide again.");
}

static void DangerBoundary()
{
    var below = Fight(Deck("network"), Dummy(6), hp: 25);
    Check(!below.Danger, "6 must be below 25 percent of 25.");
    var boundary = Fight(Deck("network"), Dummy(6), hp: 24);
    Check(boundary.Danger, "6 must meet 25 percent of 24.");
    Play(boundary, "network");
    Check(boundary.Danger, "Raw threat must ignore block.");
}

static void NoFalseCombos()
{
    var ordinary = Fight(Deck("marksman"));
    Play(ordinary, "marksman", new Cell(2, 4));
    Ok(ordinary.BeginMovement());
    Ok(ordinary.ResolveTroops());
    Equal(4, ordinary.Impacts[0].Damage);
    Equal(0, ordinary.Impacts[0].Combos.Length);
    var blocked = Fight(Deck("marksman", "recon"));
    blocked.Enemies[0].Block = 100;
    Play(blocked, "marksman", new Cell(2, 4));
    Play(blocked, "recon");
    Ok(blocked.BeginMovement());
    Ok(blocked.ResolveTroops());
    Equal(0, blocked.Impacts[0].Damage);
    Equal(0, blocked.ComboHits);
    Equal(0, blocked.Impacts[0].Combos.Length, "Fully guarded attacks must not ignite.");
}

static void GhostSignal()
{
    var battle = Fight(Deck("marksman", "recon"));
    Play(battle, "marksman", new Cell(2, 4));
    Play(battle, "recon");
    Ok(battle.BeginMovement());
    Ok(battle.ResolveTroops());
    Equal(7, battle.Impacts[0].Damage);
    Equal("GHOST SIGNAL", battle.Impacts[0].Combos.Single());
    Equal(1, battle.ComboHits);
}

static void Momentum()
{
    foreach (bool twice in new[] { false, true })
    {
        var battle = Fight(Deck("runner", "march"), enemyCell: new Cell(3, 2));
        Play(battle, "runner", new Cell(2, 4));
        Play(battle, "march", new Cell(2, 4));
        Ok(battle.BeginMovement());
        var troop = battle.Troops[0];
        if (twice)
        {
            Ok(battle.Move(troop.Card.Id, new Cell(2, 3)));
            Ok(battle.Move(troop.Card.Id, new Cell(3, 3)));
        }
        else
        {
            Ok(battle.Move(troop.Card.Id, new Cell(3, 4)));
            Ok(battle.Hold(troop.Card.Id));
        }
        var preview = battle.PreviewAttack(troop, battle.Enemies[0]);
        Equal(twice ? 6 : 3, preview.Damage);
        Equal(twice ? 1 : 0, preview.Combos.Length, "Hold must not count as moving.");
        Ok(battle.ResolveTroops());
        Equal(twice ? 1 : 0, battle.ComboHits);
    }
}

static void KillCircuit()
{
    var battle = Fight(Deck("runner", "strike", "exposed"), enemyCell: new Cell(2, 3));
    Play(battle, "runner", new Cell(2, 4));
    Play(battle, "strike");
    Play(battle, "exposed", new Cell(2, 3));
    Ok(battle.BeginMovement());
    Ok(battle.ResolveTroops());
    Equal(10, battle.Impacts[0].Damage);
    Equal("KILL CIRCUIT", battle.Impacts[0].Combos.Single());
}

static void StackedCombos()
{
    var battle = Fight(Deck("marksman", "recon", "strike", "exposed", "network"));
    Play(battle, "marksman", new Cell(2, 4));
    Play(battle, "recon");
    Play(battle, "strike");
    EndRound(battle);
    Equal(0, battle.StrikeBonus);
    Equal(0, battle.IntelRounds);
    Play(battle, "recon");
    Play(battle, "strike");
    Play(battle, "exposed", new Cell(2, 1));
    Ok(battle.BeginMovement());
    Ok(battle.ResolveTroops());
    Equal(14, battle.Impacts[0].Damage);
    Check(battle.Impacts[0].Combos.SequenceEqual(new[] { "GHOST SIGNAL", "KILL CIRCUIT" }), "Distinct synergies must stack.");
    Card[] upgraded = [new Card(1, Content.Card("recon"), true)];
    var longer = Fight(upgraded);
    Play(longer, "recon");
    EndRound(longer);
    Equal(1, longer.IntelRounds);
    EndRound(longer);
    Equal(0, longer.IntelRounds);
}

static void TileExpiry()
{
    var battle = Fight(Deck("barricade", "exposed"));
    Play(battle, "barricade", new Cell(3, 3));
    Play(battle, "exposed", new Cell(2, 1));
    EndRound(battle);
    Check(battle.IsDenied(new Cell(3, 3)), "Barricade must survive first enemy phase.");
    Equal(2, battle.Tiles.Count);
    EndRound(battle);
    Check(!battle.IsDenied(new Cell(3, 3)), "Barricade must expire after second phase.");
    Equal(0, battle.Tiles.Count);
}

static void ImmediateDefeat()
{
    var battle = new Combat(Deck("network"), 4, new Board(),
        [new Spawn(Dummy(6), new Cell(2, 1)), new Spawn(Content.Enforcer, new Cell(4, 1))], 4);
    EndRound(battle);
    Equal(Phase.Defeat, battle.Phase);
    Equal(0, battle.OrganisationHp);
    Equal(0, battle.Enemies[1].RoutineIndex);
    Equal(0, battle.Enemies[1].Block, "Later enemy reinforcement must not resolve after defeat.");
    Check(!battle.BeginMovement().Ok, "Defeated battle must stay terminal.");
}

static void RunProgression()
{
    ExpansionChecks.RunProgression();
}

static void CompleteRun()
{
    ExpansionChecks.Campaign();
}
