namespace FutureDark.Rules;

public sealed partial class Board
{
    public HashSet<Cell> Walls { get; } = [];
    public Dictionary<Cell, int> Cover { get; } = [];
    public Dictionary<Cell, int> Elevation { get; } = [];
    public HashSet<(Cell From, Cell To)> Ramps { get; } = [];
    public Dictionary<Cell, int> Hazards { get; } = [];
    public int EnemyCapacity { get; private set; } = 6;
    public int Level(Cell cell) => Elevation.GetValueOrDefault(cell);
    public bool Accessible(Cell cell) => Cells.Contains(cell) && !Terrain.Contains(cell) && !Walls.Contains(cell) && !Cover.ContainsKey(cell);
    public bool CanTraverse(Cell from, Cell to, bool leap = false) => Cells.Contains(from) && Cells.Contains(to) &&
        (Level(from) == Level(to) || Math.Abs(Level(from) - Level(to)) == 1 && (leap || Ramps.Contains((from, to))));

    public void AddRamp(Cell a, Cell b)
    {
        if (a.Distance(b) != 1 || !Cells.Contains(a) || !Cells.Contains(b) || Math.Abs(Level(a) - Level(b)) != 1)
            throw new ArgumentException("A ramp must join adjacent cells one level apart.");
        Ramps.Add((a, b));
        Ramps.Add((b, a));
    }

    public bool ClearFire(Cell from, Cell to)
    {
        int x = from.X, y = from.Y, dx = Math.Abs(to.X - x), dy = Math.Abs(to.Y - y);
        int sx = Math.Sign(to.X - x), sy = Math.Sign(to.Y - y), error = dx - dy;
        bool Blocks(Cell c) => Walls.Contains(c) || Cover.ContainsKey(c);
        while (x != to.X || y != to.Y)
        {
            int twice = 2 * error;
            bool stepX = twice > -dy, stepY = twice < dx;
            if (stepX && stepY && Blocks(new Cell(x + sx, y)) && Blocks(new Cell(x, y + sy))) return false;
            if (stepX) { error -= dy; x += sx; }
            if (stepY) { error += dx; y += sy; }
            if ((x != to.X || y != to.Y) && Blocks(new Cell(x, y))) return false;
        }
        return true;
    }

    public bool Connected()
    {
        var walkable = Cells.Where(Accessible).ToHashSet();
        if (walkable.Count == 0) return false;
        var reached = new HashSet<Cell>();
        var queue = new Queue<Cell>();
        queue.Enqueue(walkable.First());
        while (queue.TryDequeue(out var cell))
        {
            if (!reached.Add(cell)) continue;
            foreach (var next in walkable.Where(c => c.Distance(cell) == 1 && CanTraverse(cell, c)))
                if (!reached.Contains(next)) queue.Enqueue(next);
        }
        return reached.Count == walkable.Count;
    }

    private Board(int width, int height) { Width = width; Height = height; }

    public static Board Generate(int seed, int act, int depth)
    {
        int size = Math.Min(2, (act - 1) / 2);
        var board = new Board(7 + size, 6 + size) { EnemyCapacity = act >= 4 ? 8 : 6 };
        var random = new Random(seed);
        int middle = board.Width / 2;
        for (int y = 0; y < board.Height; y++)
        for (int x = 0; x < board.Width; x++)
        {
            if ((x == 0 || x == board.Width - 1) && (y == 0 || y == board.Height - 1)) continue;
            var cell = new Cell(x, y);
            board.Cells.Add(cell);
            if (y >= board.Height - 2) board.Deployment.Add(cell);
            bool raised = act switch
            {
                1 => false,
                2 => x > middle && y < board.Height - 2,
                3 => x < middle && y < board.Height - 2,
                4 => y < 3,
                _ => (x <= 2 || x >= board.Width - 3) && y < 4
            };
            if (raised && !board.Deployment.Contains(cell)) board.Elevation[cell] = 1;
        }
        if (act > 1)
        {
            foreach (var cell in board.Cells)
            foreach (var next in new[] { cell with { X = cell.X + 1 }, cell with { Y = cell.Y + 1 } })
                if (board.Cells.Contains(next) && board.Level(cell) != board.Level(next) &&
                    (cell.X == next.X ? cell.X == 1 || cell.X == middle || cell.X == board.Width - 2 :
                        cell.Y == 1 || cell.Y == board.Height - 3))
                    board.AddRamp(cell, next);
        }
        var candidates = board.Cells.Where(c => !board.Deployment.Contains(c) && c.X != middle &&
            !board.Ramps.Any(r => r.From == c || r.To == c)).OrderBy(_ => random.Next()).ToArray();
        int placed = 0;
        foreach (var cell in candidates)
        {
            if (placed >= 2 + act) break;
            bool cover = placed % 2 == 1;
            if (cover) board.Cover.Add(cell, 6 + act);
            else { board.Terrain.Add(cell); board.Walls.Add(cell); }
            if (!board.Connected())
            {
                board.Cover.Remove(cell); board.Terrain.Remove(cell); board.Walls.Remove(cell);
                continue;
            }
            placed++;
        }
        if (depth > 0 || act > 1)
            foreach (var cell in candidates.Where(board.Accessible).Where(c => !board.Ramps.Any(r => r.From == c)).Take(Math.Min(3, act)))
                board.Hazards.Add(cell, act >= 4 ? 3 : 2);
        return board;
    }
}
