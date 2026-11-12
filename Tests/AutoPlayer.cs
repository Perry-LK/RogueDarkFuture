using FutureDark.Rules;

internal static class AutoPlayer
{
    private sealed record Choice(int Id, Cell? Target, float Score);

    public static void WinBattle(Combat battle)
    {
        while (battle.Phase is not (Phase.Victory or Phase.Defeat))
        {
            if (battle.Round > 60) throw new InvalidOperationException("Policy exceeded 60 combat rounds.");
            if (battle.Phase == Phase.Cards)
            {
                for (int slot = 0; slot < battle.Supplies.Count && battle.SupplyUses > 0; slot++)
                {
                    string supply = battle.Supplies[slot];
                    if (supply == "shield" && battle.RawThreat > battle.OrganisationBlock)
                    { Require(battle.UseSupply(slot)); break; }
                    if (supply == "charge")
                    { Require(battle.UseSupply(slot, battle.Enemies.OrderBy(e => e.Hp).First().Cell)); break; }
                }
                for (int guard = 0; guard < 20 && battle.Phase == Phase.Cards; guard++)
                {
                    var choice = Choices(battle).OrderByDescending(c => c.Score).FirstOrDefault(c => c.Score > 0);
                    if (choice is null) break;
                    Require(battle.Play(choice.Id, choice.Target));
                    battle.AssertConservation();
                }
                if (battle.Phase == Phase.Cards) Require(battle.BeginMovement());
            }
            if (battle.Phase == Phase.Movement)
            {
                foreach (var troop in battle.Troops.ToArray())
                {
                    var path = BestPath(battle, troop);
                    foreach (var cell in path)
                    {
                        if (!battle.Troops.Contains(troop) || troop.MovesLeft == 0) break;
                        Require(battle.Move(troop.Card.Id, cell));
                    }
                    if (battle.Troops.Contains(troop)) Require(battle.Hold(troop.Card.Id));
                }
                Require(battle.ResolveTroops());
            }
            if (battle.Phase == Phase.Discard)
            {
                int[] discards = battle.Hand.OrderBy(id => KeepValue(battle, battle.GetCard(id)))
                    .Take(battle.RequiredDiscards).ToArray();
                Require(battle.ConfirmDiscard(discards));
            }
            battle.AssertConservation();
        }
    }

    private static void Require(CommandResult result)
    {
        if (!result.Ok) throw new InvalidOperationException(result.Message);
    }

    private static IEnumerable<Choice> Choices(Combat battle)
    {
        foreach (int id in battle.Hand)
        {
            var card = battle.GetCard(id);
            if (card.Definition.Cost > battle.Ap) continue;
            IEnumerable<Cell?> targets = card.Definition.Target == TargetKind.None
                ? new Cell?[] { null } : battle.CardTargets(id).Select(cell => (Cell?)cell);
            foreach (var target in targets)
            {
                var troop = battle.Troops.FirstOrDefault(t => t.Cell == target);
                var targetEnemy = battle.Enemies.FirstOrDefault(e => e.Cell == target);
                int uncovered = Math.Max(0, battle.RawThreat - battle.OrganisationBlock);
                float score = card.Definition.Effect switch
                {
                    EffectKind.Deploy => DeployScore(battle, card, target!.Value),
                    EffectKind.Recon => battle.IntelRounds == 0 ? 70 : -1,
                    EffectKind.Network => uncovered > 0 ? 12 + Math.Min(uncovered, 7) + (battle.OrganisationHp <= 16 ? 30 : 0) : -1,
                    EffectKind.Intercept => uncovered > 0 ? 13 + Math.Min(uncovered, 4) + Incoming(battle, troop!) : Incoming(battle, troop!) > 0 ? 8 : -1,
                    EffectKind.Armour => Incoming(battle, troop!) > troop!.Block ? (Incoming(battle, troop) >= troop.Hp + troop.Block ? 35 : 9) : -1,
                    EffectKind.Strike => PotentialTargets(battle).Count > 0 ? 13 + PotentialTargets(battle).Count * 4 : -1,
                    EffectKind.Exposed => !battle.Tiles.Any(t => t.Kind == TileKind.Exposed && t.Cell == target) &&
                        PotentialTargets(battle).Any(e => e.Cell == target) ? 14 + (battle.StrikeBonus > 0 ? 12 : 0) : -1,
                    EffectKind.March => !troop!.Marched && troop.Kind == PieceKind.Runner ? 22 : -1,
                    EffectKind.Pulse or EffectKind.Execute or EffectKind.Siege or EffectKind.Purge => targetEnemy is null ? 2 : 30 + (targetEnemy.Hp < 10 ? 20 : 0),
                    EffectKind.Mark => battle.Enemies.First(e => e.Cell == target).Mark == 0 ? 80 : -1,
                    EffectKind.Corrode => 36,
                    EffectKind.Detonate => 23 + battle.Enemies.First(e => e.Cell == target).Corrosion * 7,
                    EffectKind.Jam => uncovered > 5 && battle.Enemies.First(e => e.Cell == target).Jammed == 0 ? 45 : -1,
                    EffectKind.Push or EffectKind.Pull => 25,
                    EffectKind.Volley => PotentialTargets(battle).Count > 0 ? 29 : -1,
                    EffectKind.Arc => 32,
                    EffectKind.Draw or EffectKind.Battery => 85,
                    EffectKind.Fortify => uncovered > 5 ? 35 : -1,
                    EffectKind.Smoke => uncovered > 0 ? 20 : -1,
                    EffectKind.Overwatch => PotentialTargets(battle).Count > 0 ? 24 : -1,
                    EffectKind.Repair => troop!.Hp <= troop.MaxHp - 5 ? 22 : -1,
                    EffectKind.Advance => battle.Troops.Any(t => t.Kind == PieceKind.Runner) ? 20 : -1,
                    EffectKind.Rally => battle.Troops.Count >= 2 ? 18 : -1,
                    _ => -1
                };
                yield return new Choice(id, target, score);
            }
        }
    }

    private static float DeployScore(Combat battle, Card card, Cell cell)
    {
        if (battle.Troops.Count >= 4) return -1;
        var troop = new Troop(card, cell);
        float reach = PositionScore(battle, troop, cell, 0);
        foreach (var candidate in Neighbours(battle, troop, cell))
            reach = Math.Max(reach, PositionScore(battle, troop, candidate, 1));
        return (battle.Troops.Count == 0 ? 100 : battle.Troops.Count == 1 ? 27 : 13) +
            reach / 2 + (troop.Kind == PieceKind.Marksman ? 4 : 0);
    }

    private static List<Enemy> PotentialTargets(Combat battle) => battle.Enemies.Where(enemy =>
        battle.Troops.Any(troop => battle.AttackCells(troop.Kind, troop.Cell).Contains(enemy.Cell) ||
            Neighbours(battle, troop, troop.Cell).Any(c => battle.AttackCells(troop.Kind, c).Contains(enemy.Cell)))).ToList();

    private static int Incoming(Combat battle, Troop troop) => battle.Enemies.Where(e =>
        e.Step.Kind == EnemyActionKind.Attack && battle.EnemyAttackCells(e).Contains(troop.Cell)).Sum(e => e.Step.Power);

    private static List<Cell> BestPath(Combat battle, Troop troop)
    {
        var distances = new Dictionary<Cell, int>();
        var queue = new Queue<Cell>();
        foreach (var cell in battle.Board.Cells.Where(c => battle.Board.Accessible(c) &&
            battle.Enemies.Any(e => battle.AttackCells(troop.Kind, c).Contains(e.Cell))))
        { distances[cell] = 0; queue.Enqueue(cell); }
        while (queue.TryDequeue(out var cell))
            foreach (var next in Neighbours(battle, troop, cell))
                if (distances.TryAdd(next, distances[cell] + 1)) queue.Enqueue(next);
        var best = new List<Cell>();
        float bestScore = PositionScore(battle, troop, troop.Cell, troop.MovesMade) - distances.GetValueOrDefault(troop.Cell, 20) * 5;
        void Visit(Cell position, List<Cell> path, int remaining)
        {
            float score = PositionScore(battle, troop, position, troop.MovesMade + path.Count) - path.Count * .015f - distances.GetValueOrDefault(position, 20) * 5;
            if (score > bestScore) { bestScore = score; best = path.ToList(); }
            if (remaining == 0) return;
            foreach (var next in Neighbours(battle, troop, position))
            {
                path.Add(next);
                Visit(next, path, remaining - 1);
                path.RemoveAt(path.Count - 1);
            }
        }
        Visit(troop.Cell, new List<Cell>(), Math.Min(3, troop.MovesLeft));
        return best;
    }

    private static IEnumerable<Cell> Neighbours(Combat battle, Troop troop, Cell from) => battle.Board.Cells.Where(cell =>
    {
        int dx = Math.Abs(cell.X - from.X), dy = Math.Abs(cell.Y - from.Y);
        bool matches = troop.Kind switch { PieceKind.Breacher => dx * dy == 2, PieceKind.Sapper => dx == 1 && dy == 1,
            PieceKind.Medic => Math.Max(dx, dy) == 1, _ => dx + dy == 1 };
        return matches && battle.Board.CanTraverse(from, cell, troop.Kind == PieceKind.Breacher) && !battle.IsDenied(cell) && !battle.Enemies.Any(e => e.Cell == cell) &&
            !battle.Troops.Any(t => t != troop && t.Cell == cell);
    });

    private static float PositionScore(Combat battle, Troop troop, Cell cell, int moves)
    {
        var cells = battle.AttackCells(troop.Kind, cell);
        float score = 0;
        foreach (var enemy in battle.Enemies.Where(e => cells.Contains(e.Cell)))
        {
            int damage = battle.PreviewAttack(troop, enemy).Damage;
            if (troop.Kind == PieceKind.Runner && troop.Marched && moves >= 2 && troop.MovesMade < 2) damage += 3;
            score += Math.Min(enemy.Hp + enemy.Block, damage) * 3 + 4;
            if (damage >= enemy.Hp + enemy.Block) score += 18;
        }
        int distance = battle.Enemies.Min(enemy => troop.Kind switch
        {
            PieceKind.Marksman => Math.Abs(cell.X - enemy.Cell.X) + (cell.Y <= enemy.Cell.Y ? enemy.Cell.Y - cell.Y + 1 : Math.Max(0, cell.Y - enemy.Cell.Y - 4)),
            PieceKind.Runner => Math.Max(0, cell.Distance(enemy.Cell) - 1),
            _ => Math.Max(0, Math.Max(Math.Abs(cell.X - enemy.Cell.X), Math.Abs(cell.Y - enemy.Cell.Y)) - 1)
        });
        score -= distance * 2.5f;
        if (battle.Tiles.Any(t => t.Cell == cell && t.Kind == TileKind.EnemyHazard)) score -= 25;
        if (battle.Board.Hazards.ContainsKey(cell)) score -= 16;
        int incoming = battle.Enemies.Where(e => e.Step.Kind == EnemyActionKind.Attack &&
            battle.EnemyAttackCells(e).Contains(cell)).Sum(e => e.Step.Power);
        if (incoming >= troop.Hp + troop.Block) score -= 12;
        return score;
    }

    private static int KeepValue(Combat battle, Card card) => card.Definition.Effect switch
    {
        EffectKind.Deploy => battle.Troops.Count < 2 ? 80 : 18,
        EffectKind.Recon => battle.Troops.Any(t => t.Kind == PieceKind.Marksman) ? 75 : 25,
        EffectKind.Network => 60,
        EffectKind.Strike or EffectKind.Exposed => 55,
        EffectKind.March => battle.Troops.Any(t => t.Kind == PieceKind.Runner) ? 50 : 10,
        EffectKind.Intercept => 40,
        EffectKind.Armour => 30,
        _ => 0
    };
}
