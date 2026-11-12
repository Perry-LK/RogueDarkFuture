using FutureDark.Rules;

static class NewSystemsChecks
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    private static void Ok(CommandResult result) => Check(result.Ok, result.Message);
    private static Card[] Deck(params string[] ids) => ids.Select((id, i) => new Card(i + 1, Content.Card(id))).ToArray();
    private static void End(Combat battle)
    {
        Ok(battle.BeginMovement());
        Ok(battle.ResolveTroops());
        if (battle.Phase == Phase.Discard) Ok(battle.ConfirmDiscard(battle.Hand.Take(battle.RequiredDiscards)));
    }

    public static void Terrain()
    {
        for (int act = 1; act <= RunState.FinaleAct; act++)
        for (int seed = 0; seed < 24; seed++)
        {
            var board = Board.Generate(seed, act, 4);
            Check(board.Connected(), $"Disconnected terrain: act {act}, seed {seed}.");
            Check(board.Width is >= 7 and <= 9 && board.Height is >= 6 and <= 8, "Boards must fit the agreed size range.");
            Check(board.Walls.Count > 0 && board.Cover.Count > 0 && board.Hazards.Count > 0, "Layouts must contain real terrain systems.");
            Check(board.Deployment.All(board.Accessible), "Terrain must not block deployment.");
            if (act > 1) Check(board.Elevation.Count > 0 && board.Ramps.Count > 0, "Raised districts need connected ramps.");
        }
        var terrain = new Board();
        terrain.Terrain.Clear();
        var upper = new Cell(2, 3);
        var lower = new Cell(2, 4);
        terrain.Elevation[upper] = 1;
        Check(!terrain.CanTraverse(lower, upper), "Normal pieces cannot climb a ledge.");
        Check(terrain.CanTraverse(lower, upper, true), "Leap pieces can jump one level.");
        terrain.AddRamp(lower, upper);
        Check(terrain.CanTraverse(lower, upper) && terrain.CanTraverse(upper, lower), "Ramps work in both directions.");
        terrain.Cover[new Cell(2, 2)] = 6;
        var enemy = new EnemyDef("dummy", "Dummy", 100, [new(EnemyActionKind.Rest, 0, AttackPattern.Column, "Wait")]);
        var battle = new Combat(Deck("pulse", "marksman"), 56, terrain, [new Spawn(enemy, new Cell(2, 1))], 1);
        Ok(battle.Play(2, lower));
        Check(!battle.AttackCells(PieceKind.Marksman, lower).Contains(new Cell(2, 1)), "Cover must occlude troop fire.");
        Ok(battle.Play(1, new Cell(2, 2)));
        Check(!terrain.Cover.Any() && battle.AttackCells(PieceKind.Marksman, lower).Contains(new Cell(2, 1)), "Destroying cover opens the firing lane.");
        terrain.Walls.Add(new Cell(2, 2));
        Check(!battle.AttackCells(PieceKind.Marksman, lower).Contains(new Cell(2, 1)), "Solid walls still block fire.");
        Check(battle.IsObserved(battle.Enemies[0]) == false, "Terrain does not silently grant intel.");
        var closeBoard = new Board();
        closeBoard.Walls.Add(new Cell(2, 3));
        var close = new Combat(Deck("runner"), 56, closeBoard, [new Spawn(enemy, new Cell(2, 2))], 1);
        Ok(close.Play(1, lower));
        Check(close.IsObserved(close.Enemies[0]), "A wall must not hide proximity intel.");
        var coverBoard = new Board();
        coverBoard.Cover[new Cell(2, 3)] = 3;
        var shot = new Combat(Deck("marksman"), 56, coverBoard, [new Spawn(enemy, new Cell(2, 1))], 1);
        Ok(shot.Play(1, new Cell(2, 5)));
        End(shot);
        Check(!coverBoard.Cover.Any() && shot.Enemies[0].Hp == 100, "A shot that breaks cover cannot also pass through it.");
        End(shot);
        Check(shot.Enemies[0].Hp == 96, "The next shot must use the newly opened lane.");

        var cliffBoard = new Board();
        cliffBoard.Elevation[new Cell(2, 3)] = 1;
        var cliff = new Combat(Deck("push", "push"), 56, cliffBoard, [new Spawn(enemy, new Cell(2, 3))], 1);
        Ok(cliff.Play(1, new Cell(2, 3)));
        Check(cliff.Enemies[0].Cell == new Cell(2, 3), "Displacement cannot force an enemy over an unconnected ledge.");
        cliffBoard.AddRamp(new Cell(2, 3), new Cell(2, 2));
        Ok(cliff.Play(2, new Cell(2, 3)));
        Check(cliff.Enemies[0].Cell == new Cell(2, 2), "Displacement can follow a connecting ramp.");
    }

    public static void HeightDamage()
    {
        var board = new Board();
        board.Terrain.Clear();
        board.Elevation[new Cell(2, 1)] = 1;
        var shooter = new EnemyDef("height", "Height shooter", 100, [new(EnemyActionKind.Attack, 4, AttackPattern.Column, "Fire")]);
        var b = new Combat(Deck("runner"), 56, board, [new Spawn(shooter, new Cell(2, 1))], 1);
        Ok(b.Play(1, new Cell(2, 4)));
        End(b);
        Check(b.OrganisationHp == 52 && b.Troops.Single().Hp == 6, "Height boosts only ranged troop damage, never organisation damage.");
        var raised = new Board();
        raised.Elevation[new Cell(2, 4)] = 1;
        var a = new Combat(Deck("marksman"), 56, raised, [new Spawn(shooter, new Cell(2, 1))], 1);
        Ok(a.Play(1, new Cell(2, 4)));
        Check(a.PreviewAttack(a.Troops[0], a.Enemies[0]).Damage == 5, "Friendly ranged high ground must be previewed.");
        var melee = new Combat(Deck("breacher"), 56, raised, [new Spawn(shooter, new Cell(3, 3))], 1);
        Ok(melee.Play(1, new Cell(2, 4)));
        Check(melee.PreviewAttack(melee.Troops[0], melee.Enemies[0]).Damage == 4, "Diagonal melee must not gain the ranged height bonus.");
        var coverBoard = new Board();
        coverBoard.Cover[new Cell(2, 2)] = 2;
        var covered = new Combat(Deck("runner"), 56, coverBoard, [new Spawn(shooter, new Cell(2, 1))], 1);
        Ok(covered.Play(1, new Cell(2, 4)));
        End(covered);
        Check(covered.OrganisationHp == 52 && covered.Troops[0].Hp == 11 && !coverBoard.Cover.Any(),
            "Enemy fire must break cover without extending that shot; organisation damage remains independent.");
        var hazardBoard = new Board();
        hazardBoard.Hazards[new Cell(2, 4)] = 2;
        var hazard = new Combat(Deck("runner"), 56, hazardBoard,
            [new Spawn(shooter with { Routine = [new(EnemyActionKind.Rest, 0, AttackPattern.Column, "Wait")] }, new Cell(2, 1))], 1);
        Ok(hazard.Play(1, new Cell(2, 4)));
        End(hazard);
        Check(hazard.Troops[0].Hp == 7 && hazardBoard.Hazards.Count == 1, "Environmental hazards affect deployment, tick and remain after the phase.");
    }

    public static void ProgressAndSlots()
    {
        string folder = Path.Combine(Path.GetTempPath(), "FutureDark-progress-" + Guid.NewGuid().ToString("N"));
        try
        {
            Check(Content.StartingDeck().Count == 10 && Content.StartingDeck().Count(c => c.Definition.Id == "runner") == 2,
                "The basic faction starter must contain ten cards and two Runner anchors.");
            Check(Unlocks.Cards(0).Count < Content.Cards.Length && Unlocks.Cards(4).Select(c => c.Id).ToHashSet().SetEquals(Content.Cards.Select(c => c.Id)),
                "The final unlock tier must contain the complete card pool.");
            var store = new RunSaveStore(Path.Combine(folder, "runs"));
            var profileStore = new ProgressStore(Path.Combine(folder, "progress.json"));
            var runs = Enumerable.Range(1, 3).Select(seed => new RunState(seed)).ToArray();
            foreach (var run in runs) store.Save(run);
            bool rejected = false;
            try { store.Save(new RunState(4)); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && store.List().Count == 3, "The fourth slot must be rejected without replacing an existing run.");
            store.Save(runs[0]);
            Check(store.List()[0].RunId == runs[0].RunId, "Continue must select the latest played checkpoint.");
            var runWithHistory = RunState.Restore(new RunState(42).SnapshotForSave() with { Hp = 1, Victories = 10 });
            Ok(runWithHistory.StartAct(1));
            Ok(runWithHistory.Enter("entry"));
            while (runWithHistory.Battle!.Phase != Phase.Defeat) End(runWithHistory.Battle);
            var award = profileStore.Award(runWithHistory);
            Check(award.Earned == 80 && award.Profile.Tier == 1, "Defeat must unlock the earned pack.");
            Check(profileStore.Award(runWithHistory).Earned == 0 && profileStore.Load().Experience == 80,
                "Reloading/retrying a finished run cannot duplicate XP.");
            Check(store.Load(runs[0].RunId).UnlockTier == 0 && new RunState(99, award.Profile.Tier).UnlockTier == 1,
                "Existing runs keep their reward pool; only future runs gain the new tier.");
            store.Delete(runs[0].RunId);
            Check(store.List().Count == 2 && profileStore.Load().Experience == 80, "Deleting a run preserves other slots and unlocks.");
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    private static RunState EventAt(RunSnapshot snapshot, int act, int depth)
    {
        var node = RunState.GenerateAct(snapshot.Seed, act).First(n => n.Column == depth);
        return RunState.Restore(snapshot with
        {
            Act = act, Screen = RunScreen.Map, Available = [node.Id], Completed = [], Current = null,
            ReplayNode = node.Id, CompletedActs = Enumerable.Range(1, act - 1).ToArray()
        });
    }

    public static void Narrative()
    {
        var courier = EventAt(new RunState(7).SnapshotForSave(), 1, 2);
        Ok(courier.ResolveEvent("shelter"));
        var relay = EventAt(courier.SnapshotForSave(), 2, 2);
        Check(relay.EventChoices.Single(c => c.Id == "relay").Cost == 0, "Sheltered couriers must fund the next district's relay.");
        Ok(relay.ResolveEvent("relay"));
        var aftermath = EventAt(relay.SnapshotForSave(), 3, 2);
        Check(aftermath.EventChoices.Single(c => c.Id == "sabotage").Cost == 0, "A restored relay must unlock the later consequence.");
        Ok(aftermath.ResolveEvent("sabotage"));
        Check(aftermath.StoryFlags.Contains("sabotage"), "Sabotage must persist beyond the event.");
        var saved = aftermath.SnapshotForSave();
        var boss = RunState.GenerateAct(saved.Seed, 3).Single(n => n.IsBoss);
        var weakened = RunState.Restore(saved with { Available = [boss.Id], ReplayNode = boss.Id });
        var ordinary = RunState.Restore(saved with { Available = [boss.Id], ReplayNode = boss.Id, StoryFlags = [] });
        Check(ordinary.Battle!.Enemies[0].Hp - weakened.Battle!.Enemies[0].Hp == 10, "The story decision must affect the later boss.");
    }
}
