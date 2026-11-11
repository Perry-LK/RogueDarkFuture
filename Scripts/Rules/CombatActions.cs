namespace FutureDark.Rules;

public sealed partial class Combat
{
    private bool HasRelic(string id) => _relics.Contains(id);
    public static int CardPower(Card card) => card.Definition.Power + (card.Upgraded ? card.Definition.UpgradePower : 0);
    public int FormationArmour(Enemy enemy) => _enemies.Any(e => e != enemy &&
        e.Definition.Role == EnemyRole.Shield && e.Jammed == 0 && e.Cell.Distance(enemy.Cell) <= 1) ? 2 : 0;

    private void HitEnemy(Enemy enemy, int amount, Cell from, IEnumerable<string>? combos = null, bool direct = true, bool markIncluded = false)
    {
        if (!_enemies.Contains(enemy)) return;
        var links = (combos ?? []).ToList();
        if (direct && enemy.Mark > 0)
        {
            if (!markIncluded) amount += HasRelic("prism") ? 5 : 3;
            enemy.Mark--;
            links.Add("BREACH");
        }
        var result = direct ? Damage(enemy.Hp, enemy.Block, Math.Max(0, amount - FormationArmour(enemy))) :
            new DamageResult(Math.Max(0, enemy.Hp - amount), enemy.Block, Math.Min(enemy.Hp, amount));
        enemy.Hp = result.Hp;
        enemy.Block = result.Block;
        string[] actual = result.Lost > 0 ? links.Distinct().ToArray() : [];
        if (actual.Length > 0) ComboHits++;
        _impacts.Add(new Impact(from, enemy.Cell, result.Lost, actual));
        AddLog($"{enemy.Definition.Name} loses {result.Lost} HP" + (actual.Length > 0 ? " / " + string.Join(" + ", actual) : "") + ".");
        if (enemy.Hp == 0) _enemies.Remove(enemy);
    }

    private void TroopAttack(Troop troop, int bonus = 0)
    {
        foreach (var enemy in _enemies.Where(e => AttackCells(troop.Kind, troop.Cell).Contains(e.Cell)).ToArray())
        {
            var hit = PreviewAttack(troop, enemy);
            HitEnemy(enemy, hit.Damage + bonus, troop.Cell, hit.Combos, markIncluded: true);
        }
    }

    private void CheckVictory()
    {
        if (_enemies.Count != 0 || Phase == Phase.Defeat) return;
        Phase = Phase.Victory;
        AddLog("Sector secured. Collect funds and choose a reward.");
    }

    private void ResolveAction(Card card, Cell? target)
    {
        int p = CardPower(card);
        var enemy = target.HasValue ? _enemies.FirstOrDefault(e => e.Cell == target.Value) : null;
        var troop = target.HasValue ? _troops.FirstOrDefault(t => t.Cell == target.Value) : null;
        Cell origin = _troops.FirstOrDefault()?.Cell ?? new Cell(3, 5);
        switch (card.Definition.Effect)
        {
            case EffectKind.Pulse: HitEnemy(enemy!, p, origin); break;
            case EffectKind.Mark: enemy!.Mark += p; break;
            case EffectKind.Jam: enemy!.Jammed = Math.Max(enemy.Jammed, p); break;
            case EffectKind.Corrode: enemy!.Corrosion += p + (HasRelic("catalyst") ? 1 : 0); break;
            case EffectKind.Detonate:
                int corrosion = enemy!.Corrosion;
                enemy.Corrosion = 0;
                HitEnemy(enemy, p + corrosion * 2, origin, corrosion > 0 ? ["MELTDOWN"] : []);
                break;
            case EffectKind.Push:
            case EffectKind.Pull:
                HitEnemy(enemy!, p, origin);
                var to = enemy!.Cell with { Y = enemy.Cell.Y + (card.Definition.Effect == EffectKind.Push ? -1 : 1) };
                if (_enemies.Contains(enemy) && CanEnter(to))
                {
                    enemy.Cell = to;
                    TriggerEnemyTile(enemy);
                }
                else AddLog("Displacement blocked; damage still resolved.");
                break;
            case EffectKind.Volley: TroopAttack(troop!, p); break;
            case EffectKind.Execute:
                bool execute = enemy!.Hp <= 10;
                HitEnemy(enemy, execute ? p * 2 : p, origin, execute ? ["EXECUTION"] : []);
                break;
            case EffectKind.Arc:
                foreach (var victim in _enemies.Where(e => e.Cell.Distance(target!.Value) <= 1).ToArray())
                    HitEnemy(victim, p, origin);
                break;
            case EffectKind.Draw: DrawCards(p); break;
            case EffectKind.Battery: Ap += p; break;
            case EffectKind.Rally:
                foreach (var ally in _troops.Where(t => _troops.Any(other => other != t && other.Cell.Distance(t.Cell) <= 1)))
                    ally.AttackBonus += p;
                break;
            case EffectKind.Repair: troop!.Hp = Math.Min(troop.MaxHp, troop.Hp + p); break;
            case EffectKind.Fortify:
                OrganisationBlock += p;
                foreach (var ally in _troops) ally.Block += 3;
                break;
            case EffectKind.Smoke:
                OrganisationBlock += p;
                foreach (var ally in _troops) ally.MarkTurns = 0;
                break;
            case EffectKind.Trap:
            case EffectKind.Mine:
            case EffectKind.Acid:
                var kind = card.Definition.Effect == EffectKind.Trap ? TileKind.Trap :
                    card.Definition.Effect == EffectKind.Mine ? TileKind.Mine : TileKind.Acid;
                int power = p + (kind == TileKind.Acid ? HasRelic("catalyst") ? 1 : 0 : HasRelic("rigger") ? 2 : 0);
                SetTile(new TileEffect(kind, target!.Value, 3, power));
                if (kind == TileKind.Acid && enemy is not null) enemy.Corrosion += power;
                break;
            case EffectKind.Advance:
                foreach (var ally in _troops) { ally.MovesLeft += p; ally.Marched = true; }
                break;
            case EffectKind.Overwatch: troop!.HoldBonus += p; break;
            case EffectKind.RecoverSupply:
                _supplies.Add(_lastSupply!);
                _lastSupply = null;
                OrganisationBlock += p;
                break;
            case EffectKind.Amplify: _supplyAmplify += p; break;
            case EffectKind.SupplyUse: SupplyUses += p; break;
            case EffectKind.Purge:
                enemy!.Block = 0;
                HitEnemy(enemy, p, origin);
                break;
            case EffectKind.Siege:
                bool supported = _troops.Any(t => t.Cell.Distance(enemy!.Cell) <= 2);
                HitEnemy(enemy!, p + (supported ? 5 : 0), origin, supported ? ["FIRE SUPPORT"] : []);
                break;
            default: throw new InvalidOperationException($"Unhandled action {card.Definition.Effect}.");
        }
    }

    public HashSet<Cell> SupplyTargets(int slot)
    {
        if (slot < 0 || slot >= _supplies.Count) return new();
        return Content.Supply(_supplies[slot]).Target switch
        {
            TargetKind.Enemy => _enemies.Select(e => e.Cell).ToHashSet(),
            TargetKind.Troop => _troops.Where(t => t.Hp < t.MaxHp).Select(t => t.Cell).ToHashSet(),
            _ => new()
        };
    }

    public CommandResult UseSupply(int slot, Cell? target = null)
    {
        if (Phase != Phase.Cards) return CommandResult.Reject("Supplies can only be used during card play.");
        if (SupplyUses <= 0) return CommandResult.Reject("No supply uses left this round.");
        if (slot < 0 || slot >= _supplies.Count) return CommandResult.Reject("That supply slot is empty.");
        var supply = Content.Supply(_supplies[slot]);
        if (supply.Target != TargetKind.None && (!target.HasValue || !SupplyTargets(slot).Contains(target.Value)))
            return CommandResult.Reject("Choose a highlighted legal supply target.");
        _impacts.Clear();
        int boost = _supplyAmplify;
        _supplyAmplify = 0;
        _lastSupply = supply.Id;
        _supplies.RemoveAt(slot);
        SupplyUses--;
        var enemy = _enemies.FirstOrDefault(e => e.Cell == target);
        switch (supply.Id)
        {
            case "shield": OrganisationBlock += 10 + boost; break;
            case "stim": Ap += 1 + boost / 2; break;
            case "emp": enemy!.Jammed = Math.Max(enemy.Jammed, 1 + boost / 4); break;
            case "charge": HitEnemy(enemy!, 8 + boost, new Cell(3, 5), boost > 0 ? ["HOTWIRED"] : []); break;
            case "medkit":
                var troop = _troops.Single(t => t.Cell == target);
                troop.Hp = Math.Min(troop.MaxHp, troop.Hp + 8 + boost);
                break;
            case "acid-vial": enemy!.Corrosion += 4 + boost; break;
            default: throw new InvalidOperationException("Unknown supply.");
        }
        AddLog($"Used {supply.Name}" + (boost > 0 ? " / amplified." : "."));
        CheckVictory();
        return CommandResult.Success();
    }

    private void TriggerEnemyTile(Enemy enemy)
    {
        foreach (var tile in _tiles.Where(t => t.Cell == enemy.Cell).ToArray())
        {
            if (!_enemies.Contains(enemy)) break;
            if (tile.Kind == TileKind.Acid) enemy.Corrosion += tile.Power;
            if (tile.Kind is not (TileKind.Trap or TileKind.Mine)) continue;
            _tiles.Remove(tile);
            var victims = tile.Kind == TileKind.Mine ? _enemies.Where(e => e.Cell.Distance(tile.Cell) <= 1).ToArray() : [enemy];
            if (tile.Kind == TileKind.Trap) enemy.Rooted = 1;
            foreach (var victim in victims)
                HitEnemy(victim, victim == enemy ? tile.Power : tile.Power / 2, tile.Cell, ["DEAD END"]);
        }
    }

    private void HitTroop(Troop troop, int power, Cell from, string[]? combos = null)
    {
        var damage = Damage(troop.Hp, troop.Block, power);
        troop.Hp = damage.Hp;
        troop.Block = damage.Block;
        string[] links = damage.Lost > 0 ? combos ?? [] : [];
        if (links.Length > 0) EnemyComboHits++;
        _impacts.Add(new Impact(from, troop.Cell, damage.Lost, links, true));
        AddLog($"{troop.Card.Name} loses {damage.Lost} HP.");
        if (troop.Hp > 0) return;
        _troops.Remove(troop);
        _zones[troop.Card.Id] = Zone.Unavailable;
        AddLog($"{troop.Card.Name} unavailable until next fight.");
    }

    private void TriggerFriendlyTile(Troop troop)
    {
        var hazard = _tiles.FirstOrDefault(t => t.Cell == troop.Cell && t.Kind == TileKind.EnemyHazard);
        if (hazard is null) return;
        troop.RootedRounds = 1;
        HitTroop(troop, hazard.Power, hazard.Cell, ["CONTAINMENT"]);
    }
}
