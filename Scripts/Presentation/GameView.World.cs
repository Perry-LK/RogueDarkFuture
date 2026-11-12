using Godot;
using FutureDark.Rules;
using GColour = Godot.Color;

namespace FutureDark.Presentation;

public partial class GameView
{
    private static Vector2[][] CityDistricts()
    {
        var districts = new List<Vector2[]>();
        for (int d = 0; d < RunState.MainActCount; d++)
        {
            var points = new List<Vector2>();
            float start = -Mathf.Pi + d * Mathf.Tau / 5 + .025f;
            float end = start + Mathf.Tau / 5 - .05f;
            for (int i = 0; i <= 8; i++)
            {
                float a = Mathf.Lerp(start, end, i / 8f);
                float jag = i % 2 == 0 ? 1 : .955f;
                points.Add(new Vector2(720 + Mathf.Cos(a) * 590 * jag, 450 + Mathf.Sin(a) * 262 * jag));
            }
            for (int i = 5; i >= 0; i--)
            {
                float a = Mathf.Lerp(start, end, i / 5f);
                points.Add(new Vector2(720 + Mathf.Cos(a) * 181, 450 + Mathf.Sin(a) * 111));
            }
            districts.Add(points.ToArray());
        }
        districts.Add(Enumerable.Range(0, 10).Select(i =>
            new Vector2(720 + Mathf.Cos(i * Mathf.Tau / 10) * 163, 450 + Mathf.Sin(i * Mathf.Tau / 10) * 94)).ToArray());
        return districts.ToArray();
    }

    private static Vector2 DistrictLabel(int index)
    {
        float angle = -Mathf.Pi + (index + .5f) * Mathf.Tau / 5;
        return new Vector2(630 + Mathf.Cos(angle) * 400, 437 + Mathf.Sin(angle) * 188);
    }

    private float BoardStep => 63 * Math.Min(1, 13f / ((_run.Battle?.Board.Width ?? 7) + (_run.Battle?.Board.Height ?? 6)));

    private void DrawTerrainDetail(Combat battle, Cell cell)
    {
        var p = CellPoint(cell);
        int level = battle.Board.Level(cell);
        if (level > 0)
        {
            var top = Diamond(cell);
            DrawPolygon([top[1], top[2], top[3], top[3] + new Vector2(0, 18 * level),
                top[2] + new Vector2(0, 18 * level), top[1] + new Vector2(0, 18 * level)], [new GColour("#273138")]);
            DrawPolygon(top, [new GColour("#303b3e")]);
            Outline(top, Muted.Darkened(.5f));
        }
        foreach (var ramp in battle.Board.Ramps.Where(r => r.From == cell && battle.Board.Level(r.To) > level))
        {
            Vector2 toward = CellPoint(ramp.To);
            for (int step = 1; step <= 4; step++)
            {
                Vector2 mid = p.Lerp(toward, step / 5f);
                DrawLine(mid + new Vector2(-8, 0), mid + new Vector2(8, 0), Territory.Darkened(.1f), 2);
            }
        }
        if (battle.Board.Hazards.ContainsKey(cell))
        {
            Outline(Diamond(cell, 15), Warning.Darkened(.2f), 2);
            DrawEmblem(Emblem.Hazard, new Rect2(p - new Vector2(13, 17), new Vector2(26, 26)), Warning);
        }
    }

    private string TerrainDescription(Combat battle)
    {
        var cell = battle.Board.Cells.LastOrDefault(c => Geometry2D.IsPointInPolygon(_mouse, Diamond(c)));
        if (!battle.Board.Cells.Contains(cell) || !Geometry2D.IsPointInPolygon(_mouse, Diamond(cell))) return "";
        string text = $"{cell} / level {battle.Board.Level(cell)}";
        if (battle.Board.Walls.Contains(cell)) text += " / solid wall: blocks movement and troop fire, not intel";
        if (battle.Board.Cover.TryGetValue(cell, out int hp)) text += $" / destructible cover: {hp} HP; target with Signal Spike or troop attacks";
        if (battle.Board.Hazards.TryGetValue(cell, out int power)) text += $" / hazard: {power} damage on entry and at enemy-phase start";
        if (battle.Board.Ramps.Any(r => r.From == cell)) text += " / ramp: connects adjacent levels";
        if (battle.Board.Level(cell) > 0) text += " / ranged attacks gain +1 against lower targets";
        return text;
    }
}
