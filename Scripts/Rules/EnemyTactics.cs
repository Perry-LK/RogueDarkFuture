namespace FutureDark.Rules;

public sealed partial class Combat
{
    private static bool IsAttack(EnemyActionKind kind) => kind is EnemyActionKind.Attack or EnemyActionKind.Push;

    public HashSet<Cell> EnemyLegalMoves(Enemy enemy) => Board.Cells.Where(cell =>
    {
        int dx = Math.Abs(cell.X - enemy.Cell.X), dy = Math.Abs(cell.Y - enemy.Cell.Y);
        bool valid = enemy.Definition.Movement switch
        {
            EnemyMove.Step => dx + dy == 1,
            EnemyMove.Knight => dx * dy == 2,
            EnemyMove.Diagonal => dx == 1 && dy == 1,
            _ => false
        };
        return valid && CanEnter(cell) && Board.CanTraverse(enemy.Cell, cell, enemy.Definition.Movement == EnemyMove.Knight);
    }).ToHashSet();

    private void PlanEnemies()
    {
        var reserved = new HashSet<Cell>();
        foreach (var enemy in _enemies)
        {
            Cell target = _troops.OrderBy(t => t.Cell.Distance(enemy.Cell)).ThenBy(t => t.Card.Id).FirstOrDefault()?.Cell
                ?? new Cell(Math.Clamp(enemy.Cell.X, 1, Board.Width - 2), Board.Height - 2);
            float Score(Cell cell)
            {
                float score = -cell.Distance(target);
                if (enemy.Definition.Role == EnemyRole.Sniper)
                    score = -Math.Abs(cell.X - target.X) * 4 - Math.Abs(cell.Distance(target) - 3);
                else if (enemy.Definition.Role is EnemyRole.Shield or EnemyRole.Commander)
                    score += _enemies.Count(e => e != enemy && e.Cell.Distance(cell) <= 1) * 1.4f;
                score += _troops.Count(t => PatternCells(enemy.Step.Pattern, cell).Contains(t.Cell)) * 3;
                if (Board.Hazards.ContainsKey(cell)) score -= 6;
                if (cell == enemy.Cell) score += .1f;
                return score;
            }
            Cell destination = enemy.Jammed > 0 || enemy.Rooted > 0 ? enemy.Cell :
                EnemyLegalMoves(enemy).Where(c => !reserved.Contains(c)).Append(enemy.Cell)
                    .OrderByDescending(Score).ThenBy(c => c.Y).ThenBy(c => c.X).First();
            reserved.Add(destination);
            if (enemy.Step.Kind == EnemyActionKind.Hazard)
                target = _troops.SelectMany(t => Board.Cells.Where(c => c.Distance(t.Cell) == 1))
                    .Where(CanEnter).OrderBy(c => c.Distance(destination)).ThenBy(c => c.Y).ThenBy(c => c.X)
                    .Select(c => (Cell?)c).FirstOrDefault() ?? target;
            if (enemy.Step.Kind == EnemyActionKind.Summon)
                target = Board.Cells.Where(CanEnter).Where(c => c.Distance(destination) == 1)
                    .OrderBy(c => c.Y).ThenBy(c => c.X).Select(c => (Cell?)c).FirstOrDefault() ?? destination;
            Cell push = _tiles.Where(t => t.Kind == TileKind.EnemyHazard && t.Cell.Distance(target) == 1)
                .OrderBy(t => t.Cell.Y).ThenBy(t => t.Cell.X).Select(t => (Cell?)t.Cell).FirstOrDefault()
                ?? target with { Y = target.Y + 1 };
            enemy.Plan = new EnemyPlan(enemy.Cell, destination, target, push);
        }
    }

    private bool HasCommand(Enemy enemy) => _enemies.Any(e => e != enemy && e.Definition.Role == EnemyRole.Commander &&
        e.CommandTurns > 0 && e.Jammed == 0 && e.Cell.Distance(enemy.Cell) <= 2);
    private bool HasPrecision(Enemy enemy) => enemy.Definition.Role == EnemyRole.Sniper &&
        _enemies.Any(e => e.Definition.Role == EnemyRole.Spotter && e.Jammed == 0) &&
        _troops.Any(t => t.MarkTurns > 0 && EnemyAttackCells(enemy).Contains(t.Cell));
    private int EnemyPower(Enemy enemy) => enemy.Step.Power + (HasCommand(enemy) ? 2 : 0) + (HasPrecision(enemy) ? 3 : 0);

    private Cell ForecastPosition(Enemy enemy) => enemy.Jammed == 0 && enemy.Rooted == 0 &&
        enemy.Cell == enemy.Plan.spawnCell && CanEnter(enemy.Plan.destination) ? enemy.Plan.destination : enemy.Cell;

    private int PlannedEnemyPower(Enemy enemy)
    {
        int index = _enemies.IndexOf(enemy);
        bool command = _enemies.Any(source => source != enemy && source.Definition.Role == EnemyRole.Commander &&
            source.Jammed == 0 && ForecastPosition(source).Distance(ForecastPosition(enemy)) <= 2 &&
            (source.CommandTurns > 0 || source.Step.Kind == EnemyActionKind.Command && _enemies.IndexOf(source) < index));
        var cells = PatternCells(enemy.Step.Pattern, enemy.Plan.destination)
            .Intersect(PatternCells(enemy.Step.Pattern, ForecastPosition(enemy))).ToHashSet();
        bool precision = enemy.Definition.Role == EnemyRole.Sniper &&
            _enemies.Any(source => source.Definition.Role == EnemyRole.Spotter && source.Jammed == 0) &&
            _troops.Any(t => cells.Contains(t.Cell) && (t.MarkTurns > 0 ||
                _enemies.Any(source => source.Definition.Role == EnemyRole.Spotter && source.Jammed == 0 &&
                    source.Step.Kind == EnemyActionKind.Mark && source.Plan.target == t.Cell && _enemies.IndexOf(source) < index)));
        return enemy.Step.Power + (command ? 2 : 0) + (precision ? 3 : 0);
    }

    private void ResolveEnemies()
    {
        _impacts.Clear();
        TickEnvironment();
        foreach (var enemy in _enemies.Where(e => e.Corrosion > 0).ToArray())
        {
            int corrosion = enemy.Corrosion;
            enemy.Corrosion--;
            HitEnemy(enemy, corrosion, enemy.Cell, direct: false);
        }
        CheckVictory();
        if (Phase == Phase.Victory) return;

        var actors = _enemies.ToArray();
        foreach (var enemy in actors)
        {
            if (!_enemies.Contains(enemy) || enemy.Jammed > 0 || enemy.Rooted > 0) continue;
            if (enemy.Cell == enemy.Plan.spawnCell && enemy.Plan.destination != enemy.Cell)
            {
                if (CanEnter(enemy.Plan.destination))
                {
                    enemy.Cell = enemy.Plan.destination;
                    TriggerEnemyTile(enemy);
                }
                else AddLog($"{enemy.Definition.Name}'s committed move was blocked.");
            }
        }
        CheckVictory();
        if (Phase == Phase.Victory) return;

        foreach (var enemy in actors)
        {
            if (!_enemies.Contains(enemy)) continue;
            if (enemy.Jammed > 0)
            {
                AdvanceEnemy(enemy);
                AddLog($"{enemy.Definition.Name}'s plan was jammed.");
                continue;
            }
            switch (enemy.Step.Kind)
            {
                case EnemyActionKind.Attack:
                case EnemyActionKind.Push:
                    ResolveEnemyAttack(enemy);
                    if (Phase == Phase.Defeat) { AdvanceEnemy(enemy); return; }
                    break;
                case EnemyActionKind.Reinforce:
                    enemy.Block += enemy.Step.Power;
                    AddLog($"{enemy.Definition.Name} reinforces.");
                    break;
                case EnemyActionKind.Mark:
                    var marked = _troops.FirstOrDefault(t => t.Cell == enemy.Plan.target);
                    if (marked is not null) { marked.MarkTurns = 2; AddLog($"{marked.Card.Name} is marked for precision fire."); }
                    break;
                case EnemyActionKind.Command:
                    enemy.CommandTurns = 2;
                    AddLog($"{enemy.Definition.Name} arms nearby allies' coordinated attacks.");
                    break;
                case EnemyActionKind.Summon:
                    if (_enemies.Count < Board.EnemyCapacity && CanEnter(enemy.Plan.target))
                    {
                        int id = _enemies.Max(e => e.Id) + 1;
                        var spawned = new Enemy(id, new Spawn(Content.Drone, enemy.Plan.target));
                        spawned.Plan = new EnemyPlan(spawned.Cell, spawned.Cell, spawned.Cell, spawned.Cell);
                        _enemies.Add(spawned);
                        AddLog("Response drone deployed. It acts starting next round.");
                    }
                    else AddLog("Reinforcement blocked: landing zone occupied or squad at capacity.");
                    break;
                case EnemyActionKind.Hazard:
                    if (Board.Cells.Contains(enemy.Plan.target) && !Board.Terrain.Contains(enemy.Plan.target))
                        SetTile(new TileEffect(TileKind.EnemyHazard, enemy.Plan.target, 3, enemy.Step.Power));
                    AddLog($"{enemy.Definition.Name} energises its committed target tile.");
                    break;
                case EnemyActionKind.Lockdown:
                    foreach (var cell in PatternCells(enemy.Step.Pattern, enemy.Plan.destination).Where(CanEnter).ToArray())
                        SetTile(new TileEffect(TileKind.Lockdown, cell, 2));
                    break;
                case EnemyActionKind.Rest: AddLog($"{enemy.Definition.Name} rests."); break;
                default: throw new InvalidOperationException("Unknown enemy routine.");
            }
            AdvanceEnemy(enemy);
        }
        OrganisationBlock = 0;
        foreach (var troop in _troops) { troop.Block = 0; troop.MarkTurns = Math.Max(0, troop.MarkTurns - 1); }
        foreach (var enemy in _enemies)
        {
            enemy.Rooted = 0;
            enemy.Jammed = Math.Max(0, enemy.Jammed - 1);
            enemy.CommandTurns = Math.Max(0, enemy.CommandTurns - 1);
        }
        foreach (var tile in _tiles) tile.Duration--;
        _tiles.RemoveAll(t => t.Duration <= 0);
        IntelRounds = Math.Max(0, IntelRounds - 1);
        StartRound();
    }

    private static void AdvanceEnemy(Enemy enemy) => enemy.RoutineIndex = (enemy.RoutineIndex + 1) % enemy.Definition.Routine.Length;

    private void ResolveEnemyAttack(Enemy enemy)
    {
        int power = EnemyPower(enemy);
        var links = new List<string>();
        if (HasCommand(enemy)) links.Add("DIRECTIVE");
        if (HasPrecision(enemy)) links.Add("TARGET LOCK");
        var org = Damage(OrganisationHp, OrganisationBlock, power);
        OrganisationHp = org.Hp;
        OrganisationBlock = org.Block;
        string[] combos = org.Lost > 0 ? links.ToArray() : [];
        if (combos.Length > 0) EnemyComboHits++;
        _impacts.Add(new Impact(enemy.Cell, enemy.Cell, org.Lost, combos, true));
        AddLog($"{enemy.Definition.Name} / organisation loses {org.Lost} HP" +
            (combos.Length > 0 ? " / " + string.Join(" + ", combos) : "") + ".");
        if (OrganisationHp == 0) { Phase = Phase.Defeat; AddLog("Signal lost. This run is over."); return; }
        var covered = EnemyAttackCells(enemy);
        foreach (var cover in Board.Cover.Keys.Where(covered.Contains).ToArray())
            HitCover(cover, power + HeightBonus(enemy.Cell, cover, enemy.Step.Pattern != AttackPattern.Adjacent));
        foreach (var troop in _troops.Where(t => covered.Contains(t.Cell)).ToArray())
        {
            HitTroop(troop, power + HeightBonus(enemy.Cell, troop.Cell, enemy.Step.Pattern != AttackPattern.Adjacent), enemy.Cell, links.ToArray());
            if (enemy.Step.Kind == EnemyActionKind.Push && _troops.Contains(troop) &&
                troop.Cell == enemy.Plan.target && CanEnter(enemy.Plan.pushTo) && Board.CanTraverse(troop.Cell, enemy.Plan.pushTo))
            {
                troop.Cell = enemy.Plan.pushTo;
                TriggerFriendlyTile(troop);
            }
        }
    }
}
