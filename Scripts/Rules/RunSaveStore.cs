using System.Text.Json;

namespace FutureDark.Rules;

public sealed record SavedCard(int Id, string DefinitionId, bool Upgraded);
public sealed record RunSnapshot
{
    public int Version { get; init; } = 2;
    public int UnlockTier { get; init; }
    public string[] StoryFlags { get; init; } = [];
    public string RunId { get; init; } = "";
    public int Seed { get; init; }
    public int Act { get; init; }
    public int Hp { get; init; }
    public int Funds { get; init; }
    public RunScreen Screen { get; init; }
    public SavedCard[] Cards { get; init; } = [];
    public string[] Relics { get; init; } = [];
    public string[] Supplies { get; init; } = [];
    public string[] Completed { get; init; } = [];
    public int[] CompletedActs { get; init; } = [];
    public string[] Available { get; init; } = [];
    public string? Current { get; init; }
    public string? ReplayNode { get; init; }
    public int TotalCombos { get; init; }
    public int TotalEnemyCombos { get; init; }
    public int Victories { get; init; }
    public int Removals { get; init; }

    public void Validate()
    {
        if (Version != 2 || !Guid.TryParseExact(RunId, "N", out _) || Act is < 1 or > RunState.FinaleAct ||
            UnlockTier is < 0 or > 4 || StoryFlags is null || StoryFlags.Any(f => !RunState.KnownStoryFlags.Contains(f)) ||
            StoryFlags.Distinct().Count() != StoryFlags.Length ||
            Hp is <= 0 or > RunState.MaxHp || Funds < 0 || Screen is not (RunScreen.Map or RunScreen.City) ||
            Cards is null || Cards.Length is < 8 or > 300 || Cards.Any(c => c is null || c.Id <= 0 ||
                !Content.Cards.Any(d => d.Id == c.DefinitionId)) || Cards.Select(c => c.Id).Distinct().Count() != Cards.Length ||
            Relics is null || Relics.Any(id => !Content.Relics.Any(r => r.Id == id)) || Relics.Distinct().Count() != Relics.Length ||
            Supplies is null || Supplies.Length > 3 || Supplies.Any(id => !Content.Supplies.Any(s => s.Id == id)) ||
            Completed is null || CompletedActs is null || Available is null ||
            CompletedActs.Any(a => a is < 1 or > RunState.MainActCount) || CompletedActs.Distinct().Count() != CompletedActs.Length ||
            TotalCombos < 0 || TotalEnemyCombos < 0 || Victories < 0 || Removals < 0)
            throw new InvalidDataException("Save data is invalid or from an unsupported version.");
        var nodes = RunState.GenerateAct(Seed, Act);
        if (Available.Any(id => !nodes.Any(n => n.Id == id)) || Completed.Any(id => !nodes.Any(n => n.Id == id)) ||
            (Current is not null && !nodes.Any(n => n.Id == Current)) ||
            (ReplayNode is not null && (Screen != RunScreen.Map || !Available.Contains(ReplayNode))))
            throw new InvalidDataException("Save contains an invalid room or route.");
    }
}

public sealed record SaveEntry(string RunId, DateTime Modified, RunSnapshot? Snapshot, string? Error);

public sealed class RunSaveStore(string directory)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private string PathFor(string runId)
    {
        if (!Guid.TryParseExact(runId, "N", out _)) throw new InvalidDataException("Invalid run identity.");
        return Path.Combine(directory, runId + ".json");
    }

    public void Save(RunState run)
    {
        var snapshot = run.SnapshotForSave();
        snapshot.Validate();
        Directory.CreateDirectory(directory);
        string path = PathFor(run.RunId);
        if (!File.Exists(path) && List().Count >= 3) throw new InvalidOperationException("All three run slots are occupied. Abandon one before starting another.");
        AtomicJson.Write(path, snapshot);
    }

    public RunState Load(string runId)
    {
        var snapshot = JsonSerializer.Deserialize<RunSnapshot>(File.ReadAllText(PathFor(runId)), Options)
            ?? throw new InvalidDataException("Save is empty.");
        if (snapshot.RunId != runId) throw new InvalidDataException("Save identity does not match its slot.");
        return RunState.Restore(snapshot);
    }

    public void Delete(string runId)
    {
        string path = PathFor(runId);
        if (File.Exists(path)) File.Delete(path);
    }

    public IReadOnlyList<SaveEntry> List()
    {
        if (!Directory.Exists(directory)) return [];
        var entries = new List<SaveEntry>();
        foreach (string path in Directory.EnumerateFiles(directory, "*.json"))
        {
            string id = Path.GetFileNameWithoutExtension(path);
            try
            {
                var snapshot = JsonSerializer.Deserialize<RunSnapshot>(File.ReadAllText(path), Options)
                    ?? throw new InvalidDataException("Save is empty.");
                snapshot.Validate();
                if (snapshot.RunId != id) throw new InvalidDataException("Mismatched save identity.");
                entries.Add(new SaveEntry(id, File.GetLastWriteTimeUtc(path), snapshot, null));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
            {
                entries.Add(new SaveEntry(id, File.GetLastWriteTimeUtc(path), null, error.Message));
            }
        }
        return entries.OrderByDescending(e => e.Modified).ToArray();
    }
}
