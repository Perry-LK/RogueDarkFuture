using FutureDark.Rules;

namespace FutureDark.Presentation;

public readonly record struct RoutePoint(float X, float Y);
public sealed record RouteStreet(string From, string To, IReadOnlyList<RoutePoint> Points);

public sealed class RouteLayout
{
    public const float Height = 496;
    public IReadOnlyDictionary<string, RoutePoint> Positions { get; }
    public IReadOnlyList<RouteStreet> Streets { get; }
    public float Width { get; }

    public RouteLayout(IReadOnlyList<RouteNode> nodes, int seed, int act)
    {
        var random = new Random(unchecked(seed * 701 + act * 7919));
        var positions = new Dictionary<string, RoutePoint>();
        float x = nodes.Count == 1 ? 666 : 86;
        foreach (var layer in nodes.GroupBy(n => n.Column).OrderBy(g => g.Key))
        {
            var ordered = layer.OrderBy(n => n.Row).ToArray();
            float offset = ordered.Length == 1 ? 0 : random.Next(-14, 15);
            for (int i = 0; i < ordered.Length; i++)
                positions.Add(ordered[i].Id, new RoutePoint(x, Height / 2 + (i - (ordered.Length - 1) / 2f) * 160 + offset));
            x += random.Next(218, 327);
        }
        Positions = positions;
        Width = positions.Values.Max(p => p.X) + 86;
        Streets = nodes.SelectMany(n => n.Next.Select(next =>
        {
            RoutePoint a = positions[n.Id], b = positions[next];
            float gap = b.X - a.X, middle = (a.Y + b.Y) / 2;
            var turnA = new RoutePoint(a.X + gap * .32f, middle);
            var turnB = new RoutePoint(b.X - gap * .32f, middle);
            return new RouteStreet(n.Id, next, [Trim(a, turnA), turnA, turnB, Trim(b, turnB)]);
        })).ToArray();
    }

    private static RoutePoint Trim(RoutePoint end, RoutePoint toward)
    {
        float dx = toward.X - end.X, dy = toward.Y - end.Y;
        float distance = MathF.Sqrt(dx * dx + dy * dy);
        return new RoutePoint(end.X + dx / distance * 38, end.Y + dy / distance * 38);
    }
}
