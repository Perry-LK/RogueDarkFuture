using FutureDark.Rules;

namespace FutureDark.Presentation;

public partial class GameView
{
    private Cell? _smokeCover;
    private bool UsesComboFixture => _smokeScreen is "combo" or "keyboard" or "intel" or "enemyturn" or "action" or "reduced";
    private RunState CreateSmokeRun(int seed)
    {
        var run = new RunState(seed);
        if (_smokeScreen == "end") return RunState.Restore(run.SnapshotForSave() with { Victories = 10, Hp = 1 });
        if (!UsesComboFixture) return run;
        string[] cards = ["runner", "marksman", "breacher", "sentinel", "network", "network", "armour", "intercept",
            "recon", "march", "strike", "exposed", "pulse", "mark", "corrode", "detonate"];
        return RunState.Restore(run.SnapshotForSave() with
        {
            UnlockTier = 4, Cards = cards.Select((id, index) => new SavedCard(index + 1, id, false)).ToArray()
        });
    }
}
