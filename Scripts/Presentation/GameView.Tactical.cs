using Godot;
using FutureDark.Rules;
using GColour = Godot.Color;

namespace FutureDark.Presentation;

public partial class GameView
{
    private sealed class CardMotion(Rect2 rect, float start)
    {
        public Rect2 Rect = rect;
        public float Start = start;
    }
    private sealed record CardFlight(Card Card, Rect2 From, Rect2 To, float Start);
    private sealed record UnitMotion(Vector2 From, Vector2 To, float Start);
    private readonly Dictionary<int, CardMotion> _cardMotions = new();
    private readonly List<CardFlight> _flying = new();
    private readonly Dictionary<string, UnitMotion> _unitMotions = new();
    private float _uiTime;
    private float _presentationDelta;
    private string _turnNotice = "";
    private string _turnDetail = "";
    private string _nextNotice = "";
    private float _noticeTime;

    private static GColour CategoryColor(CardKind kind) => kind switch
    {
        CardKind.Troop => new("#8d9b68"),
        CardKind.Action => new("#be805d"),
        CardKind.Block => new("#7899b0"),
        _ => new("#a18aad")
    };

    private void DrawCategoryIcon(CardKind kind, Vector2 p, float size, GColour color)
    {
        switch (kind)
        {
            case CardKind.Troop:
                DrawCircle(p + new Vector2(0, -size * .45f), size * .28f, color);
                DrawPolygon([p + new Vector2(-size * .7f, size * .7f), p + new Vector2(-size * .35f, -size * .08f),
                    p + new Vector2(size * .35f, -size * .08f), p + new Vector2(size * .7f, size * .7f)], [color]);
                break;
            case CardKind.Action:
                DrawPolygon([p + new Vector2(size * .3f, -size), p + new Vector2(-size * .7f, size * .2f),
                    p + new Vector2(0, size * .2f), p + new Vector2(-size * .3f, size),
                    p + new Vector2(size * .7f, -size * .2f), p + new Vector2(0, -size * .2f)], [color]);
                break;
            case CardKind.Block:
                Outline([p + new Vector2(-size * .7f, -size * .8f), p + new Vector2(size * .7f, -size * .8f),
                    p + new Vector2(size * .6f, size * .2f), p + new Vector2(0, size),
                    p + new Vector2(-size * .6f, size * .2f)], color, 2);
                break;
            case CardKind.Board:
                for (int i = 0; i < 4; i++)
                    DrawRect(new Rect2(p + new Vector2((i % 2) * size - size, (i / 2) * size - size),
                        new Vector2(size * .8f, size * .8f)), color, false, 1.5f);
                break;
        }
    }

    private void DrawCardStencil(Card card, Vector2 centre, float size, GColour color)
    {
        DrawCategoryIcon(card.Definition.Kind, centre, size, color);
        DrawArc(centre, size * 1.4f, -.6f, 2.3f, 20, color, 1, true);
        DrawLine(centre + new Vector2(-size * 1.9f, size * 1.4f), centre + new Vector2(size * 1.7f, size * 1.4f), color, 1);
        if (card.Definition.Piece == PieceKind.Marksman)
            DrawLine(centre + new Vector2(-size * .8f, size * .5f), centre + new Vector2(size, -size), color, 3, true);
        if (card.Definition.Effect is EffectKind.Corrode or EffectKind.Detonate or EffectKind.Acid)
            for (int i = 0; i < 3; i++) DrawCircle(centre + new Vector2(i * size * .5f - size * .5f, size * 1.8f), size * .1f, color);
    }

    private Enemy? HoveredEnemy(Combat battle) => battle.Enemies.LastOrDefault(e =>
        new Rect2(CellPoint(e.Cell) - new Vector2(27, 91), new Vector2(54, 108)).HasPoint(_mouse) && _modal.Length == 0);

    private static string SpecialDescription(Enemy enemy) => enemy.Definition.Role switch
    {
        EnemyRole.Spotter => "Marks a committed tile. A surviving spotter enables snipers to exploit marked troops for +3 attack damage.",
        EnemyRole.Sniper => "Moves into a firing column. Precision attacks gain +3 while a marked troop remains in the plan and an unjammed spotter is alive.",
        EnemyRole.Shield => "Adjacent allies reduce incoming direct damage by 2. Move them apart or jam the shield to break the formation.",
        EnemyRole.Controller => "Places a live hazard on its committed tile. Entering it damages and roots troops; riot units can push you into it.",
        EnemyRole.Commander => "Command arms allies within two cells for +2 attack damage. Destroy or jam the officer, or break its formation.",
        EnemyRole.Reinforcer => "Calls a drone into a fixed landing tile. Block the tile or jam the call. New drones act next round. Squad cap: six.",
        EnemyRole.Pusher => "Leaps like a knight and attacks. Pushes its committed target toward a nearby live hazard, or one cell back.",
        _ => "Moves one orthogonal cell, then follows a repeating attack and recovery routine."
    };

    private void DrawIntentIcon(Combat battle, Enemy enemy)
    {
        var intent = battle.GetIntent(enemy);
        Vector2 p = UnitPosition("e" + enemy.Id, enemy.Cell) + new Vector2(0, -77);
        string icon = enemy.Jammed > 0 ? "-" : enemy.Step.Kind switch
        {
            EnemyActionKind.Attack => "X", EnemyActionKind.Push => ">",
            EnemyActionKind.Reinforce => "B", EnemyActionKind.Mark => "+",
            EnemyActionKind.Command => "^", EnemyActionKind.Summon => "*",
            EnemyActionKind.Rest => ".", _ => "#"
        };
        Box(new Rect2(p - new Vector2(22, 14), new Vector2(44, 25)), Ink, Line);
        Text(icon, p.X - 14, p.Y + 5, 17, enemy.Jammed > 0 ? Muted : Warning, _mono);
        if (intent.Power.HasValue)
        {
            string numeric = (enemy.Step.Kind == EnemyActionKind.Reinforce ? "+" : "") + intent.Power.Value;
            Text(numeric, p.X + 1, p.Y + 5, 12, Paper, _mono);
        }
        else if (!intent.Observed) Text("?", p.X + 4, p.Y + 5, 12, Muted, _mono);
        if (enemy.Mark > 0 || enemy.Corrosion > 0)
            Text((enemy.Mark > 0 ? $"+{enemy.Mark} " : "") + (enemy.Corrosion > 0 ? $"C{enemy.Corrosion}" : ""),
                p.X - 23, p.Y - 22, 12, CategoryColor(CardKind.Action), _mono);
        _hotspots.Add(new Hotspot("intent-" + enemy.Id, new Rect2(p - new Vector2(24, 15), new Vector2(48, 29)), () =>
        { _selectedEnemy = enemy.Id; _selectedTroop = null; _selectedCard = null; }, _lock <= 0));
    }

    private void DrawInspector(Combat battle)
    {
        float x = 1084;
        int? cardId = _hoverCard ?? _selectedCard;
        Enemy? enemy = HoveredEnemy(battle) ?? battle.Enemies.FirstOrDefault(e => e.Id == _selectedEnemy);
        Troop? troop = battle.Troops.FirstOrDefault(t => t.Card.Id == _selectedTroop);
        if (cardId.HasValue)
        {
            var card = battle.GetCard(cardId.Value);
            var color = CategoryColor(card.Definition.Kind);
            Box(new Rect2(x, 176, 321, 421), Panel, color.Darkened(.35f));
            DrawRect(new Rect2(x, 176, 321, 5), color);
            DrawCardStencil(card, new Vector2(x + 257, 247), 31, new GColour(color, .46f));
            Text($"{card.Definition.Kind} / {card.Definition.Cost} AP", x + 20, 211, 16, color);
            Paragraph(card.Name, x + 20, 253, 237, 28, Paper, 2, _bold);
            Rule(x + 20, 297, 281, color.Darkened(.5f));
            Paragraph(Content.RulesText(card), x + 20, 329, 281, 18, Paper, 8);
            Paragraph(card.Definition.Synergy, x + 20, 520, 281, 16, color, 3);
        }
        else if (_inspectedSupply is not null)
        {
            var supply = Content.Supply(_inspectedSupply);
            Text(supply.Name, x + 18, 222, 25, Paper, _bold);
            Paragraph(supply.Description, x + 18, 269, 281, 20, Muted, 4);
            Paragraph($"Uses left this round: {battle.SupplyUses}.\nSupplies are outside your deck. Use during card play.", x + 18, 389, 281, 17, Muted, 4);
        }
        else if (enemy is not null)
        {
            var intent = battle.GetIntent(enemy);
            Text("Hostile / " + enemy.Cell, x + 18, 204, 16, Warning);
            Paragraph(enemy.Definition.Name, x + 18, 246, 281, 29, Paper, 2, _bold);
            Text($"HP {enemy.Hp}    Guard {enemy.Block}", x + 18, 305, 19, Paper, _mono);
            Paragraph(intent.Observed ? intent.Detail : intent.Label + " / Recon for detail", x + 18, 343, 281, 16, Muted, 2);
            Paragraph(SpecialDescription(enemy), x + 18, 385, 281, 17, Muted, 7);
            Text($"Marks {enemy.Mark} / Corrosion {enemy.Corrosion}", x + 18, 539, 16, CategoryColor(CardKind.Action));
            Text($"Formation armour {battle.FormationArmour(enemy)}", x + 18, 566, 16, Muted);
        }
        else if (troop is not null)
        {
            Text("Friendly / " + troop.Cell, x + 18, 204, 16, CategoryColor(CardKind.Troop));
            Paragraph(troop.Card.Name, x + 18, 246, 281, 29, Paper, 2, _bold);
            Text($"HP {troop.Hp}/{troop.MaxHp} / Block {troop.Block}", x + 18, 300, 18, Paper, _mono);
            Paragraph($"Move: {Content.Pieces[troop.Kind].MoveText}.\nAttack: {Content.Pieces[troop.Kind].AttackText}.", x + 18, 341, 281, 17, Muted, 6);
            var targets = battle.Enemies.Where(e => battle.AttackCells(troop.Kind, troop.Cell).Contains(e.Cell)).ToArray();
            Paragraph(targets.Length == 0 ? "No enemy in the current attack pattern." :
                string.Join("\n", targets.Select(e => $"{e.Definition.Name}: {battle.PreviewAttack(troop, e).Damage} before defence")), x + 18, 488, 281, 16, Paper, 3);
            if (battle.Phase == Phase.Movement)
                Button("hold", $"HOLD / {troop.MovesLeft} MOVES LEFT", new Rect2(x + 18, 551, 281, 37), () => Act(battle.Hold(troop.Card.Id)), troop.MovesLeft > 0);
        }
        else
        {
            Text(battle.Phase == Phase.Cards ? "Build the connection." : battle.Phase == Phase.Movement ? "Set your formation." : "Choose what stays.", x + 18, 225, 23, Paper, _bold);
            Paragraph(battle.Phase == Phase.Cards ? "Hover a card to inspect it.\nSelect, then choose its target." :
                battle.Phase == Phase.Movement ? "Click a piece for legal moves and automatic attacks. Hold any moves you do not need." :
                $"Select {battle.RequiredDiscards} cards to discard. The rest remain in your hand.", x + 18, 274, 281, 19, Muted, 5);
            Rule(x + 18, 407, 281);
            Paragraph("Enemy icons show broad intent. Hover a hostile for its role. Recon reveals exact power and committed destinations.", x + 18, 441, 281, 17, Muted, 5);
        }
        if (battle.Danger)
            Text("! High incoming organisation threat", 37, 187, 16, Warning);
    }

    private void DrawSupplyBelt(Combat battle)
    {
        _inspectedSupply = null;
        Text("Supplies", 36, 593, 14, Muted);
        for (int i = 0; i < 3; i++)
        {
            int slot = i;
            var rect = new Rect2(36 + i * 104, 603, 96, 28);
            string label = i < battle.Supplies.Count ? battle.Supplies[i].Replace("-vial", "").ToUpperInvariant() : "EMPTY";
            Button("supply-" + i, label, rect, () => SelectSupply(slot), i < battle.Supplies.Count && battle.Phase == Phase.Cards && battle.SupplyUses > 0);
            if (i < battle.Supplies.Count && (_keyboardFocus ? _focus == "supply-" + i : rect.HasPoint(_mouse)))
                _inspectedSupply = battle.Supplies[i];
            if (_selectedSupply == i) DrawRect(rect.Grow(2), Territory, false, 1);
        }
    }

    private void SelectSupply(int slot)
    {
        if (_run.Battle is not { } battle || slot >= battle.Supplies.Count) return;
        _selectedCard = null;
        _selectedTroop = null;
        _selectedEnemy = null;
        _selectedSupply = slot;
        if (Content.Supply(battle.Supplies[slot]).Target == TargetKind.None) UseSelectedSupply(null);
        else Act(CommandResult.Success("Select a highlighted target for the supply."));
    }

    private void UseSelectedSupply(Cell? target)
    {
        if (_run.Battle is not { } battle || !_selectedSupply.HasValue) return;
        if (Act(battle.UseSupply(_selectedSupply.Value, target)))
        {
            _selectedSupply = null;
            AnimateImpacts(battle.Impacts);
        }
    }

    private void UpdatePresentation(float delta)
    {
        _presentationDelta = delta;
        _uiTime += delta;
        _flying.RemoveAll(f => _uiTime - f.Start > .48f);
        foreach (string key in _unitMotions.Where(p => _uiTime - p.Value.Start > .35f).Select(p => p.Key).ToArray())
            _unitMotions.Remove(key);
        _noticeTime = Math.Max(0, _noticeTime - delta);
        if (_noticeTime <= 0 && _nextNotice.Length > 0)
        {
            _turnNotice = _nextNotice;
            _turnDetail = "Card play is open. Plan your next connection.";
            _nextNotice = "";
            _noticeTime = 1.8f;
        }
    }

    private Rect2 AnimatedCardRect(int id, Rect2 target, bool hover)
    {
        if (!_cardMotions.TryGetValue(id, out var motion))
        {
            motion = new CardMotion(_reduced ? target : new Rect2(36, 865, target.Size.X, target.Size.Y),
                _uiTime + (_reduced ? 0 : _cardMotions.Count % 9 * .035f));
            _cardMotions[id] = motion;
        }
        if (hover) target.Position -= new Vector2(0, 9);
        if (_reduced) motion.Rect = target;
        else if (_uiTime >= motion.Start)
        {
            float weight = 1 - Mathf.Exp(-18 * _presentationDelta);
            motion.Rect = new Rect2(motion.Rect.Position.Lerp(target.Position, weight), motion.Rect.Size.Lerp(target.Size, weight));
        }
        return motion.Rect;
    }

    private void PlayCard(int id, Cell? target = null)
    {
        var battle = _run.Battle!;
        var card = battle.GetCard(id);
        var from = _cardMotions.TryGetValue(id, out var motion) ? motion.Rect : new Rect2(590, 663, 180, 177);
        var positions = CapturePositions(battle);
        if (!Act(battle.Play(id, target))) return;
        _selectedCard = null;
        _cardMotions.Remove(id);
        if (!_reduced)
        {
            Vector2 to = target.HasValue ? CellPoint(target.Value) : new Vector2(590, 342);
            _flying.Add(new CardFlight(card, from, new Rect2(to - new Vector2(27, 35), new Vector2(54, 70)), _uiTime));
        }
        AnimatePositions(battle, positions);
        if (battle.Impacts.Count > 0) AnimateImpacts(battle.Impacts);
        else _lock = _reduced ? .06f : .14f;
    }

    private void FlyDiscards(Combat battle)
    {
        if (_discards.Count != battle.RequiredDiscards) return;
        foreach (int id in _discards)
        {
            if (!_reduced && _cardMotions.TryGetValue(id, out var motion))
                _flying.Add(new CardFlight(battle.GetCard(id), motion.Rect, new Rect2(160, 858, 54, 70), _uiTime));
            _cardMotions.Remove(id);
        }
    }

    private void DrawFlyingCards()
    {
        if (_reduced) return;
        foreach (var flight in _flying)
        {
            float t = Mathf.Clamp((_uiTime - flight.Start) / .48f, 0, 1);
            float ease = 1 - Mathf.Pow(1 - t, 3);
            var rect = new Rect2(flight.From.Position.Lerp(flight.To.Position, ease), flight.From.Size.Lerp(flight.To.Size, ease));
            var color = new GColour(CategoryColor(flight.Card.Definition.Kind), 1 - t * .6f);
            Box(rect, new GColour(Panel, 1 - t * .5f), color);
            DrawCategoryIcon(flight.Card.Definition.Kind, rect.GetCenter(), rect.Size.X * .17f, color);
        }
    }

    private Dictionary<string, Cell> CapturePositions(Combat battle) => battle.Troops
        .ToDictionary(t => "t" + t.Card.Id, t => t.Cell)
        .Concat(battle.Enemies.Select(e => new KeyValuePair<string, Cell>("e" + e.Id, e.Cell)))
        .ToDictionary(pair => pair.Key, pair => pair.Value);

    private void AnimatePositions(Combat battle, Dictionary<string, Cell> before)
    {
        if (_reduced) return;
        foreach (var pair in CapturePositions(battle))
            if (before.TryGetValue(pair.Key, out var old) && old != pair.Value)
                _unitMotions[pair.Key] = new UnitMotion(CellPoint(old), CellPoint(pair.Value), _uiTime);
        if (_unitMotions.Count > 0) _lock = Math.Max(_lock, .3f);
    }

    private Vector2 UnitPosition(string id, Cell cell)
    {
        if (_reduced || !_unitMotions.TryGetValue(id, out var motion)) return CellPoint(cell);
        float t = Mathf.Clamp((_uiTime - motion.Start) / .3f, 0, 1);
        return motion.From.Lerp(motion.To, t * t * (3 - 2 * t));
    }

    private void NotifyTurn(string title, string detail, string next = "")
    {
        _turnNotice = title;
        _turnDetail = detail;
        _nextNotice = next;
        _noticeTime = next.Length > 0 ? .85f : 1.8f;
    }

    private void DrawTurnNotice()
    {
        if (_noticeTime <= 0 || _modal.Length > 0 || _title || _effects.Any(e => e.Impact.Combos.Length > 0)) return;
        Box(new Rect2(374, 91, 672, 73), Ink, Line);
        Text(_turnNotice, 395, 120, 23, Paper, _bold);
        Text(_turnDetail, 395, 147, 15, Muted);
    }
}
