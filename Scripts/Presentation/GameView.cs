using Godot;
using FutureDark.Rules;
using GColour = Godot.Color;

namespace FutureDark.Presentation;

public partial class GameView : Control
{
    private static readonly GColour Ink = new("#090d10");
    private static readonly GColour Panel = new("#11171b");
    private static readonly GColour Raised = new("#192126");
    private static readonly GColour Line = new("#344046");
    private static readonly GColour Muted = new("#929b9c");
    private static readonly GColour Paper = new("#dedcd0");
    private static readonly GColour Warning = new("#b47d74");
    private static readonly GColour Cyan = new("#62eee8");
    private static readonly GColour Amber = new("#ffb357");
    private Font _font = null!;
    private Font _bold = null!;
    private Font _mono = null!;
    private AudioStreamPlayer _audio = null!;
    private RunState _run = new(1);
    private bool _title = true;
    private bool _muted;
    private bool _reduced;
    private bool _haze = true;
    private string _cardChoice = "";
    private int? _selectedSupply;
    private string? _inspectedSupply;
    private string? _roomDialog;
    private RunSaveStore _saves = null!;
    private IReadOnlyList<SaveEntry> _saveEntries = [];
    private int _savePage;
    private string? _deletedRun;
    private string _modal = "";
    private string _message = "";
    private bool _messageError;
    private int? _selectedCard;
    private int? _selectedTroop;
    private int? _selectedEnemy;
    private int? _hoverCard;
    private readonly HashSet<int> _discards = new();
    private readonly List<Hotspot> _hotspots = new();
    private readonly List<VisualImpact> _effects = new();
    private string? _focus;
    private bool _keyboardFocus;
    private Vector2 _mouse;
    private Vector2 _origin;
    private float _scale = 1;
    private float _lock;
    private float _effectClock;
    private int _deckPage;
    private string _deckZone = "RUN";
    private Vector2 _boardImpulse;
    private bool _smoke;
    private int _smokeFrame;
    private string? _screenshotPath;
    private string _smokeScreen = "combo";
    private readonly Queue<(Action Action, float Delay, bool DuringLock)> _smokeSteps = new();
    private float _nextSmokeStep;
    private string? _smokeSaveDirectory;

    private sealed record Hotspot(string Id, Rect2 Rect, Action Click, bool Enabled, Vector2[]? Polygon = null)
    {
        public bool Contains(Vector2 point) => Polygon is null ? Rect.HasPoint(point) : Geometry2D.IsPointInPolygon(point, Polygon);
    }
    private sealed record VisualImpact(Impact Impact, float Start);

    public override void _Ready()
    {
        _font = new SystemFont { FontNames = ["Bahnschrift", "Arial", "DejaVu Sans"], FontWeight = 400 };
        _bold = new SystemFont { FontNames = ["Bahnschrift", "Arial", "DejaVu Sans"], FontWeight = 700 };
        _mono = new SystemFont { FontNames = ["Consolas", "DejaVu Sans Mono"], FontWeight = 400 };
        _audio = new AudioStreamPlayer { Stream = CreateComboSound(), VolumeDb = -18 };
        AddChild(_audio);
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.All;
        GrabFocus();
        LoadSettings();
        _saves = new RunSaveStore(ProjectSettings.GlobalizePath("user://runs"));
        string[] args = OS.GetCmdlineUserArgs();
        _smoke = args.Contains("--smoke-test");
        _screenshotPath = args.FirstOrDefault(a => a.StartsWith("--screenshot=", StringComparison.Ordinal))?["--screenshot=".Length..];
        _smokeScreen = args.FirstOrDefault(a => a.StartsWith("--smoke-screen=", StringComparison.Ordinal))?["--smoke-screen=".Length..] ?? "combo";
        if (_smoke) StartVisualSmoke();
        else _run = new RunState(NewSeed());
    }

    private static int NewSeed() => (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() & int.MaxValue);

    public override void _Process(double delta)
    {
        _scale = Math.Min(Size.X / 1440f, Size.Y / 900f);
        _origin = (Size - new Vector2(1440, 900) * _scale) / 2;
        _mouse = (GetLocalMousePosition() - _origin) / _scale;
        _lock = Math.Max(0, _lock - (float)delta);
        _effectClock += (float)delta;
        UpdatePresentation((float)delta);
        if (!_title && (_run.Battle?.Phase == Phase.Defeat || _run.Screen == RunScreen.Defeat) && _deletedRun != _run.RunId)
            DeleteFinishedSave();
        _effects.RemoveAll(e => _effectClock - e.Start > 1.8f);
        _boardImpulse = Vector2.Zero;
        if (!_reduced && _effects.Any(e => e.Impact.Combos.Length > 0))
        {
            float age = _effectClock - _effects.First(e => e.Impact.Combos.Length > 0).Start;
            if (age >= 0 && age < .32f)
                _boardImpulse = new Vector2(Mathf.Sin(age * 75), Mathf.Cos(age * 63)) * (1 - age / .32f) * 3;
        }
        QueueRedraw();
        if (_smoke) StepVisualSmoke(++_smokeFrame);
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), Ink);
        DrawSetTransform(_origin, 0, Vector2.One * _scale);
        _hotspots.Clear();
        _hoverCard = null;
        if (_title) DrawTitle();
        else
        {
            DrawHeader();
            switch (_run.Screen)
            {
                case RunScreen.City: DrawCityOverview(); break;
                case RunScreen.Map: DrawActMap(); break;
                case RunScreen.Combat: DrawCombat(); break;
                case RunScreen.Reward: DrawReward(); break;
                case RunScreen.Event: DrawRoomEvent(); break;
                case RunScreen.Vendor: DrawVendor(); break;
                case RunScreen.Complete: DrawEnd(true); break;
                case RunScreen.Defeat: DrawEnd(false); break;
            }
        }
        if (_modal.Length > 0)
        {
            _hotspots.Clear();
            DrawRect(new Rect2(0, 0, 1440, 900), new GColour(0, 0, 0, .88f));
            if (_modal == "guide") DrawGuide();
            if (_modal == "deck") DrawDeck();
            if (_modal == "log") DrawLog();
            if (_modal == "restart") DrawRestart();
            if (_modal == "room") DrawRoomDialog();
            if (_modal == "saves") DrawSaves();
            if (_modal == "inventory") DrawInventory();
            if (_modal == "choose-card") DrawCardChoice();
        }
        DrawTurnNotice();
        if (_modal.Length > 0 && _message.Length > 0)
            Paragraph(_message, 90, 865, 1260, 16, _messageError ? Warning : Paper, 1);
        else if (_messageError && (_title || _run.Screen is RunScreen.City or RunScreen.Map))
            Paragraph(_message, 60, 882, 1300, 16, Warning, 1);
        var focus = _hotspots.LastOrDefault(h => h.Enabled && (_keyboardFocus ? h.Id == _focus : h.Contains(_mouse)));
        if (focus is not null && _keyboardFocus)
            DrawRect(focus.Rect.Grow(3), Paper, false, 1);
    }

    private void Text(string text, float x, float y, int size = 16, GColour? color = null, Font? font = null)
        => DrawString(font ?? _font, new Vector2(x, y), text, HorizontalAlignment.Left, -1, size, color ?? Paper);

    private void Tag(string text, float x, float y, GColour? color = null) => Text(text, x, y, 11, color ?? Muted, _mono);
    private void Rule(float x, float y, float width, GColour? color = null)
        => DrawLine(new Vector2(x, y), new Vector2(x + width, y), color ?? Line, 1);
    private void Box(Rect2 rect, GColour? fill = null, GColour? border = null)
    {
        DrawRect(rect, fill ?? Panel);
        DrawRect(rect, border ?? Line, false, 1);
    }
    private void Paragraph(string text, float x, float y, float width, int size = 16, GColour? color = null, int maxLines = 20, Font? font = null)
    {
        var face = font ?? _font;
        int lineCount = 0;
        foreach (string paragraph in text.Split('\n'))
        {
            string line = "";
            foreach (string word in paragraph.Split(' '))
            {
                string next = line.Length == 0 ? word : line + " " + word;
                if (face.GetStringSize(next, fontSize: size).X > width && line.Length > 0)
                {
                    if (++lineCount > maxLines) return;
                    Text(line, x, y, size, color, face);
                    y += size * 1.35f;
                    line = word;
                }
                else line = next;
            }
            if (++lineCount > maxLines) return;
            Text(line, x, y, size, color, face);
            y += size * 1.35f;
        }
    }

    private void Button(string id, string text, Rect2 rect, Action click, bool enabled = true, bool primary = false)
    {
        enabled &= _lock <= 0;
        bool hover = enabled && ((_keyboardFocus && _focus == id) || (!_keyboardFocus && rect.HasPoint(_mouse)));
        var fill = primary && enabled ? Paper : hover ? Raised.Lightened(.12f) : Panel;
        Box(rect, fill, enabled ? primary ? Paper : hover ? Paper : Line : Line.Darkened(.45f));
        float textWidth = _mono.GetStringSize(text, fontSize: 12).X;
        Text(text, rect.Position.X + (rect.Size.X - textWidth) / 2, rect.Position.Y + rect.Size.Y / 2 + 4,
            12, primary && enabled ? Ink : enabled ? Paper : Muted.Darkened(.4f), _mono);
        _hotspots.Add(new Hotspot(id, rect, click, enabled));
    }

    private void DrawHeader()
    {
        Text("FUTURE / DARK", 30, 36, 23, Paper, _bold);
        Text(_run.ActName, 30, 61, 14, Muted);
        var phase = _run.Battle?.Phase;
        string state = _run.Screen == RunScreen.Combat ? phase switch
        {
            Phase.Cards => "01  Card play", Phase.Movement => "02  Board orders",
            Phase.Discard => "03  Discard", Phase.Victory => "Sector secured", _ => "Signal lost"
        } : _run.Screen switch { RunScreen.City => "City control", RunScreen.Map => "Choose your route", RunScreen.Vendor => "Night exchange", _ => "Resolve encounter" };
        if (_turnNotice == "Enemy turn" && _noticeTime > 0) state = "Enemy response";
        Box(new Rect2(263, 18, 240, 48), Raised, Line);
        DrawCategoryIcon(phase == Phase.Movement ? CardKind.Board : CardKind.Action, new Vector2(286, 42), 12, Paper);
        Text(state, 312, 47, 18, Paper);
        Text($"HP  {_run.Hp}/{RunState.MaxHp}", 528, 36, 19, Paper, _mono);
        Text($"Block  {_run.Battle?.OrganisationBlock ?? 0}", 528, 59, 14, Muted);
        Text($"{_run.Funds} funds", 715, 36, 19, Paper, _mono);
        Text($"{_run.Deck.Count} cards / {_run.Relics.Count} relics", 715, 59, 14, Muted);
        Button("inventory", "INVENTORY", new Rect2(902, 24, 118, 36), () => OpenModal("inventory"));
        Button("save", "SAVE RUN", new Rect2(1032, 24, 105, 36), SaveRun,
            _run.Screen is not (RunScreen.Defeat or RunScreen.Complete) && _run.Battle?.Phase != Phase.Defeat);
        Button("guide", "GUIDE", new Rect2(1149, 24, 86, 36), () => OpenModal("guide"));
        Button("sound", _muted ? "MUTED" : "SOUND", new Rect2(1247, 24, 79, 36), ToggleMute);
        Button("restart", "MENU", new Rect2(1338, 24, 70, 36), () => OpenModal("restart"));
        Rule(30, 81, 1378);
    }

    private void DrawTitle()
    {
        DrawCity(new Rect2(620, 150, 780, 570));
        DrawRect(new Rect2(0, 0, 620, 900), Ink);
        Tag("A TACTICAL DECKBUILDING ROGUELIKE", 64, 90);
        Rule(64, 113, 375);
        Tag("TWO DISTRICTS / ONE FINAL STRONGHOLD", 64, 219);
        Text("FUTURE", 54, 343, 112, Paper, _bold);
        Text("DARK", 54, 444, 112, Paper, _bold);
        DrawRect(new Rect2(65, 476, 38, 4), Paper);
        Paragraph("The city is quiet.\nMake your resistance count.", 64, 534, 430, 27, Paper);
        Paragraph("Build combinations. Position your pieces.\nBreak the pattern.", 64, 630, 400, 18, Muted);
        Button("begin", "ESTABLISH A SIGNAL  >", new Rect2(64, 713, 362, 56), () => { _title = false; ClearSelection(); }, primary: true);
        Button("title-load", "CONTINUE A SAVED RUN", new Rect2(64, 784, 240, 38), OpenSaves);
        Button("title-guide", "GUIDE", new Rect2(316, 784, 110, 38), () => OpenModal("guide"));
        Tag("GODOT / C#    DESKTOP PROTOTYPE", 64, 863);
        Tag("THE SYSTEM SEES PIECES.", 916, 760);
        Text("YOU SEE CONNECTIONS.", 914, 790, 23, Paper, _bold);
        Tag("NO LIGHT WITHOUT A REASON.", 916, 817);
        Rule(916, 836, 425);
    }

    private void DrawCity(Rect2 bounds)
    {
        DrawRect(bounds, new GColour("#10171b"));
        for (int i = 0; i < 24; i++)
        {
            float x = bounds.Position.X + i * bounds.Size.X / 24;
            float height = 110 + (i * 137 % 340);
            var rect = new Rect2(x, bounds.End.Y - height, 42 + i % 3 * 16, height);
            Box(rect, i % 2 == 0 ? new GColour("#182024") : new GColour("#131a1e"), new GColour("#2a3337"));
            DrawRect(new Rect2(x + 8, rect.Position.Y - 12, 12, 12), Line.Darkened(.4f));
            for (int row = 1; row < height / 24; row++)
            for (int col = 0; col < 3; col++)
                if ((row * 3 + col * 7 + i) % 5 == 0)
                    DrawRect(new Rect2(x + 8 + col * 12, rect.Position.Y + row * 24, 4, 8), Muted.Darkened(.65f));
        }
        float tx = bounds.Position.X + bounds.Size.X * .57f;
        DrawPolygon([new(tx - 86, bounds.End.Y), new(tx - 45, bounds.Position.Y + 86), new(tx + 66, bounds.Position.Y + 86), new(tx + 115, bounds.End.Y)],
            [new GColour("#202a2e")]);
        for (int i = 0; i < 16; i++) Rule(tx - 25, bounds.Position.Y + 122 + i * 28, 77, Line);
        DrawLine(new(tx + 12, bounds.Position.Y + 87), new(tx + 12, bounds.Position.Y + 4), Muted, 2);
        DrawCircle(new(tx + 12, bounds.Position.Y + 4), 3, Paper);
        for (int i = 0; i < 5; i++)
        {
            float y = bounds.End.Y - 25 + i * 5;
            Rule(bounds.Position.X, y, bounds.Size.X, Ink);
        }
        Tag("DISTRICT AUTHORITY / SECTOR 09", bounds.Position.X + 32, bounds.Position.Y + 36);
    }

    private void DrawCombat()
    {
        var battle = _run.Battle ?? throw new InvalidOperationException("Combat screen has no battle.");
        Text(_run.CurrentNode!.Name, 36, 125, 23, Paper, _bold);
        Text($"Round {battle.Round}  /  {battle.Enemies.Count} hostiles", 36, 153, 16, Muted);
        Text("Action points", 842, 122, 15, Muted);
        Text($"{battle.Ap}", 964, 125, 27, Paper, _mono);
        for (int i = 0; i < Math.Min(battle.Ap, 8); i++)
            DrawRect(new Rect2(843 + i * 17, 141, 11, 5), Paper);
        DrawBoard(battle);
        DrawHand(battle);
        DrawSupplyBelt(battle);
        DrawInspector(battle);
        if (_message.Length > 0)
            Paragraph(_message, 374, 626, 1023, 14, _messageError ? Warning : Paper, 1);
        else
            Paragraph(PhaseHint(battle), 374, 626, 1023, 13, Muted, 1);
        DrawCombatFooter(battle);
        DrawImpacts();
        DrawFlyingCards();
    }

    private static string PhaseHint(Combat battle) => battle.Phase switch
    {
        Phase.Cards => "SELECT A CARD, THEN ITS TARGET. UNTARGETED CARDS PLAY ON SELECTION.  [1-9] CARDS / [ESC] CANCEL",
        Phase.Movement => "SELECT A PIECE. DIAMONDS = LEGAL MOVES / TICKS = AUTOMATIC ATTACK. CLICK THE PIECE AGAIN TO HOLD.",
        Phase.Discard => $"SELECT {battle.RequiredDiscards} CARDS TO DISCARD. KEEP THE REST. ENEMIES RESOLVE AFTER CONFIRMATION.",
        Phase.Victory => "SECTOR SECURED. COLLECT YOUR REWARD AND CARRY YOUR REMAINING ORGANISATION HP FORWARD.",
        _ => "SIGNAL LOST. THE ORGANISATION HAS FALLEN."
    };

    private Vector2 CellPoint(Cell cell) => new Vector2(633 + (cell.X - cell.Y) * 63, 206 + (cell.X + cell.Y) * 31) + _boardImpulse;
    private Vector2[] Diamond(Cell cell, float inset = 0)
    {
        Vector2 p = CellPoint(cell);
        return [p + new Vector2(0, -31 + inset / 2), p + new Vector2(63 - inset, 0),
            p + new Vector2(0, 31 - inset / 2), p + new Vector2(-63 + inset, 0)];
    }
    private void Outline(Vector2[] points, GColour color, float width = 1)
        => DrawPolyline(points.Append(points[0]).ToArray(), color, width, true);

    private void DrawBoard(Combat battle)
    {
        HashSet<Cell> legal = new(), attacks = new();
        Troop? selected = battle.Troops.FirstOrDefault(t => t.Card.Id == _selectedTroop);
        if (_selectedSupply.HasValue && battle.Phase == Phase.Cards) legal = battle.SupplyTargets(_selectedSupply.Value);
        else if (_selectedCard.HasValue && battle.Phase == Phase.Cards) legal = battle.CardTargets(_selectedCard.Value);
        else if (selected is not null)
        {
            legal = battle.LegalMoves(selected);
            attacks = battle.AttackCells(selected.Kind, selected.Cell);
        }
        Enemy? inspected = HoveredEnemy(battle) ?? battle.Enemies.FirstOrDefault(e => e.Id == _selectedEnemy);
        var threats = inspected is not null ? battle.GetIntent(inspected).Cells : new HashSet<Cell>();
        var ordered = battle.Board.Cells.OrderBy(c => c.X + c.Y).ThenBy(c => c.X).ToArray();
        foreach (var cell in ordered)
        {
            var p = CellPoint(cell);
            var diamond = Diamond(cell);
            DrawPolygon(diamond.Select(v => v + new Vector2(0, 12)).ToArray(), [new GColour("#090d0f")]);
            Outline(diamond.Select(v => v + new Vector2(0, 12)).ToArray(), Line.Darkened(.55f));
            bool hover = Geometry2D.IsPointInPolygon(_mouse, diamond);
            GColour tile = (cell.X + cell.Y) % 2 == 0 ? new("#222c31") : new("#1c252a");
            if (battle.Board.Deployment.Contains(cell)) tile = tile.Lightened(.035f);
            if (hover) tile = tile.Lightened(.055f);
            DrawPolygon(diamond, [tile]);
            Outline(diamond, Line);
            if (battle.Board.Deployment.Contains(cell))
            {
                var inner = Diamond(cell, 9);
                DrawLine(inner[2], inner[2].Lerp(inner[1], .3f), Muted.Darkened(.25f), 2);
                DrawLine(inner[2], inner[2].Lerp(inner[3], .3f), Muted.Darkened(.25f), 2);
            }
            if (threats.Contains(cell))
            {
                DrawLine(p + new Vector2(22, -3), p + new Vector2(28, 3), Warning.Darkened(.12f), 1);
                DrawLine(p + new Vector2(22, 3), p + new Vector2(28, -3), Warning.Darkened(.12f), 1);
            }
            if (battle.Tiles.Any(t => t.Cell == cell && t.Kind == TileKind.Exposed))
            {
                DrawArc(p, 17, 0, Mathf.Tau, 24, Paper.Darkened(.25f), 1, true);
                DrawArc(p, 11, 0, Mathf.Tau, 24, Paper.Darkened(.25f), 1, true);
            }
            var special = battle.Tiles.FirstOrDefault(t => t.Cell == cell && t.Kind is TileKind.Trap or TileKind.Mine or TileKind.Acid or TileKind.EnemyHazard);
            if (special is not null)
            {
                GColour color = special.Kind == TileKind.EnemyHazard ? Warning : CategoryColor(CardKind.Board);
                DrawCategoryIcon(CardKind.Board, p, 11, color);
                Tag(special.Kind == TileKind.EnemyHazard ? "!" : special.Kind.ToString()[..1], p.X - 4, p.Y + 5, Paper);
            }
            if (battle.IsDenied(cell) && !battle.Board.Terrain.Contains(cell))
            {
                DrawLine(p + new Vector2(-20, -10), p + new Vector2(20, 10), Muted, 2);
                DrawLine(p + new Vector2(-20, 10), p + new Vector2(20, -10), Muted, 2);
            }
            if (legal.Contains(cell)) Outline(Diamond(cell, 6), Paper, 2);
            if (attacks.Contains(cell))
            {
                DrawLine(p + new Vector2(-12, -6), p + new Vector2(-12, 0), Paper, 2);
                DrawLine(p + new Vector2(12, 0), p + new Vector2(12, 6), Paper, 2);
            }
            if (hover) Tag(cell.ToString(), p.X - 8, p.Y + 18, Paper);
            _hotspots.Add(new Hotspot($"tile-{cell}", new Rect2(p - new Vector2(63, 31), new Vector2(126, 62)),
                () => ClickCell(cell), _lock <= 0 && battle.Phase is Phase.Cards or Phase.Movement, diamond));
        }
        foreach (var cell in ordered)
        {
            if (battle.Board.Terrain.Contains(cell)) DrawTerrain(CellPoint(cell));
            var troop = battle.Troops.FirstOrDefault(t => t.Cell == cell);
            var enemy = battle.Enemies.FirstOrDefault(e => e.Cell == cell);
            if (troop is not null)
            {
                DrawUnit(UnitPosition("t" + troop.Card.Id, cell), troop.Kind, false, troop.Hp, troop.MaxHp, troop.Block, troop.Card.Id == _selectedTroop, troop.MovesLeft, troop.Card.Upgraded);
                if (troop.MarkTurns > 0) Tag("MARK", CellPoint(cell).X - 14, CellPoint(cell).Y - 63, Warning);
                _hotspots.Add(new Hotspot($"piece-{troop.Card.Id}", new Rect2(CellPoint(cell) - new Vector2(19, 49), new Vector2(38, 60)),
                    () => ClickCell(cell), _lock <= 0 && battle.Phase is Phase.Cards or Phase.Movement));
            }
            if (enemy is not null)
            {
                DrawUnit(UnitPosition("e" + enemy.Id, cell), enemy.Definition.Role == EnemyRole.Shield ? PieceKind.Sentinel :
                    enemy.Definition.Role == EnemyRole.Sniper ? PieceKind.Marksman : enemy.Definition.Role == EnemyRole.Commander ? PieceKind.Breacher : PieceKind.Runner,
                    true, enemy.Hp, enemy.Definition.Hp, enemy.Block, enemy.Id == _selectedEnemy, 0, enemy.Definition == Content.Warden);
                DrawIntentIcon(battle, enemy);
                _hotspots.Add(new Hotspot($"hostile-{enemy.Id}", new Rect2(CellPoint(cell) - new Vector2(20, 49), new Vector2(40, 60)),
                    () => ClickCell(cell), _lock <= 0 && battle.Phase is Phase.Cards or Phase.Movement));
            }
        }
        for (int x = 0; x < 7; x++)
        {
            Vector2 p = CellPoint(new Cell(x, 5)) + new Vector2(-24, 37);
            Tag(((char)('A' + x)).ToString(), p.X, p.Y);
        }
        for (int y = 0; y < 6; y++)
        {
            Vector2 p = CellPoint(new Cell(6, y)) + new Vector2(65, 4);
            Tag((y + 1).ToString(), p.X, p.Y);
        }
        if (inspected is not null && battle.GetIntent(inspected).MoveTo is { } destination)
        {
            Vector2 from = CellPoint(inspected.Cell), to = CellPoint(destination);
            DrawLine(from, to, Warning, 2, true);
            Outline(Diamond(destination, 8), Warning, 2);
            Text("Move", to.X - 15, to.Y - 10, 13, Warning);
        }
        Text("V  Deployment     <>  Move     [ ]  Attack     X  Threat", 390, 589, 13, Muted);
        if (battle.Danger && _haze)
        {
            DrawRect(new Rect2(25, 166, 1000, 423), new GColour(.5f, .16f, .13f, .22f), false, 2);
        }
    }

    private void DrawTerrain(Vector2 p)
    {
        Vector2[] top = [p + new Vector2(0, -47), p + new Vector2(32, -30), p + new Vector2(0, -13), p + new Vector2(-32, -30)];
        DrawPolygon([top[3], top[2], p + new Vector2(0, 18), p + new Vector2(-32, 1)], [new GColour("#293237")]);
        DrawPolygon([top[2], top[1], p + new Vector2(32, 1), p + new Vector2(0, 18)], [new GColour("#151c20")]);
        DrawPolygon(top, [new GColour("#3a4346")]);
        Outline(top, Muted.Darkened(.42f));
        DrawLine(top[3] + new Vector2(9, 13), top[2] + new Vector2(-6, 7), Muted.Darkened(.55f), 3);
    }

    private void DrawUnit(Vector2 p, PieceKind kind, bool hostile, int hp, int maxHp, int block, bool selected, int moves, bool elite)
    {
        DrawSetTransform(_origin, 0, Vector2.One * _scale);
        DrawEllipse(p + new Vector2(0, 1), new Vector2(20, 9), new GColour(0, 0, 0, .48f));
        DrawEllipse(p + new Vector2(0, -2), new Vector2(18, 7), hostile ? new GColour("#373536") : new GColour("#697273"));
        if (selected) Outline([p + new Vector2(0, -13), p + new Vector2(24, -1), p + new Vector2(0, 11), p + new Vector2(-24, -1)], Paper, 2);
        GColour cloth = hostile ? new("#292e32") : new("#7f8988");
        GColour light = hostile ? new("#687273") : Paper;
        if (kind == PieceKind.Marksman)
        {
            DrawPolygon([p + new Vector2(-9, -32), p + new Vector2(6, -34), p + new Vector2(15, -6), p + new Vector2(-14, -5)], [cloth]);
            DrawLine(p + new Vector2(5, -25), p + new Vector2(22, -46), light.Darkened(.15f), 4, true);
            DrawLine(p + new Vector2(-6, -28), p + new Vector2(-11, -9), light.Darkened(.3f), 2);
        }
        else if (kind == PieceKind.Breacher)
        {
            DrawPolygon([p + new Vector2(-14, -31), p + new Vector2(14, -31), p + new Vector2(13, -9), p + new Vector2(-14, -9)], [cloth]);
            DrawPolygon([p + new Vector2(3, -31), p + new Vector2(21, -25), p + new Vector2(19, -7), p + new Vector2(3, -12)], [light.Darkened(.15f)]);
            DrawLine(p + new Vector2(8, -26), p + new Vector2(15, -23), cloth, 2);
        }
        else
        {
            DrawPolygon([p + new Vector2(-9, -30), p + new Vector2(7, -31), p + new Vector2(10, -10), p + new Vector2(-8, -10)], [cloth]);
            DrawLine(p + new Vector2(-3, -28), p + new Vector2(9, -22), light, 4);
            DrawLine(p + new Vector2(8, -21), p + new Vector2(18, -27), light.Darkened(.3f), 3);
        }
        DrawLine(p + new Vector2(-5, -11), p + new Vector2(-7, -3), cloth.Darkened(.3f), 5);
        DrawLine(p + new Vector2(5, -11), p + new Vector2(7, -3), cloth.Darkened(.3f), 5);
        DrawCircle(p + new Vector2(0, -37), 8, cloth.Darkened(.15f));
        if (hostile)
        {
            DrawRect(new Rect2(p + new Vector2(-8, -43), new Vector2(16, 7)), light);
            DrawLine(p + new Vector2(-4, -35), p + new Vector2(5, -35), Paper.Darkened(.35f), 2);
        }
        else
        {
            DrawPolygon([p + new Vector2(-8, -39), p + new Vector2(-1, -48), p + new Vector2(7, -39)], [light.Darkened(.2f)]);
            DrawLine(p + new Vector2(-3, -37), p + new Vector2(4, -37), Ink, 3);
        }
        DrawRect(new Rect2(p + new Vector2(-19, 12), new Vector2(38, 3)), Ink);
        DrawRect(new Rect2(p + new Vector2(-19, 12), new Vector2(38f * hp / maxHp, 3)), hostile ? Muted : Paper);
        Tag($"{hp}" + (block > 0 ? $" / B{block}" : ""), p.X - 12, p.Y + 28, Paper);
        if (!hostile && moves > 0)
            for (int i = 0; i < Math.Min(moves, 4); i++) DrawCircle(p + new Vector2(-16 + i * 6, -55), 1.8f, Paper);
        if (elite) Tag("+", p.X + 13, p.Y - 43, Paper);
    }

    private void DrawEllipse(Vector2 center, Vector2 radius, GColour color)
    {
        var points = Enumerable.Range(0, 32).Select(i =>
            center + new Vector2(Mathf.Cos(i * Mathf.Tau / 32) * radius.X, Mathf.Sin(i * Mathf.Tau / 32) * radius.Y)).ToArray();
        DrawPolygon(points, [color]);
    }

    private void DrawHand(Combat battle)
    {
        Rule(36, 643, 1368);
        int count = battle.Hand.Count;
        float width = count <= 6 ? 194 : (1368f - (count - 1) * 10) / Math.Max(count, 1);
        float start = (1440 - (width + 10) * count + 10) / 2;
        for (int i = 0; i < count; i++)
        {
            int id = battle.Hand[i];
            var card = battle.GetCard(id);
            var target = new Rect2(start + i * (width + 10), 661, width, 177);
            bool pointer = (_keyboardFocus ? _focus == "card-" + id : target.HasPoint(_mouse)) && _modal.Length == 0;
            var rect = AnimatedCardRect(id, target, pointer);
            bool selected = _selectedCard == id || _discards.Contains(id);
            bool hover = pointer;
            if (hover) _hoverCard = id;
            DrawCard(card, rect, selected, hover, battle.Phase == Phase.Cards && card.Definition.Cost > battle.Ap, i + 1);
            _hotspots.Add(new Hotspot("card-" + id, rect, () => ClickCard(id),
                _lock <= 0 && battle.Phase is Phase.Cards or Phase.Discard));
        }
        if (count == 0)
        {
            Tag("NO CARDS IN HAND", 634, 735);
            Text("Your deployed pieces still receive orders.", 559, 766, 16, Muted);
        }
    }

    private void DrawCard(Card card, Rect2 rect, bool selected = false, bool hover = false, bool expensive = false, int key = 0)
    {
        float x = rect.Position.X, y = rect.Position.Y, width = rect.Size.X;
        GColour category = CategoryColor(card.Definition.Kind);
        Box(rect, selected ? Raised.Lightened(.08f) : hover ? Raised : Panel, selected || hover ? category.Lightened(.25f) : category.Darkened(.4f));
        DrawRect(new Rect2(x, y, width, 29), new GColour(category, .19f));
        DrawRect(new Rect2(x, y, 3, rect.Size.Y), category);
        DrawCategoryIcon(card.Definition.Kind, new Vector2(x + 16, y + 15), 8, category);
        Text(card.Definition.Kind.ToString(), x + 31, y + 20, 12, category);
        DrawCircle(new Vector2(x + width - 21, y + 20), 11, expensive ? Line.Darkened(.3f) : Paper);
        Text(card.Definition.Cost.ToString(), x + width - 25, y + 25, 13, expensive ? Muted : Ink, _mono);
        Paragraph(card.Name, x + 13, y + 50, width - 25, width < 160 ? 16 : 19, Paper, 2, _bold);
        DrawCardStencil(card, new Vector2(x + width - 28, y + 102), 20, new GColour(category, .2f));
        Rule(x + 13, y + 81, width - 26, category.Darkened(.5f));
        string body = card.Definition.Effect switch
        {
            EffectKind.Deploy => $"{Content.Pieces[card.Definition.Piece!.Value].Hp + (card.Upgraded ? 2 : 0)} HP / {Content.Pieces[card.Definition.Piece.Value].Damage + (card.Upgraded ? 1 : 0)} ATK\n" +
                (card.Definition.Piece == PieceKind.Marksman ? "Forward column / range 4" :
                 card.Definition.Piece == PieceKind.Medic ? "Heal adjacent allies" :
                 card.Definition.Piece == PieceKind.Sentinel ? "Guard adjacent allies" : Content.Pieces[card.Definition.Piece.Value].AttackText),
            EffectKind.Network => $"{(card.Upgraded ? 10 : 7)} organisation block",
            EffectKind.Armour => $"{(card.Upgraded ? 10 : 7)} block to one troop",
            EffectKind.Intercept => $"{(card.Upgraded ? 7 : 4)} org + troop block",
            EffectKind.Recon => card.Upgraded ? "Reveal 2 rounds.\nMarksmen +3 ATK" : "Reveal this round.\nMarksmen +3 ATK",
            EffectKind.March => $"+{(card.Upgraded ? 2 : 1)} troop move.\nRunner: 2 moves = +3",
            EffectKind.Strike => $"+{(card.Upgraded ? 3 : 2)} troop ATK.\nExposed hits +3 more",
            EffectKind.Barricade => $"Deny an empty tile.\n{(card.Upgraded ? 3 : 2)} enemy phases",
            EffectKind.Exposed => $"Tile: +{(card.Upgraded ? 3 : 2)} damage taken.\n2 enemy phases",
            _ => Content.RulesText(card).Split('.')[0]
        };
        Paragraph(body, x + 13, y + 106, width - 26, 13, Muted, 3);
        Rule(x + 13, y + rect.Size.Y - 32, width - 26);
        string footer = selected && _run.Battle?.Phase == Phase.Discard ? "DISCARD [X]" : card.Definition.Target == TargetKind.None ? "INSTANT" :
            card.Definition.Target == TargetKind.Deployment ? "DEPLOY" : card.Definition.Target == TargetKind.Troop ? "TARGET TROOP" :
            card.Definition.Target == TargetKind.Enemy ? "TARGET ENEMY" : "TARGET TILE";
        if (card.Definition.Exhaust) footer = "EXHAUST";
        Tag(footer, x + 13, y + rect.Size.Y - 13, selected ? Paper : category);
        if (key > 0) Tag($"[{key}]", x + width - 33, y + rect.Size.Y - 13);
    }

    private void DrawCombatFooter(Combat battle)
    {
        Button("draw-pile", $"DRAW {battle.DrawPile.Count:00}", new Rect2(36, 855, 111, 31), () => OpenDeck("DRAW"));
        Button("discard-pile", $"DISCARD {battle.DiscardPile.Count:00}", new Rect2(157, 855, 121, 31), () => OpenDeck("DISCARD"));
        Button("lost-pile", $"LOST {battle.Zones.Count(z => z.Value == Zone.Unavailable):00}", new Rect2(288, 855, 100, 31), () => OpenDeck("LOST"));
        Button("exhausted-pile", $"EXHAUST {battle.Zones.Count(z => z.Value == Zone.Exhausted):00}", new Rect2(398, 855, 124, 31), () => OpenDeck("EXHAUSTED"));
        Text($"Hand {battle.Hand.Count}/9", 545, 877, 15, Muted);
        Button("combat-log", "LOG", new Rect2(672, 855, 68, 31), () => OpenModal("log"));
        string label = battle.Phase switch
        {
            Phase.Cards => "COMMIT CARDS / ISSUE ORDERS  >",
            Phase.Movement => "HOLD REMAINING / ATTACK  >",
            Phase.Discard => $"DISCARD {_discards.Count}/{battle.RequiredDiscards} / ENEMY TURN  >",
            Phase.Victory => "CLAIM SECTOR  >",
            _ => "VIEW FINAL REPORT  >"
        };
        Button("phase", label, new Rect2(1062, 850, 342, 39), AdvancePhase,
            battle.Phase != Phase.Discard || _discards.Count == battle.RequiredDiscards, true);
        if (battle.Phase == Phase.Cards && battle.Ap > 0) Tag($"{battle.Ap} UNSPENT AP", 922, 875);
        if (battle.Phase == Phase.Movement) Tag($"{battle.Troops.Sum(t => t.MovesLeft)} MOVES LEFT", 922, 875);
    }

    private void DrawImpacts()
    {
        foreach (var effect in _effects)
        {
            float age = _effectClock - effect.Start;
            if (age < 0 || age > 1.8f) continue;
            var impact = effect.Impact;
            Vector2 from = CellPoint(impact.From) + new Vector2(0, -26);
            Vector2 to = CellPoint(impact.To) + new Vector2(0, -25);
            float alpha = Mathf.Clamp(1 - (age - .6f) / 1.2f, 0, 1);
            bool combo = impact.Combos.Length > 0;
            if (combo)
            {
                if (!_reduced)
                {
                    DrawLine(from, to, new GColour(Cyan, alpha * .1f), 14, true);
                    DrawLine(from, to, new GColour(Cyan, alpha * .7f), 2, true);
                    DrawArc(to, 18 + age * 33, 0, Mathf.Tau, 48, new GColour(Amber, alpha * .65f), 2, true);
                    for (int i = 0; i < 9; i++)
                    {
                        float angle = i * Mathf.Tau / 9;
                        Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                        DrawLine(to + direction * (20 + age * 34), to + direction * (25 + age * 42),
                            new GColour(i % 2 == 0 ? Cyan : Amber, alpha), 2, true);
                    }
                }
                else Outline(Diamond(impact.To, 3), new GColour(Cyan, alpha), 2);
            }
            Text($"-{impact.Damage}", to.X + 14, to.Y - 20 - (_reduced ? 0 : age * 13), combo ? 27 : 17,
                new GColour(combo ? Amber : Paper, alpha), _bold);
        }
        var latest = _effects.LastOrDefault(e => e.Impact.Combos.Length > 0 && _effectClock >= e.Start);
        if (latest is not null)
        {
            float alpha = Mathf.Clamp(1 - (_effectClock - latest.Start - .8f), 0, 1);
            Box(new Rect2(411, 138, 658, 60), new GColour(Ink, .96f), new GColour(Cyan, alpha * .65f));
            Tag(latest.Impact.Hostile ? "HOSTILE COMBINATION" : "LINK CONFIRMED", 428, 158, new GColour(latest.Impact.Hostile ? Amber : Cyan, alpha));
            Text(string.Join(" + ", latest.Impact.Combos), 428, 184, 23, new GColour(Paper, alpha), _bold);
            Text($"{latest.Impact.Damage} DMG", 956, 177, 20, new GColour(Amber, alpha), _mono);
        }
    }

    private void DrawReward()
    {
        Tag("ENCOUNTER COMPLETE / REQUISITION", 80, 155);
        Text("SALVAGE A NEW POSSIBILITY.", 76, 215, 44, Paper, _bold);
        Paragraph(_run.RewardNote, 80, 250, 1250, 16, Muted, 3);
        for (int i = 0; i < _run.Rewards.Count; i++)
        {
            var definition = _run.Rewards[i];
            float x = 204 + i * 364;
            DrawCard(new Card(0, definition), new Rect2(x, 316, 302, 232));
            Paragraph(definition.Synergy, x + 4, 581, 293, 17, Paper, 3);
            Button("reward-" + definition.Id, "ADD TO DECK  >", new Rect2(x, 652, 302, 47),
                () => { Act(_run.ClaimReward(definition.Id)); ClearSelection(); }, primary: true);
        }
        Button("skip-reward", "SKIP / KEEP CURRENT DECK", new Rect2(524, 779, 392, 44),
            () => { Act(_run.ClaimReward(null)); ClearSelection(); });
        Tag($"ORGANISATION {_run.Hp}/{RunState.MaxHp}   /   TROOPS RETURN AT FULL HP NEXT FIGHT", 80, 868);
    }

    private void DrawRelay(Vector2 p)
    {
        Box(new Rect2(p - new Vector2(70, 85), new Vector2(130, 145)), Raised, Muted);
        Box(new Rect2(p - new Vector2(50, 64), new Vector2(91, 56)), Ink, Line);
        for (int i = 0; i < 7; i++) Rule(p.X - 45, p.Y + 4 + i * 6, 51, Line);
        DrawLine(p + new Vector2(-37, -85), p + new Vector2(-37, -145), Muted, 3);
        DrawCircle(p + new Vector2(22, 29), 9, Line);
        Tag("NO CARRIER", p.X - 42, p.Y - 30);
    }

    private void DrawEnd(bool victory)
    {
        DrawCity(new Rect2(830, 190, 570, 540));
        DrawRect(new Rect2(0, 90, 820, 728), Ink);
        Tag(victory ? "CENTRAL COMMAND / CITY LIBERATED" : "TRANSMISSION ENDED / ORGANISATION LOST", 76, 208);
        Text(victory ? "THE SILENCE" : "NO SIGNAL.", 70, 300, 76, Paper, _bold);
        if (victory) Text("BREAKS.", 70, 379, 76, Paper, _bold);
        Paragraph(victory ? "The Warden is down. For one night, the district belongs to its people.\nThe resistance survives." :
            "The city folds back into its routine.\nYour combinations were not enough. This time.", 76, 446, 644, 23, Muted);
        Rule(76, 548, 635);
        Text($"{_run.TotalCombos:00}", 76, 607, 43, Paper, _mono);
        Tag("COMBO HITS", 76, 632);
        Text($"{_run.Victories:00}", 287, 607, 43, Paper, _mono);
        Tag("SECTORS SECURED", 287, 632);
        Text($"{_run.Hp:00}", 531, 607, 43, Paper, _mono);
        Tag(victory ? "ORGANISATION SURVIVED" : "ORGANISATION HP", 531, 632);
        Button("new-run", "ESTABLISH A NEW SIGNAL  >", new Rect2(76, 712, 444, 54), Restart, primary: true);
        Tag("TWO ACTS + CENTRAL FINALE / FUTURE DISTRICTS WILL EXPAND THE CITY", 76, 844);
    }

    private void DrawGuide()
    {
        Box(new Rect2(80, 98, 1280, 712), Ink, Muted);
        Tag("RESISTANCE / FIELD MANUAL 001", 110, 139);
        Text("BUILD THE CONNECTION. THEN COMMIT.", 106, 188, 38, Paper, _bold);
        Rule(110, 212, 1220);
        Tag("THE ROUND", 110, 247);
        Paragraph("1  CARDS / Draw 5. Spend 3 AP.\n2  ORDERS / Move or hold each piece.\n3  ATTACK / All covered enemies are hit.\n4  DISCARD / Choose 2. Retain the rest.\n5  ENEMIES / Fixed, repeating routines.", 110, 281, 390, 17, Paper);
        Tag("TWO DIFFERENT THINGS TO PROTECT", 110, 435);
        Paragraph("Every enemy attack hits organisation HP, even with no troop nearby. Troops in its pattern take the same base attack separately.\n\nEach recipient spends its own block. Organisation and troop block expire after enemies. Enemy guard persists until consumed.", 110, 466, 382, 16, Muted, 9);

        Tag("CONNECTIONS, NOT JUST TROOPS", 544, 247);
        Text("01 / Precision & movement", 544, 283, 21, Paper, _bold);
        Paragraph("Recon + Marksman: Ghost Signal +3. Consume a mark: Breach +3. Forced March + Runner moving twice: Momentum +3.", 544, 312, 344, 16, Muted, 4);
        Text("02 / Status & terrain", 544, 412, 21, Paper, _bold);
        Paragraph("Corrosion + Detonate: Meltdown. Push or pull into traps: Dead End. Strike + exposed target: Kill Circuit +3 on top of both bonuses.", 544, 441, 344, 16, Muted, 4);
        Text("03 / Build your engine", 544, 541, 21, Paper, _bold);
        Paragraph("Command immediate attacks, reinforce formations, or amplify supplies. Exhausted cards return next fight. Different combinations stack.", 544, 570, 344, 16, Muted, 4);

        Tag("DISRUPT THE SYSTEM", 937, 247);
        Paragraph("Enemy movement commits at round start. Recon reveals power and destinations; proximity within 2 Manhattan cells also reveals intent.\n\nJam support units, break shield formations and deny reinforcement tiles.", 937, 281, 365, 16, Paper, 9);
        Paragraph("Carry 3 supplies. Use one free per round during card play; logistics can grant more. Relics last this run.\n\nF5 saves to this run's slot. Load restarts the current room. Defeat deletes the slot. Save manually before leaving.", 937, 493, 365, 15, Muted, 8);
        Rule(110, 686, 1220);
        Tag("[1-9] CARDS  [SPACE] ADVANCE  [ESC / RIGHT CLICK] CANCEL  [TAB / ENTER] KEYBOARD SELECT", 110, 714);
        Button("guide-haze", _haze ? "THREAT HAZE ON" : "THREAT HAZE OFF", new Rect2(110, 744, 210, 38), ToggleHaze);
        Button("guide-motion", _reduced ? "REDUCED MOTION" : "FULL MOTION", new Rect2(334, 744, 210, 38), ToggleMotion);
        Button("guide-sound", _muted ? "SOUND OFF" : "SOUND ON", new Rect2(558, 744, 166, 38), ToggleMute);
        Button("close-guide", "UNDERSTOOD  >", new Rect2(1041, 742, 289, 41), () => _modal = "", primary: true);
    }

    private void DrawDeck()
    {
        Box(new Rect2(50, 85, 1340, 783), Ink, Muted);
        Tag($"ARCHIVE / {_deckZone} CARDS", 76, 122);
        Text(_deckZone == "RUN" ? "YOUR RESISTANCE, CARD BY CARD." : "ENCOUNTER CARD ZONES", 72, 163, 35, Paper, _bold);
        if (_deckZone == "DRAW") Tag("SORTED CATALOGUE / DRAW ORDER IS NOT REVEALED", 76, 196);
        else Tag("UPGRADES PERSIST. IN-PLAY AND LOST TROOPS NEVER ENTER THE DRAW PILE.", 76, 196);
        IEnumerable<Card> cards = _run.Deck;
        if (_run.Battle is { } battle && _deckZone != "RUN")
        {
            Zone zone = _deckZone switch { "DRAW" => Zone.Draw, "DISCARD" => Zone.Discard, "LOST" => Zone.Unavailable, "EXHAUSTED" => Zone.Exhausted, _ => Zone.InPlay };
            cards = battle.Zones.Where(z => z.Value == zone).Select(z => battle.GetCard(z.Key)).OrderBy(c => c.Definition.Name).ThenBy(c => c.Id);
        }
        DrawDeckGrid(cards.ToArray(), 222);
        Button("close-deck", "CLOSE ARCHIVE", new Rect2(1110, 807, 252, 40), () => _modal = "", primary: true);
    }

    private void DrawDeckGrid(IReadOnlyList<Card> cards, float top)
    {
        const int pageSize = 12;
        int pages = Math.Max(1, (cards.Count + pageSize - 1) / pageSize);
        _deckPage = Math.Clamp(_deckPage, 0, pages - 1);
        for (int i = 0; i < Math.Min(pageSize, cards.Count - _deckPage * pageSize); i++)
        {
            var card = cards[_deckPage * pageSize + i];
            float x = 77 + i % 6 * 216;
            float y = top + i / 6 * 252;
            DrawCard(card, new Rect2(x, y, 202, 188));
        }
        if (cards.Count == 0) Text("No cards in this zone.", 77, top + 70, 23, Muted);
        const float footer = 807;
        Button("deck-prev", "<", new Rect2(750, footer, 44, 40), () => _deckPage--, _deckPage > 0);
        Tag($"PAGE {_deckPage + 1}/{pages} / {cards.Count} CARDS", 810, footer + 25);
        Button("deck-next", ">", new Rect2(1007, footer, 44, 40), () => _deckPage++, _deckPage + 1 < pages);
    }

    private void DrawRestart()
    {
        Box(new Rect2(424, 284, 592, 291), Ink, Muted);
        Tag("CONFIRM / ABANDON RUN", 455, 326);
        Text("Break this connection?", 455, 373, 32, Paper, _bold);
        Paragraph("Save manually to resume from this room's entrance.\nEach run owns one slot. Defeat deletes that slot.", 455, 418, 522, 17, Muted);
        Button("menu-save", "SAVE RUN", new Rect2(455, 456, 155, 32), SaveRun);
        Button("menu-load", "LOAD RUN", new Rect2(623, 456, 155, 32), OpenSaves);
        Button("cancel-restart", "KEEP PLAYING", new Rect2(455, 502, 250, 43), () => _modal = "", primary: true);
        Button("confirm-restart", "RETURN TO TITLE", new Rect2(721, 502, 263, 43), () => { Restart(); _title = true; });
    }

    private void DrawLog()
    {
        Box(new Rect2(190, 95, 1060, 720), Ink, Muted);
        Tag("ENCOUNTER / AFTER-ACTION RECORD", 222, 137);
        Text("EVERY CONSEQUENCE, ACCOUNTED FOR.", 218, 181, 32, Paper, _bold);
        Rule(222, 205, 996);
        int row = 0;
        foreach (string entry in _run.Battle?.Log.TakeLast(21) ?? Enumerable.Empty<string>())
            Text(entry, 222, 237 + row++ * 23, 14, Muted, _mono);
        Button("close-log", "CLOSE RECORD", new Rect2(935, 749, 283, 41), () => _modal = "", primary: true);
    }

    private void EnterNode(string id)
    {
        if (!Act(_run.Enter(id))) return;
        _effects.Clear();
        _cardMotions.Clear();
        _unitMotions.Clear();
        _flying.Clear();
        ClearSelection();
        _message = _run.Screen == RunScreen.Combat ? "Every enemy attack threatens organisation HP. Deploy, disrupt, then connect." : "";
        if (_run.Screen == RunScreen.Combat) NotifyTurn("Your turn / Round 1", "Card play is open. Enemy plans are committed.");
    }

    private bool Act(CommandResult result)
    {
        _message = result.Message;
        _messageError = !result.Ok;
        if (!result.Ok) GD.Print($"[Command rejected] {result.Message}");
        return result.Ok;
    }

    private void ClickCard(int id)
    {
        if (_run.Battle is not { } battle) return;
        if (battle.Phase == Phase.Discard)
        {
            if (!_discards.Remove(id))
            {
                if (_discards.Count >= battle.RequiredDiscards)
                { Act(CommandResult.Reject("Deselect a card before choosing another discard.")); return; }
                _discards.Add(id);
            }
            _message = "";
            return;
        }
        if (battle.Phase != Phase.Cards) { Act(CommandResult.Reject("Card play is closed. Issue troop orders first.")); return; }
        var card = battle.GetCard(id);
        if (card.Definition.Cost > battle.Ap) { Act(CommandResult.Reject($"Need {card.Definition.Cost} AP to play {card.Name}.")); return; }
        if (_selectedCard == id) { _selectedCard = null; _message = ""; return; }
        _selectedTroop = null;
        _selectedEnemy = null;
        _selectedSupply = null;
        if (card.Definition.Target == TargetKind.None)
        {
            PlayCard(id);
        }
        else
        {
            _selectedCard = id;
            _messageError = false;
            _message = battle.CardTargets(id).Count == 0 ? "No legal targets. Cancel and deploy a troop first, or choose another card." :
                $"{card.Name} selected / choose a highlighted {(card.Definition.Target == TargetKind.Troop ? "troop" : "tile")}.";
        }
    }

    private void ClickCell(Cell cell)
    {
        if (_run.Battle is not { } battle) return;
        if (_selectedSupply.HasValue && battle.Phase == Phase.Cards)
        {
            UseSelectedSupply(cell);
            return;
        }
        if (_selectedCard.HasValue && battle.Phase == Phase.Cards)
        {
            PlayCard(_selectedCard.Value, cell);
            return;
        }
        var troop = battle.Troops.FirstOrDefault(t => t.Cell == cell);
        if (_selectedTroop.HasValue && battle.Phase == Phase.Movement)
        {
            if (troop?.Card.Id == _selectedTroop) { Act(battle.Hold(_selectedTroop.Value)); return; }
            if (troop is null)
            {
                var before = CapturePositions(battle);
                if (Act(battle.Move(_selectedTroop.Value, cell)))
                {
                    AnimatePositions(battle, before);
                    if (battle.Impacts.Count > 0) AnimateImpacts(battle.Impacts);
                    return;
                }
            }
        }
        if (troop is not null)
        {
            _selectedTroop = troop.Card.Id;
            _selectedEnemy = null;
            _message = "";
            return;
        }
        var enemy = battle.Enemies.FirstOrDefault(e => e.Cell == cell);
        if (enemy is not null) { _selectedEnemy = enemy.Id; _selectedTroop = null; }
    }

    private void AdvancePhase()
    {
        if (_lock > 0 || _modal.Length > 0 || _run.Battle is not { } battle) return;
        bool ok;
        switch (battle.Phase)
        {
            case Phase.Cards:
                ok = Act(battle.BeginMovement());
                if (ok) NotifyTurn("Board orders", "Move your pieces, then commit their attacks.");
                break;
            case Phase.Movement:
                ok = Act(battle.ResolveTroops());
                if (ok) AnimateImpacts(battle.Impacts);
                break;
            case Phase.Discard:
                var positions = CapturePositions(battle);
                FlyDiscards(battle);
                ok = Act(battle.ConfirmDiscard(_discards));
                if (ok)
                {
                    AnimatePositions(battle, positions);
                    AnimateImpacts(battle.Impacts);
                    _lock = Math.Max(_lock, 1.05f);
                    NotifyTurn("Enemy turn", "Committed plans resolve. Your next hand is being dealt.", $"Your turn / Round {battle.Round}");
                    if (battle.Phase == Phase.Defeat) DeleteFinishedSave();
                }
                break;
            case Phase.Victory:
            case Phase.Defeat:
                ok = Act(_run.FinishBattle());
                if (ok && _run.Screen == RunScreen.Defeat) DeleteFinishedSave();
                break;
            default:
                throw new InvalidOperationException("No phase command.");
        }
        if (ok) ClearSelection();
    }

    private void AnimateImpacts(IReadOnlyList<Impact> impacts)
    {
        _effects.Clear();
        _effectClock = 0;
        for (int i = 0; i < impacts.Count; i++)
            _effects.Add(new VisualImpact(impacts[i], _reduced ? 0 : i * .12f));
        bool combo = impacts.Any(i => i.Combos.Length > 0);
        _lock = combo ? 1.15f + impacts.Count * .12f : .25f;
        if (combo && !_muted) _audio.Play();
    }

    private void ClearSelection()
    {
        _selectedCard = null;
        _selectedTroop = null;
        _selectedEnemy = null;
        _selectedSupply = null;
        _discards.Clear();
        _focus = null;
    }

    private void Restart()
    {
        _run = new RunState(NewSeed());
        _title = false;
        _modal = "";
        _message = "";
        _messageError = false;
        _effects.Clear();
        _audio.Stop();
        _lock = 0;
        _deckPage = 0;
        _cardMotions.Clear();
        _flying.Clear();
        _unitMotions.Clear();
        _deletedRun = null;
        ClearSelection();
    }

    private void OpenModal(string modal) { _modal = modal; _focus = null; _keyboardFocus = false; }
    private void OpenDeck(string zone) { _deckZone = zone; _deckPage = 0; OpenModal("deck"); }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true } mouse) return;
        if (mouse.ButtonIndex == MouseButton.Right) { Cancel(); AcceptEvent(); return; }
        if (mouse.ButtonIndex != MouseButton.Left) return;
        _keyboardFocus = false;
        var point = (mouse.Position - _origin) / _scale;
        var hit = _hotspots.LastOrDefault(h => h.Enabled && h.Contains(point));
        if (hit is not null) { _focus = hit.Id; hit.Click(); AcceptEvent(); QueueRedraw(); }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.Escape) { Cancel(); GetViewport().SetInputAsHandled(); return; }
        if (key.Keycode == Key.Tab)
        {
            var enabled = _hotspots.Where(h => h.Enabled).ToArray();
            if (enabled.Length > 0)
            {
                int index = Array.FindIndex(enabled, h => h.Id == _focus);
                index = (index + (key.ShiftPressed ? -1 : 1) + enabled.Length) % enabled.Length;
                _focus = enabled[index].Id;
                _keyboardFocus = true;
            }
        }
        else if (key.Keycode == Key.Enter || key.Keycode == Key.KpEnter)
            _hotspots.FirstOrDefault(h => h.Enabled && h.Id == _focus)?.Click();
        else if (key.Keycode == Key.M) ToggleMute();
        else if (key.Keycode == Key.R) ToggleMotion();
        else if (key.Keycode == Key.H) OpenModal("guide");
        else if (key.Keycode == Key.F5) SaveRun();
        else if (_modal.Length == 0 && _lock <= 0)
        {
            if (key.Keycode == Key.Space) AdvancePhase();
            int index = (int)key.Keycode - (int)Key.Key1;
            if (index >= 0 && index < 9 && _run.Battle is { } battle && index < battle.Hand.Count)
                ClickCard(battle.Hand[index]);
        }
        GetViewport().SetInputAsHandled();
    }

    private void Cancel()
    {
        if (_modal.Length > 0) { _modal = ""; return; }
        _selectedCard = null;
        _selectedTroop = null;
        _selectedEnemy = null;
        _selectedSupply = null;
        _discards.Clear();
        _message = "";
    }

    private void ToggleMute() { _muted = !_muted; if (_muted) _audio.Stop(); SaveSettings(); }
    private void ToggleMotion() { _reduced = !_reduced; SaveSettings(); }
    private void ToggleHaze() { _haze = !_haze; SaveSettings(); }
    private void LoadSettings()
    {
        var config = new ConfigFile();
        var error = config.Load("user://settings.cfg");
        if (error == Error.FileNotFound) return;
        if (error != Error.Ok) { GD.PushError($"Cannot load settings: {error}"); _message = "Settings could not be loaded."; return; }
        _muted = config.GetValue("accessibility", "muted", false).AsBool();
        _reduced = config.GetValue("accessibility", "reduced_motion", false).AsBool();
        _haze = config.GetValue("accessibility", "threat_haze", true).AsBool();
    }

    private void SaveSettings()
    {
        var config = new ConfigFile();
        config.SetValue("accessibility", "muted", _muted);
        config.SetValue("accessibility", "reduced_motion", _reduced);
        config.SetValue("accessibility", "threat_haze", _haze);
        var error = config.Save("user://settings.cfg");
        if (error != Error.Ok) { GD.PushError($"Cannot save settings: {error}"); Act(CommandResult.Reject("Settings could not be saved.")); }
    }

    private static AudioStreamWav CreateComboSound()
    {
        const int rate = 22050;
        var data = new byte[(int)(rate * .34) * 2];
        for (int i = 0; i < data.Length / 2; i++)
        {
            double t = (double)i / rate;
            double envelope = Math.Min(1, t * 180) * Math.Exp(-t * 14);
            double signal = Math.Sin(Math.Tau * (180 * t + 450 * t * t)) * .48 +
                Math.Sin(Math.Tau * 880 * t) * .22 + Math.Sin(Math.Tau * 1320 * t) * .12;
            short sample = (short)(signal * envelope * short.MaxValue * .65);
            data[i * 2] = (byte)(sample & 255);
            data[i * 2 + 1] = (byte)((sample >> 8) & 255);
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = rate, Stereo = false, Data = data };
    }

    private void StartVisualSmoke()
    {
        _muted = true;
        _reduced = _smokeScreen == "reduced";
        _haze = true;
        int chosenSeed = 1;
        for (int seed = 1; seed <= 100; seed++)
        {
            var candidate = new RunState(seed);
            candidate.StartAct(1);
            candidate.Enter("entry");
            string first = _smokeScreen == "action" ? "mark" : "marksman";
            string second = _smokeScreen == "action" ? "pulse" : "recon";
            if (candidate.Battle!.Hand.Any(id => candidate.Battle.GetCard(id).Definition.Id == first) &&
                candidate.Battle.Hand.Any(id => candidate.Battle.GetCard(id).Definition.Id == second))
            { chosenSeed = seed; break; }
        }
        _run = new RunState(chosenSeed);
        if (_smokeScreen == "saves")
        {
            _smokeSaveDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FutureDark-native-" + Guid.NewGuid().ToString("N"));
            _saves = new RunSaveStore(_smokeSaveDirectory);
        }
        _nextSmokeStep = .5f;
        void Step(Action action, float delay = .32f, bool duringLock = false) => _smokeSteps.Enqueue((action, delay, duringLock));
        bool keyboard = _smokeScreen == "keyboard";
        void Activate(string id, float delay = .32f)
        {
            if (keyboard) { Step(() => SmokeFocus(id)); Step(() => SmokeKey(Key.Enter), delay); }
            else Step(() => SmokeClick(id), delay);
        }
        if (_smokeScreen == "title") { Step(FinishVisualSmoke); return; }
        Activate("begin");
        if (_smokeScreen == "city") { Step(FinishVisualSmoke); return; }
        if (_smokeScreen == "liberated")
        {
            Step(() =>
            {
                _run.StartAct(1);
                _run = RunState.Restore(_run.SnapshotForSave() with { Act = 2, Screen = RunScreen.City, CompletedActs = [1, 2] });
            });
            Step(FinishVisualSmoke);
            return;
        }
        if (_smokeScreen is "vendor" or "event" or "act2" or "inventory")
        {
            Step(() =>
            {
                _run.StartAct(1);
                int act = _smokeScreen == "act2" ? 2 : 1;
                var snapshot = _run.SnapshotForSave() with
                {
                    Act = act, CompletedActs = act == 2 ? [1] : [], Funds = 220,
                    Relics = ["capacitor", "plating", "catalyst"], Supplies = ["shield", "emp", "acid-vial"]
                };
                var room = RunState.GenerateAct(_run.Seed, act).First(n => n.Kind == (_smokeScreen == "vendor" ? RoomKind.Vendor : RoomKind.Event));
                _run = RunState.Restore(snapshot with { Available = [room.Id] });
                if (_smokeScreen != "act2") _run.Enter(room.Id);
                if (_smokeScreen == "inventory") OpenModal("inventory");
            }, .6f);
            Step(FinishVisualSmoke);
            return;
        }
        Activate("district-1");
        if (_smokeScreen == "map") { Step(FinishVisualSmoke); return; }
        if (_smokeScreen == "deck")
        {
            Activate("map-deck");
            Step(FinishVisualSmoke);
            return;
        }
        Activate("node-entry");
        Activate("room-enter", .65f);
        if (_smokeScreen == "saves")
        {
            Activate("save");
            Activate("supply-0");
            Step(() => SmokeKey(Key.F5));
            Activate("restart");
            Activate("menu-load");
            Step(() => SmokeClick("load-" + _run.RunId));
            Step(() =>
            {
                if (!SmokeCheck(_run.Battle?.Round == 1 && _run.Supplies.Count == 1 && _saves.List().Count == 1,
                    "Native manual save/load did not restore the room entrance to a single slot.")) return;
                var battle = _run.Battle!;
                for (int i = 0; i < 50 && battle.Phase != Phase.Defeat; i++)
                {
                    battle.BeginMovement();
                    battle.ResolveTroops();
                    battle.ConfirmDiscard(battle.Hand.Take(battle.RequiredDiscards).ToArray());
                }
            });
            Step(FinishVisualSmoke);
            return;
        }
        if (_smokeScreen == "combat") { Step(FinishVisualSmoke); return; }
        if (_smokeScreen == "guide")
        {
            Activate("guide");
            Step(FinishVisualSmoke);
            return;
        }
        if (_smokeScreen == "dense")
        {
            Step(() => _run.Battle!.DrawCards(12), .7f);
            Step(FinishVisualSmoke);
            return;
        }
        if (_smokeScreen == "action")
        {
            Step(() => SmokeClick("card-" + _run.Battle!.Hand.First(id => _run.Battle.GetCard(id).Definition.Id == "mark")));
            Activate("tile-D2");
            Step(() => SmokeClick("card-" + _run.Battle!.Hand.First(id => _run.Battle.GetCard(id).Definition.Id == "pulse")));
            Activate("tile-D2", .2f);
            Step(FinishVisualSmoke, duringLock: true);
            return;
        }
        Step(() =>
        {
            var battle = _run.Battle!;
            string id = "card-" + battle.Hand.First(id => battle.GetCard(id).Definition.Id == "marksman");
            if (keyboard) SmokeFocus(id); else SmokeClick(id);
        });
        if (keyboard) Step(() => SmokeKey(Key.Enter));
        Activate("tile-D5");
        if (keyboard) { Step(FinishVisualSmoke); return; }
        Step(() => SmokeClick("piece-" + _run.Battle!.Troops.Single().Card.Id));
        Step(() =>
        {
            if (!SmokeCheck(_selectedTroop == _run.Battle!.Troops.Single().Card.Id, "Piece hitbox did not select its troop.")) return;
            SmokeClick("card-" + _run.Battle.Hand.First(id => _run.Battle.GetCard(id).Definition.Id == "recon"));
        });
        if (_smokeScreen == "intel")
        {
            Step(() => SmokeClick("intent-" + _run.Battle!.Enemies[0].Id));
            Step(FinishVisualSmoke);
            return;
        }
        Step(() => SmokeKey(Key.Space));
        Step(() => SmokeKey(Key.Space), .22f);
        if (_smokeScreen == "enemyturn")
        {
            Step(() => SmokeClick("card-" + _run.Battle!.Hand[0]));
            Step(() => SmokeClick("card-" + _run.Battle!.Hand[1]));
            Step(() => SmokeKey(Key.Space), .25f);
        }
        Step(FinishVisualSmoke, duringLock: true);
    }

    private void StepVisualSmoke(int frame)
    {
        if (_uiTime > 25) { SmokeCheck(false, "Scenario timed out."); return; }
        if (_smokeSteps.Count == 0 || _uiTime < _nextSmokeStep) return;
        if (_lock > 0 && !_smokeSteps.Peek().DuringLock) { _nextSmokeStep = _uiTime + .08f; return; }
        var step = _smokeSteps.Dequeue();
        step.Action();
        _nextSmokeStep = _uiTime + step.Delay;
    }

    private void SmokeClick(string id)
    {
        var hotspot = _hotspots.SingleOrDefault(h => h.Id == id && h.Enabled);
        if (!SmokeCheck(hotspot is not null, $"Missing pointer target {id}.")) return;
        Vector2 local = _origin + hotspot!.Rect.GetCenter() * _scale;
        // Injected input needs the viewport's screen transform, unlike local _GuiInput events.
        Vector2 point = GetViewport().GetScreenTransform() * GetGlobalTransformWithCanvas() * local;
        Input.ParseInputEvent(new InputEventMouseButton
        { ButtonIndex = MouseButton.Left, Pressed = true, Position = point, GlobalPosition = point });
        Input.ParseInputEvent(new InputEventMouseButton
        { ButtonIndex = MouseButton.Left, Pressed = false, Position = point, GlobalPosition = point });
    }

    private bool SmokeCheck(bool condition, string message)
    {
        if (condition) return true;
        GD.PushError("VISUAL_SMOKE: " + message);
        _smoke = false;
        GetTree().Quit(1);
        return false;
    }

    private static void SmokeKey(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = false });
    }

    private void SmokeFocus(string id)
    {
        var controls = _hotspots.Where(h => h.Enabled).ToArray();
        int current = Array.FindIndex(controls, h => h.Id == _focus);
        int target = Array.FindIndex(controls, h => h.Id == id);
        if (!SmokeCheck(target >= 0, $"Missing keyboard target {id} (title={_title}, screen={_run.Screen}).")) return;
        int steps = current < 0 ? target + 1 : (target - current + controls.Length) % controls.Length;
        for (int i = 0; i < steps; i++) SmokeKey(Key.Tab);
    }

    private async void FinishVisualSmoke()
    {
        bool valid = _smokeScreen switch
        {
            "title" => _title,
            "city" => !_title && _run.Screen == RunScreen.City,
            "liberated" => _run.Screen == RunScreen.City && _run.CentreUnlocked,
            "map" => !_title && _run.Screen == RunScreen.Map,
            "guide" => _modal == "guide",
            "deck" => _modal == "deck",
            "dense" => _run.Battle?.Hand.Count == 9,
            "combat" => _run.Battle?.Phase == Phase.Cards && _effects.Count == 0,
            "keyboard" => _run.Battle?.Troops.Count == 1,
            "combo" or "reduced" => _run.Battle?.ComboHits >= 1 && !_audio.Playing,
            "action" => _run.Battle?.ComboHits == 1 && _run.Battle.Impacts.Any(i => i.Combos.Contains("BREACH")),
            "intel" => _run.Battle?.IntelRounds > 0 && _selectedEnemy.HasValue,
            "enemyturn" => _run.Battle?.Round == 2,
            "vendor" => _run.Screen == RunScreen.Vendor,
            "event" => _run.Screen == RunScreen.Event,
            "act2" => _run.Screen == RunScreen.Map && _run.Act == 2,
            "inventory" => _modal == "inventory",
            "saves" => _run.Battle?.Phase == Phase.Defeat && _saves.List().Count == 0,
            _ => false
        };
        if (!SmokeCheck(valid, $"Unexpected final screen for {_smokeScreen}.")) return;
        _run.Battle?.AssertConservation();
        GD.Print($"VISUAL_SMOKE: {_smokeScreen} passed; native input and final state verified. Window {GetWindow().Size}, render {GetViewport().GetTexture().GetSize()}.");
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        if (_screenshotPath is not null)
        {
            var error = GetViewport().GetTexture().GetImage().SavePng(_screenshotPath);
            if (error != Error.Ok) { GD.PushError($"Screenshot failed: {error}"); GetTree().Quit(1); return; }
            GD.Print("VISUAL_SMOKE: screenshot saved.");
        }
        if (_smokeSaveDirectory is not null && System.IO.Directory.Exists(_smokeSaveDirectory))
            System.IO.Directory.Delete(_smokeSaveDirectory, true);
        GetTree().Quit();
    }
}
