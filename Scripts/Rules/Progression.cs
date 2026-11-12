using System.Text.Json;

namespace FutureDark.Rules;

public static class Unlocks
{
    public static readonly int[] Thresholds = [0, 60, 140, 240, 360];
    public static readonly string[] Names = ["Street cell", "Disruption", "Chemical warfare", "Formations", "Logistics"];
    private static readonly string[][] Packs =
    [
        ["runner", "marksman", "network", "armour", "intercept", "recon", "march", "strike", "barricade", "exposed", "pulse"],
        ["breacher", "sapper", "mark", "jam", "push", "pull", "trap", "mine"],
        ["corrode", "detonate", "acid", "purge", "arc", "execute"],
        ["medic", "sentinel", "volley", "rally", "repair", "fortify", "advance", "overwatch", "siege"],
        ["draw", "battery", "smoke", "recover-supply", "amplify", "supply-use"]
    ];
    public static int Tier(int xp) => Thresholds.Count(t => xp >= t) - 1;
    public static IReadOnlyList<CardDef> Cards(int tier) => Packs.Take(tier + 1).SelectMany(p => p).Select(Content.Card).ToArray();
    public static IReadOnlyList<RelicDef> Relics(int tier) => Content.Relics.Take(Math.Min(12, 3 + tier * 3)).ToArray();
}

public sealed record ProgressProfile
{
    public int Version { get; init; } = 1;
    public int Experience { get; init; }
    public string[] AwardedRuns { get; init; } = [];
    public int Tier => Unlocks.Tier(Experience);

    public void Validate()
    {
        if (Version != 1 || Experience < 0 || AwardedRuns is null || AwardedRuns.Any(id => !Guid.TryParseExact(id, "N", out _)) ||
            AwardedRuns.Distinct().Count() != AwardedRuns.Length)
            throw new InvalidDataException("Unlock profile is invalid or unsupported.");
    }
}

internal static class AtomicJson
{
    public static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, value, new JsonSerializerOptions { WriteIndented = true });
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

public sealed class ProgressStore(string path)
{
    public ProgressProfile Load()
    {
        if (!File.Exists(path)) return new();
        var profile = JsonSerializer.Deserialize<ProgressProfile>(File.ReadAllText(path)) ?? throw new InvalidDataException("Unlock profile is empty.");
        profile.Validate();
        return profile;
    }

    public (ProgressProfile Profile, int Earned) Award(RunState run)
    {
        if (run.Screen is not (RunScreen.Complete or RunScreen.Defeat) && run.Battle?.Phase != Phase.Defeat)
            throw new InvalidOperationException("Only a completed or defeated run earns XP.");
        var profile = Load();
        if (profile.AwardedRuns.Contains(run.RunId)) return (profile, 0);
        int earned = Math.Min(160, run.Victories * 8 + run.CompletedActs.Count * 18 + (run.Screen == RunScreen.Complete ? 40 : 0));
        profile = profile with { Experience = checked(profile.Experience + earned), AwardedRuns = [.. profile.AwardedRuns, run.RunId] };
        AtomicJson.Write(path, profile);
        return (profile, earned);
    }
}
