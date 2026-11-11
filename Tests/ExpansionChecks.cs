using FutureDark.Rules;

internal static class ExpansionChecks
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
    private static void Ok(CommandResult result) => Check(result.Ok, result.Message);
    private static Card[] Deck(params string[] ids) => ids.Select((id, index) => new Card(index + 1, Content.Card(id))).ToArray();
    private static EnemyDef Rest(int hp = 100) => new("test", "Test", hp, [new(EnemyActionKind.Rest, 0, AttackPattern.Cross, "Wait")]);
    private static Combat Fight(Card[] cards, IEnumerable<Spawn>? spawns = null, string[]? supplies = null, string[]? relics = null) =>
        new(cards, 56, new Board(), spawns ?? [new Spawn(Rest(), new Cell(2, 1))], 42, relics, supplies);
    private static void Play(Combat battle, string id, Cell? target = null)
        => Ok(battle.Play(battle.Hand.First(key => battle.GetCard(key).Definition.Id == id), target));
    private static void End(Combat battle)
    {
        if (battle.Phase == Phase.Cards) Ok(battle.BeginMovement());
        if (battle.Phase == Phase.Movement) Ok(battle.ResolveTroops());
        if (battle.Phase == Phase.Discard) Ok(battle.ConfirmDiscard(battle.Hand.Take(battle.RequiredDiscards).ToArray()));
        battle.AssertConservation();
    }

    public static void CommittedMovement()
    {
        var walker = new EnemyDef("walk", "Walker", 100, [new(EnemyActionKind.Attack, 4, AttackPattern.Cross, "Fire")],
            Movement: EnemyMove.Step);
        var battle = Fight(Deck("runner", "barricade", "recon"), [new Spawn(walker, new Cell(3, 1))]);
        var enemy = battle.Enemies.Single();
        var plan = enemy.Plan;
        Check(plan.destination.Distance(enemy.Cell) == 1, "Enemy must make a piece-legal move.");
        Play(battle, "runner", new Cell(2, 4));
        Check(enemy.Plan == plan, "Deployment must not silently change committed enemy plans.");
        Play(battle, "barricade", plan.destination);
        Play(battle, "recon");
        Check(battle.GetIntent(enemy).MoveTo == plan.destination, "Recon must reveal the committed destination.");
        End(battle);
        Check(enemy.Cell == plan.spawnCell, "A denied destination must hold, not retarget.");
        Check(battle.OrganisationHp == 52, "A blocked move cannot remove organisation attack damage.");
    }

    public static void EnemyCombos()
    {
        var battle = Fight(Deck("pulse", "pulse", "jam"),
            [new Spawn(Content.Enforcer, new Cell(3, 1)), new Spawn(Rest(), new Cell(4, 1))]);
        var target = battle.Enemies[1];
        Play(battle, "pulse", target.Cell);
        Check(target.Hp == 96, "Formation must reduce a six-damage hit to four.");
        Play(battle, "jam", battle.Enemies[0].Cell);
        Play(battle, "pulse", target.Cell);
        Check(target.Hp == 90, "Jamming the shield must immediately remove formation protection.");
        foreach (bool jam in new[] { false, true })
        {
            var sniper = Content.Sniper with { Movement = EnemyMove.Hold, Routine = [new(EnemyActionKind.Attack, 5, AttackPattern.Column, "Precision")] };
            var b = Fight(Deck("runner", "jam"), [new Spawn(Content.Spotter, new Cell(4, 1)), new Spawn(sniper, new Cell(2, 1))]);
            Play(b, "runner", new Cell(2, 4));
            b.Troops[0].MarkTurns = 2;
            if (jam) Play(b, "jam", b.Enemies[0].Cell);
            End(b);
            Check(b.OrganisationHp == (jam ? 51 : 48), "Spotter precision must add three only while its source is active.");
            Check(b.EnemyComboHits == (jam ? 0 : 2), "Enemy combo must produce positive-damage events for its actual recipients.");
        }
    }

    public static void Reinforcements()
    {
        var caller = Content.Reinforcer with { Movement = EnemyMove.Hold, Routine = [new(EnemyActionKind.Summon, 1, AttackPattern.Cross, "Call")] };
        foreach (bool blocked in new[] { false, true })
        {
            var battle = Fight(Deck("barricade"), [new Spawn(caller, new Cell(3, 1))]);
            if (blocked) Play(battle, "barricade", battle.Enemies[0].Plan.target);
            End(battle);
            Check(battle.Enemies.Count == (blocked ? 1 : 2), "Summoning must honour its fixed landing zone.");
            Check(battle.OrganisationHp == 56, "A new drone cannot act in its spawning phase.");
            if (!blocked) Check(battle.Enemies[1].RoutineIndex == 0, "New drone routine must not advance until it acts.");
        }

    }

    public static void CommandAndHazard()
    {
        var officer = Content.Commander with { Movement = EnemyMove.Hold };
        var shooter = new EnemyDef("shooter", "Shooter", 100, [new(EnemyActionKind.Attack, 4, AttackPattern.Column, "Fire")]);
        var battle = Fight(Deck("recon"), [new Spawn(officer, new Cell(3, 1)), new Spawn(shooter, new Cell(2, 1))]);
        Play(battle, "recon");
        Check(battle.GetIntent(battle.Enemies[1]).Power == 6,
            "Recon must forecast a command that will resolve before this attack.");
        End(battle);
        Check(battle.OrganisationHp == 50 && battle.EnemyComboHits == 1, "Commander combo must match its forecast.");

        var hazard = Content.Controller with { Movement = EnemyMove.Hold, Routine = [new(EnemyActionKind.Hazard, 3, AttackPattern.Column, "Hazard")] };
        var pusher = Content.Pusher with { Movement = EnemyMove.Hold };
        var b = Fight(Deck("runner"), [new Spawn(hazard, new Cell(4, 1)), new Spawn(pusher, new Cell(2, 3))]);
        Play(b, "runner", new Cell(2, 4));
        b.Enemies[0].Plan = new EnemyPlan(new Cell(4, 1), new Cell(4, 1), new Cell(2, 5), new Cell(2, 5));
        b.Enemies[1].Plan = new EnemyPlan(new Cell(2, 3), new Cell(2, 3), new Cell(2, 4), new Cell(2, 5));
        End(b);
        Check(b.OrganisationHp == 52, "Only the push attack, not hazard placement, damages the organisation.");
        Check(b.Troops.Single().Cell == new Cell(2, 5) && b.Troops[0].Hp == 4,
            "Pushing onto a live hazard applies independent attack and hazard damage once.");
        Check(b.Troops[0].MovesLeft == 0 && b.EnemyComboHits == 1, "Hazard must root for the next troop turn and emit containment.");
    }

    public static void ActionCombos()
    {
        var battle = Fight(Deck("mark", "corrode", "detonate"));
        var enemy = battle.Enemies.Single();
        Play(battle, "mark", enemy.Cell);
        Play(battle, "corrode", enemy.Cell);
        Play(battle, "detonate", enemy.Cell);
        Check(enemy.Hp == 87 && enemy.Corrosion == 0 && enemy.Mark == 1, "Detonate should deal 4+6+3 and consume corrosion plus one mark.");
        Check(battle.Impacts.Single().Combos.SequenceEqual(new[] { "MELTDOWN", "BREACH" }), "Different action combos must stack.");

        var trap = Fight(Deck("trap", "pull"), [new Spawn(Rest(), new Cell(2, 2))]);
        Play(trap, "trap", new Cell(2, 3));
        Play(trap, "pull", new Cell(2, 2));
        Check(trap.Enemies[0].Cell == new Cell(2, 3) && trap.Enemies[0].Hp == 89, "Pull must trigger the trap exactly once.");
        Check(trap.ComboHits == 1 && trap.Tiles.Count == 0, "Trap must be consumed and emit Dead End.");

        var mine = Fight(Deck("mine", "pull"),
            [new Spawn(Rest(), new Cell(2, 2)), new Spawn(Rest(), new Cell(3, 3))]);
        Play(mine, "mine", new Cell(2, 3));
        Play(mine, "pull", new Cell(2, 2));
        Check(mine.Enemies[0].Hp == 87 && mine.Enemies[1].Hp == 96,
            "A mine deals its full hit plus half splash, once per recipient.");
        Check(mine.Enemies.All(e => e.Rooted == 0), "Only a snare roots; mines must follow their damage-only card text.");

        var preview = Fight(Deck("marksman", "mark", "recon"));
        Play(preview, "marksman", new Cell(2, 4));
        Play(preview, "mark", new Cell(2, 1));
        Play(preview, "recon");
        int damage = preview.PreviewAttack(preview.Troops[0], preview.Enemies[0]).Damage;
        Ok(preview.BeginMovement());
        Ok(preview.ResolveTroops());
        Check(preview.Impacts.Single().Damage == damage && damage == 10, "Marked troop preview and actual damage must agree without doubling the mark.");
    }

    public static void Supplies()
    {
        var b = Fight(Deck("amplify", "recover-supply", "supply-use"), supplies: ["charge", "shield", "stim"]);
        Check(!b.Play(b.Hand.First(id => b.GetCard(id).Definition.Id == "recover-supply")).Ok, "Cannot recover a supply without using it.");
        Play(b, "amplify");
        Ok(b.UseSupply(0, new Cell(2, 1)));
        Check(b.Enemies[0].Hp == 88 && b.Impacts[0].Combos.Contains("HOTWIRED"), "Amplified supply should connect for 12.");
        Check(!b.UseSupply(0).Ok, "Normal supply limit must be enforced.");
        Play(b, "recover-supply");
        Check(b.Supplies.Count == 3 && b.Supplies[2] == "charge", "Recovery returns one consumed supply.");
        Play(b, "supply-use");
        Ok(b.UseSupply(0));
        Check(b.OrganisationBlock == 13, "Shield plus recovery block must remain independent.");
        b.AssertConservation();
        Check(b.Zones.Values.All(z => z is Zone.Exhausted), "Logistics effects must exhaust rather than redraw into an infinite loop.");
    }

    public static void CardPool()
    {
        foreach (var definition in Content.Cards)
        foreach (bool upgrade in new[] { false, true })
        {
            Card[] cards = [new(1, Content.Card("runner")), new(2, definition, upgrade)];
            var battle = Fight(cards, supplies: ["charge"], relics: ["capacitor"]);
            Ok(battle.Play(1, new Cell(2, 4)));
            battle.Troops[0].Hp = 5;
            if (definition.Effect == EffectKind.RecoverSupply) Ok(battle.UseSupply(0, new Cell(2, 1)));
            Cell? target = definition.Target == TargetKind.None ? null : battle.CardTargets(2).OrderBy(c => c.Y).ThenBy(c => c.X).Select(c => (Cell?)c).First();
            Ok(battle.Play(2, target));
            Check(!Content.RulesText(new Card(2, definition, upgrade)).Contains("{p}"), "Rules must expose the actual upgraded numeric value.");
            battle.AssertConservation();
        }
    }

    public static RunState AtRoom(int seed, RoomKind kind, int act = 1)
    {
        var initial = new RunState(seed);
        Ok(initial.StartAct(1));
        var nodes = RunState.GenerateAct(seed, act);
        var room = nodes.First(n => n.Kind == kind);
        var snapshot = initial.SnapshotForSave() with
        {
            Act = act, Available = [room.Id], Current = null, Completed = [], ReplayNode = null,
            Funds = 350, CompletedActs = act == 1 ? [] : [1]
        };
        var run = RunState.Restore(snapshot);
        Ok(run.Enter(room.Id));
        return run;
    }

    public static void Economy()
    {
        var run = AtRoom(8, RoomKind.Vendor);
        int funds = run.Funds, count = run.Deck.Count;
        Ok(run.Purchase(0));
        Check(run.Funds == funds - run.Shop[0].Price && run.Deck.Count == count + 1, "Purchasing must charge once and add a unique instance.");
        Check(!run.Purchase(0).Ok, "A bought offer cannot be repurchased.");
        int firstId = run.Deck[0].Id;
        int price = run.RemovalPrice;
        Ok(run.RemoveCard(firstId));
        Check(run.Deck.All(c => c.Id != firstId) && run.RemovalPrice == price + 15, "Paid removal must remove only its instance and increase its price.");
        int relicIndex = run.Shop.ToList().FindIndex(o => o.Kind == "relic");
        Ok(run.Purchase(relicIndex));
        Check(run.Relics.Count == 1, "Purchased relic must be equipped for this run.");
        int supplyIndex = run.Shop.ToList().FindIndex(o => o.Kind == "supply");
        Ok(run.Purchase(supplyIndex));
        Ok(run.Purchase(supplyIndex + 1));
        int before = run.Funds;
        Check(!run.Purchase(supplyIndex + 2).Ok && run.Funds == before, "Full inventory must reject before spending funds.");
        Ok(run.SellSupply(0));
        Check(run.Supplies.Count == 2 && run.Funds > before, "Only the sold supply should disappear.");
        Ok(run.LeaveVendor());
        Check(!run.Purchase(1).Ok, "Cannot buy after leaving the vendor.");
        var a = RunState.GenerateAct(8, 1);
        var b = RunState.GenerateAct(8, 1);
        Check(a.Select(n => $"{n.Id}:{n.Kind}:{string.Join(',', n.Next)}").SequenceEqual(b.Select(n => $"{n.Id}:{n.Kind}:{string.Join(',', n.Next)}")), "Act generation must be deterministic.");
        Check(a.Select(n => n.Column).Distinct().Count() == 9 && a.Any(n => n.Kind == RoomKind.Vendor), "Each full route has nine rooms and a vendor.");
        Check(a.SelectMany(n => n.Next).All(id => a.Any(n => n.Id == id)), "Every route edge must have a destination.");
    }

    public static void RunProgression()
    {
        var run = new RunState(4);
        Check(!run.StartAct(3).Ok && !run.StartAct(2).Ok, "Centre and later act must start locked.");
        Ok(run.StartAct(1));
        Check(!run.Enter(run.Nodes.Last().Id).Ok, "Cannot skip straight to a boss.");
        Ok(run.Enter("entry"));
        AutoPlayer.WinBattle(run.Battle!);
        Check(run.Battle!.Phase == Phase.Victory, "Opening fight should be completable with normal commands.");
        Ok(run.FinishBattle());
        int funds = run.Funds;
        Check(!run.FinishBattle().Ok && run.Funds == funds, "Cannot duplicate battle funds.");
        Ok(run.ClaimReward(run.Rewards[0].Id));
        Check(!run.ClaimReward(null).Ok, "Cannot claim a reward twice.");

        var boundary = new RunState(9);
        Ok(boundary.StartAct(1));
        var snapshot = boundary.SnapshotForSave();
        var one = RunState.Restore(snapshot with { Screen = RunScreen.City, CompletedActs = [1] });
        Check(!one.CentreUnlocked && !one.StartAct(3).Ok, "One of two acts does not reach two-thirds.");
        var two = RunState.Restore(snapshot with { Screen = RunScreen.City, CompletedActs = [1, 2] });
        Check(two.CentreUnlocked, "Both acts must unlock the centre.");
        Ok(two.StartAct(3));
        Ok(two.Enter("finale"));
        Check(two.Battle!.Enemies.Any(e => e.Definition == Content.Architect), "Central finale must have its own boss encounter.");
    }

    public static void Saves()
    {
        string directory = Path.Combine(Path.GetTempPath(), "FutureDark-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new RunSaveStore(directory);
            var run = AtRoom(8, RoomKind.Vendor);
            var other = new RunState(8);
            store.Save(run);
            store.Save(other);
            Ok(run.Purchase(0));
            store.Save(run);
            Check(store.List().Count == 2, "Saving the same run must overwrite its slot, not create duplicates.");
            var loaded = store.Load(run.RunId);
            Check(loaded.RunId == run.RunId && loaded.Funds == 350 && loaded.Purchased.Count == 0,
                "Loading must restore the room entrance, including funds and stock.");
            Check(loaded.Shop.SequenceEqual(run.Shop), "Reload cannot reroll vendor stock.");
            Check(loaded.Deck.Count == run.Deck.Count - 1, "Mid-room purchases must be rolled back together with their costs.");
            store.Delete(run.RunId);
            Check(store.List().Count == 1 && store.Load(other.RunId).RunId == other.RunId, "Defeat deletion must affect only that run's slot.");

            var fight = new RunState(4);
            Ok(fight.StartAct(1));
            Ok(fight.Enter("entry"));
            int[] initialHand = fight.Battle!.Hand.ToArray();
            End(fight.Battle);
            store.Save(fight);
            var restarted = store.Load(fight.RunId);
            Check(restarted.Battle!.Round == 1 && restarted.Hp == RunState.MaxHp &&
                restarted.Battle.Hand.SequenceEqual(initialHand), "Combat saves must restore the exact seeded opening, not current turn state.");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    public static void Campaign()
    {
        int wins = 0;
        for (int seed = 1; seed <= 6; seed++)
        {
            var run = new RunState(seed);
            int transitions = 0;
            while (run.Screen is not (RunScreen.Complete or RunScreen.Defeat))
            {
                Check(++transitions <= 150, "Campaign must not get stuck on a room transition.");
                switch (run.Screen)
                {
                    case RunScreen.City:
                        Ok(run.StartAct(run.CentreUnlocked ? 3 : run.CompletedActs.Count + 1));
                        break;
                    case RunScreen.Map:
                        string next = run.Available.OrderBy(id => run.Nodes.Single(n => n.Id == id).Kind == RoomKind.Elite ? 1 : 0).First();
                        Ok(run.Enter(next));
                        break;
                    case RunScreen.Combat:
                        AutoPlayer.WinBattle(run.Battle!);
                        Ok(run.FinishBattle());
                        break;
                    case RunScreen.Reward:
                        var reward = run.Rewards.FirstOrDefault(c => c.Id is "pulse" or "siege" or "corrode" or "detonate" or "mark" or "battery" or "network" or "marksman");
                        Ok(run.ClaimReward(reward?.Id));
                        break;
                    case RunScreen.Event:
                        var choice = run.EventChoices.FirstOrDefault(c => c.Id == "heal") ?? run.EventChoices.FirstOrDefault(c => c.Id == "upgrade")
                            ?? run.EventChoices.FirstOrDefault(c => c.Id == "cash") ?? run.EventChoices.Last();
                        Ok(run.ResolveEvent(choice.Id, choice.Id == "upgrade" ? run.Deck.First(c => !c.Upgraded).Id : null));
                        break;
                    case RunScreen.Vendor:
                        for (int i = 0; i < run.Shop.Count; i++)
                        {
                            var offer = run.Shop[i];
                            if (run.Funds >= offer.Price && offer.Kind == "card" && offer.DefinitionId is "pulse" or "battery" or "siege" or "network")
                                Ok(run.Purchase(i));
                        }
                        Ok(run.LeaveVendor());
                        break;
                }
            }
            Console.WriteLine($"  city seed {seed}: {run.Screen}, act {run.Act}, HP {run.Hp}, wins {run.Victories}, player combos {run.TotalCombos}, enemy combos {run.TotalEnemyCombos}");
            if (run.Screen == RunScreen.Complete)
            {
                wins++;
                Check(run.CompletedActs.Count == 2 && run.Act == 3, "Completion requires two acts and the central boss.");
            }
        }
        Check(wins > 0, "At least one seeded full campaign must be completable by the fixed tactical policy.");
    }
}
