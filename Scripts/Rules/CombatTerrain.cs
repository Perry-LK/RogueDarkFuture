namespace FutureDark.Rules;

public sealed partial class Combat
{
    public int HeightBonus(Cell from, Cell to, bool ranged = true) => ranged && Board.Level(from) > Board.Level(to) ? 1 : 0;

    private void HitCover(Cell cell, int damage)
    {
        if (!Board.Cover.TryGetValue(cell, out int hp)) throw new InvalidOperationException("No cover at this target.");
        int lost = Math.Min(hp, damage);
        if (hp <= damage) Board.Cover.Remove(cell); else Board.Cover[cell] = hp - damage;
        _impacts.Add(new Impact(cell, cell, lost, []));
        AddLog($"Cover at {cell} loses {lost} HP" + (hp <= damage ? " / firing lane opened." : "."));
    }

    private void TickEnvironment()
    {
        foreach (var enemy in _enemies.ToArray())
            if (Board.Hazards.TryGetValue(enemy.Cell, out int power)) HitEnemy(enemy, power, enemy.Cell, direct: false);
        foreach (var troop in _troops.ToArray())
            if (Board.Hazards.TryGetValue(troop.Cell, out int power)) HitTroop(troop, power, troop.Cell);
    }
}
