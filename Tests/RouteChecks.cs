using FutureDark.Rules;
using FutureDark.Presentation;

static class RouteChecks
{
    public static void PlanarRoutes()
    {
        for (int seed = 0; seed < 128; seed++)
        for (int act = 1; act <= RunState.MainActCount; act++)
        {
            var nodes = RunState.GenerateAct(seed, act);
            var byId = nodes.ToDictionary(n => n.Id);
            var reachable = new HashSet<string> { "entry" };
            foreach (var node in nodes.OrderBy(n => n.Column))
            {
                Check(reachable.Contains(node.Id), $"Unreachable node {node.Id}, seed {seed}.");
                Check(node.IsBoss || node.Next.Length > 0, "Non-boss routes cannot dead-end.");
                foreach (string next in node.Next)
                {
                    Check(byId[next].Column == node.Column + 1, "Edges must advance exactly one encounter.");
                    reachable.Add(next);
                }
            }
            var layout = new RouteLayout(nodes, seed, act);
            var again = new RouteLayout(nodes, seed, act);
            Check(layout.Positions.SequenceEqual(again.Positions), "Layout must survive deterministic room reloads.");
            Check(layout.Width > 1332, "The route must extend beyond the viewport rather than compress all rooms.");
            var gaps = nodes.GroupBy(n => n.Column).Select(g => layout.Positions[g.First().Id].X).ToArray();
            Check(gaps.Skip(1).Select((x, i) => x - gaps[i]).Distinct().Count() >= 3, "Route spacing must vary measurably.");
            for (int i = 0; i < layout.Streets.Count; i++)
            {
                var street = layout.Streets[i];
                for (int j = i + 1; j < layout.Streets.Count; j++)
                for (int a = 1; a < street.Points.Count; a++)
                for (int b = 1; b < layout.Streets[j].Points.Count; b++)
                    Check(!Intersect(street.Points[a - 1], street.Points[a], layout.Streets[j].Points[b - 1], layout.Streets[j].Points[b]),
                        $"Intersecting or overlapping streets {street.From}>{street.To} and {layout.Streets[j].From}>{layout.Streets[j].To}, seed {seed}.");
                foreach (var node in nodes.Where(n => n.Id != street.From && n.Id != street.To))
                for (int p = 1; p < street.Points.Count; p++)
                    Check(Distance(layout.Positions[node.Id], street.Points[p - 1], street.Points[p]) >= 40,
                        $"A street enters unrelated node {node.Id}, seed {seed}.");
            }
        }
    }

    private static double Cross(RoutePoint a, RoutePoint b) => a.X * (double)b.Y - a.Y * (double)b.X;
    private static RoutePoint Subtract(RoutePoint a, RoutePoint b) => new(a.X - b.X, a.Y - b.Y);

    private static bool Intersect(RoutePoint a, RoutePoint b, RoutePoint c, RoutePoint d)
    {
        var ab = Subtract(b, a);
        var cd = Subtract(d, c);
        var ac = Subtract(c, a);
        double denominator = Cross(ab, cd);
        if (Math.Abs(denominator) < .001)
        {
            if (Math.Abs(Cross(ac, ab)) > .001) return false;
            return Math.Max(Math.Min(a.X, b.X), Math.Min(c.X, d.X)) <= Math.Min(Math.Max(a.X, b.X), Math.Max(c.X, d.X)) + .001 &&
                   Math.Max(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y)) <= Math.Min(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y)) + .001;
        }
        double t = Cross(ac, cd) / denominator, u = Cross(ac, ab) / denominator;
        return t >= -.00001 && t <= 1.00001 && u >= -.00001 && u <= 1.00001;
    }

    private static double Distance(RoutePoint point, RoutePoint a, RoutePoint b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double t = Math.Clamp(((point.X - a.X) * dx + (point.Y - a.Y) * dy) / (dx * dx + dy * dy), 0, 1);
        return Math.Sqrt(Math.Pow(point.X - a.X - t * dx, 2) + Math.Pow(point.Y - a.Y - t * dy, 2));
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
