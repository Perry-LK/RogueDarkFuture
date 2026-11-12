using Godot;
using FutureDark.Rules;
using System.IO;

namespace FutureDark.Presentation;

public partial class GameView
{
    private ProgressStore _progress = null!;
    private ProgressProfile _profile = new();
    private bool _servicesReady;
    private string _checkpointStamp = "";
    private string? _settlementAttempt;
    private string? _abandonId;
    private int _earnedXp;
    private string _settingsPath = "";

    private void InitializePersistence()
    {
        string root = ProjectSettings.GlobalizePath("user://");
        if (_smoke)
        {
            root = Path.Combine(Path.GetTempPath(), "FutureDark-native-" + Guid.NewGuid().ToString("N"));
            _smokeSaveDirectory = root;
        }
        _settingsPath = Path.Combine(root, "settings.cfg");
        _saves = new RunSaveStore(Path.Combine(root, "runs"));
        _progress = new ProgressStore(Path.Combine(root, "progress.json"));
        try { Directory.CreateDirectory(root); _profile = _progress.Load(); _servicesReady = true; }
        catch (Exception error) when (PersistenceError(error))
        {
            GD.PushError(error.ToString());
            Act(CommandResult.Reject("Cannot read progression. No data has been overwritten: " + error.Message));
        }
    }

    private void BeginRun()
    {
        if (!_servicesReady) { Act(CommandResult.Reject("Progression storage is unavailable.")); return; }
        try
        {
            if (_saves.List().Count >= 3)
            {
                OpenSaves();
                Act(CommandResult.Reject("Three runs are active. Resume one or explicitly abandon a slot."));
                return;
            }
            if (!_smoke) Restart();
            _title = false;
            _checkpointStamp = "";
            ClearSelection();
            AutoCheckpoint();
        }
        catch (Exception error) when (PersistenceError(error))
        { GD.PushError(error.ToString()); Act(CommandResult.Reject("Could not start the run: " + error.Message)); }
    }

    private void AutoCheckpoint()
    {
        if (_title || !_servicesReady || _run.Screen is RunScreen.Complete or RunScreen.Defeat || _run.Battle?.Phase == Phase.Defeat) return;
        string boundary = _run.Screen is RunScreen.Map or RunScreen.City ? $"{_run.Screen}:{_run.Completed.Count}" : "room";
        string stamp = $"{_run.RunId}:{_run.Act}:{_run.CurrentNode?.Id}:{boundary}";
        if (stamp == _checkpointStamp) return;
        _checkpointStamp = stamp;
        TrySaveRun();
    }

    private void ContinueLatest()
    {
        if (!_servicesReady) { Act(CommandResult.Reject("Progression storage is unavailable.")); return; }
        try
        {
            var latest = _saves.List().FirstOrDefault();
            if (latest is null) { Act(CommandResult.Reject("No active run to continue.")); return; }
            if (latest.Error is not null) { OpenSaves(); Act(CommandResult.Reject("The latest slot cannot be read: " + latest.Error)); return; }
            LoadRun(latest.RunId);
        }
        catch (Exception error) when (PersistenceError(error))
        { GD.PushError(error.ToString()); Act(CommandResult.Reject("Could not continue: " + error.Message)); }
    }

    private void SaveAndExit(bool desktop)
    {
        bool finished = _run.Screen is RunScreen.Complete or RunScreen.Defeat || _run.Battle?.Phase == Phase.Defeat;
        if (!_title && !(finished ? SettleRun() : TrySaveRun())) return;
        if (desktop) GetTree().Quit();
        else { _title = true; _modal = ""; _effects.Clear(); _flying.Clear(); _lock = 0; }
    }

    private bool SettleRun()
    {
        if (_deletedRun == _run.RunId) return true;
        _settlementAttempt = _run.RunId;
        try
        {
            var award = _progress.Award(_run);
            _profile = award.Profile;
            if (award.Earned > 0) _earnedXp = award.Earned;
            _saves.Delete(_run.RunId);
            _deletedRun = _run.RunId;
            return true;
        }
        catch (Exception error) when (PersistenceError(error))
        {
            GD.PushError(error.ToString());
            Act(CommandResult.Reject("Run settlement failed. Save & exit retries safely: " + error.Message));
            return false;
        }
    }

    private void DrawAbandonSlot()
    {
        Box(new Rect2(405, 292, 630, 292), Ink, Warning);
        Text("Abandon this run?", 438, 345, 30, Paper, _bold);
        Paragraph("This permanently removes only the selected run. Abandoning does not award XP. Your unlocked collection is preserved.", 438, 395, 555, 19, Muted, 3);
        Button("keep-slot", "KEEP RUN", new Rect2(438, 502, 251, 42), OpenSaves);
        Button("delete-slot", "ABANDON RUN", new Rect2(709, 502, 291, 42), () =>
        {
            if (_abandonId is null) { Act(CommandResult.Reject("No run slot selected.")); return; }
            try { _saves.Delete(_abandonId); _abandonId = null; OpenSaves(); }
            catch (Exception error) when (PersistenceError(error))
            { GD.PushError(error.ToString()); Act(CommandResult.Reject("Could not remove the slot: " + error.Message)); }
        });
    }

    private void DrawUnlocks()
    {
        Box(new Rect2(110, 88, 1220, 750), Ink, Line);
        Text("The resistance learns.", 143, 137, 32, Paper, _bold);
        Text($"{_profile.Experience} XP / {Unlocks.Names[_profile.Tier]}", 146, 174, 20, Territory);
        Text("Earn XP when a run ends. Packs enter future runs, never your current deck. No permanent stat boosts.", 146, 204, 17, Muted);
        for (int tier = 0; tier <= 4; tier++)
        {
            float y = 254 + tier * 100;
            DrawEmblem(tier <= _profile.Tier ? Emblem.Check : Emblem.Deck, new Rect2(146, y - 23, 38, 38), tier <= _profile.Tier ? Territory : Muted);
            Text($"{Unlocks.Names[tier]} / {Unlocks.Thresholds[tier]} XP", 198, y, 21, tier <= _profile.Tier ? Paper : Muted, _bold);
            var previous = tier == 0 ? [] : Unlocks.Cards(tier - 1);
            Paragraph(string.Join(" / ", Unlocks.Cards(tier).Except(previous).Select(c => c.Name)), 198, y + 30, 1060, 16, Muted, 2);
        }
        Button("unlocks-close", "BACK", new Rect2(1077, 777, 213, 39), () => _modal = "");
    }
}
