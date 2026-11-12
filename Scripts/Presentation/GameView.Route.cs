using Godot;
using FutureDark.Rules;
using GColour = Godot.Color;

namespace FutureDark.Presentation;

public partial class GameView
{
    private static readonly Rect2 RouteViewport = new(54, 220, 1332, RouteLayout.Height);
    private static readonly Rect2 RouteScrollTrack = new(70, 740, 1300, 16);
    private IReadOnlyList<RouteNode>? _layoutNodes;
    private RouteLayout _routeLayout = null!;
    private float _routeScroll;
    private Vector2? _routeDragStart;
    private float _routeDragScroll;
    private bool _routeDragged;
    private bool _routeThumbDrag;
    private string? _routePressedNode;
    private float RouteScrollLimit => Math.Max(0, _routeLayout.Width - RouteViewport.Size.X);

    private void EnsureRouteLayout()
    {
        if (ReferenceEquals(_layoutNodes, _run.Nodes)) return;
        _layoutNodes = _run.Nodes;
        _routeLayout = new RouteLayout(_run.Nodes, _run.Seed, _run.Act);
        string frontier = _run.Available.FirstOrDefault() ?? _run.Nodes[0].Id;
        _routeScroll = Math.Clamp(_routeLayout.Positions[frontier].X - 220, 0, RouteScrollLimit);
        _routeDragStart = null;
    }

    private Vector2 RoutePosition(RouteNode node)
    {
        RoutePoint p = _routeLayout.Positions[node.Id];
        return RouteViewport.Position + new Vector2(p.X - _routeScroll, p.Y);
    }

    private void DrawRouteLine(Vector2 a, Vector2 b, GColour color, float width = 1)
    {
        Vector2 delta = b - a;
        float enter = 0, leave = 1;
        bool Clip(float p, float q)
        {
            if (Math.Abs(p) < .0001f) return q >= 0;
            float t = q / p;
            if (p < 0) enter = Math.Max(enter, t); else leave = Math.Min(leave, t);
            return enter <= leave;
        }
        Rect2 bounds = RouteViewport.Grow(-width / 2);
        if (Clip(-delta.X, a.X - bounds.Position.X) && Clip(delta.X, bounds.End.X - a.X) &&
            Clip(-delta.Y, a.Y - bounds.Position.Y) && Clip(delta.Y, bounds.End.Y - a.Y))
            DrawLine(a + delta * enter, a + delta * leave, color, width, true);
    }

    private void DrawRouteRing(Vector2 centre, float radius, GColour color, float width)
    {
        for (int i = 0; i < 48; i++)
            DrawRouteLine(centre + Vector2.FromAngle(i * Mathf.Tau / 48) * radius,
                centre + Vector2.FromAngle((i + 1) * Mathf.Tau / 48) * radius, color, width);
    }

    private void DrawActMap()
    {
        EnsureRouteLayout();
        Text(_run.ActName, 54, 143, 36, Paper, _bold);
        Text("Follow the signal through the district.", 56, 180, 18, Muted);
        DrawEmblem(Emblem.Route, new Rect2(1203, 116, 32, 32));
        Text($"{_run.Completed.Count:00} / {_run.Nodes.Max(n => n.Column) + 1:00}", 1245, 140, 20, Muted, _mono);
        Rule(54, 204, 1332);
        DrawRect(RouteViewport, new GColour("#0c1216"));
        var random = new Random(unchecked(_run.Seed * 17 + _run.Act));
        for (int x = 0; x < _routeLayout.Width; x += 88)
        for (int y = 25; y < RouteLayout.Height - 30; y += 84)
        {
            var building = new Rect2(RouteViewport.Position + new Vector2(x + random.Next(8, 25) - _routeScroll, y + random.Next(0, 19)),
                new Vector2(random.Next(24, 63), random.Next(20, 52)));
            var visible = building.Intersection(RouteViewport);
            if (visible.HasArea())
            {
                DrawRect(visible, new GColour("#10191e"));
                DrawRect(visible, new GColour("#182329"), false, 1);
            }
        }
        foreach (var street in _routeLayout.Streets)
        {
            bool travelled = _run.Completed.Contains(street.From) && _run.Completed.Contains(street.To);
            bool available = _run.Completed.Contains(street.From) && _run.Available.Contains(street.To);
            GColour color = travelled ? Territory : available ? Territory.Darkened(.25f) : new GColour("#506066");
            for (int i = 1; i < street.Points.Count; i++)
            {
                Vector2 Point(RoutePoint p) => RouteViewport.Position + new Vector2(p.X - _routeScroll, p.Y);
                Vector2 a = Point(street.Points[i - 1]), b = Point(street.Points[i]);
                DrawRouteLine(a, b, Ink, 7);
                DrawRouteLine(a, b, color, travelled ? 3 : 1.6f);
            }
        }
        RouteNode? inspected = null;
        foreach (var node in _run.Nodes)
        {
            Vector2 p = RoutePosition(node);
            var rect = new Rect2(p - new Vector2(38, 38), new Vector2(76, 76));
            bool complete = _run.Completed.Contains(node.Id), available = _run.Available.Contains(node.Id);
            bool hover = _modal.Length == 0 && (_keyboardFocus ? _focus == "node-" + node.Id : rect.HasPoint(_mouse) && RouteViewport.HasPoint(_mouse));
            if (hover) inspected = node;
            GColour color = complete ? Territory : hover || available ? Paper : node.Kind == RoomKind.Elite ? Warning : Muted;
            DrawRouteRing(p, 36, hover || available ? color : Line, available ? 2.5f : 1);
            DrawEmblem(RoomEmblem(node.Kind), new Rect2(p - new Vector2(33, 33), new Vector2(66, 66)),
                hover || available ? Colors.White : new GColour(color, .85f), RouteViewport);
            if (complete)
                DrawEmblem(Emblem.Check, new Rect2(p + new Vector2(18, 21), new Vector2(22, 22)), Territory, RouteViewport);
            if (available)
                DrawRouteRing(p + new Vector2(0, 48), 3, Territory, 3);
            _hotspots.Add(new Hotspot("node-" + node.Id, rect, () => { _roomDialog = node.Id; OpenModal("room"); },
                _lock <= 0, Clip: RouteViewport));
        }
        foreach (var layer in _run.Nodes.GroupBy(n => n.Column))
        {
            float x = RoutePosition(layer.First()).X;
            if (x > RouteViewport.Position.X + 15 && x < RouteViewport.End.X - 25)
                Text($"{layer.Key + 1:00}", x - 10, 249, 12, Muted.Darkened(.3f), _mono);
        }
        float thumbWidth = RouteScrollTrack.Size.X * Math.Min(1, RouteViewport.Size.X / _routeLayout.Width);
        float thumbX = RouteScrollTrack.Position.X + (RouteScrollLimit == 0 ? 0 : _routeScroll / RouteScrollLimit) * (RouteScrollTrack.Size.X - thumbWidth);
        Rule(RouteScrollTrack.Position.X, RouteScrollTrack.GetCenter().Y, RouteScrollTrack.Size.X, Line.Darkened(.45f));
        DrawRect(new Rect2(thumbX, RouteScrollTrack.Position.Y + 5, thumbWidth, 6), Muted.Darkened(.35f));

        Text(inspected?.Name ?? "Choose your next crossing.", 58, 803, 22, Paper, _bold);
        Text(inspected is null ? "Wheel / drag to explore. Select a room to inspect." : $"{inspected.Kind}  /  {inspected.Subtitle}", 58, 835, 15, Muted);
        int legend = 0;
        foreach (RoomKind kind in Enum.GetValues<RoomKind>())
        {
            IconButton("legend-" + kind, RoomEmblem(kind), kind == RoomKind.Elite ? "Elite: specialist squad and relic reward" : kind.ToString(),
                new Rect2(886 + legend++ * 48, 782, 40, 40), () => OpenModal("guide"));
        }
        IconButton("route-back", Emblem.Left, "Scroll back", new Rect2(1160, 782, 42, 42), () => ScrollRoute(-440), _routeScroll > 0);
        IconButton("route-forward", Emblem.Right, "Scroll forward", new Rect2(1210, 782, 42, 42), () => ScrollRoute(440), _routeScroll < RouteScrollLimit);
        IconButton("route-centre", Emblem.Route, "Return to the next available room", new Rect2(1260, 782, 42, 42), () =>
        {
            _focus = "node-" + (_run.Available.FirstOrDefault() ?? _run.Nodes[0].Id);
            _keyboardFocus = true;
            EnsureRouteFocusVisible();
        });
        IconButton("map-deck", Emblem.Deck, "Review deck", new Rect2(1330, 782, 46, 42), () => OpenDeck("RUN"));
    }

    private void ScrollRoute(float amount)
    {
        _routeScroll = Math.Clamp(_routeScroll + amount, 0, RouteScrollLimit);
        _focus = null;
        _keyboardFocus = false;
    }

    private void EnsureRouteFocusVisible()
    {
        if (_title || _modal.Length > 0 || _run.Screen != RunScreen.Map || _focus is null || !_focus.StartsWith("node-", StringComparison.Ordinal)) return;
        EnsureRouteLayout();
        if (!_routeLayout.Positions.TryGetValue(_focus[5..], out var p)) return;
        if (p.X - _routeScroll < 100) _routeScroll = Math.Max(0, p.X - 100);
        if (p.X - _routeScroll > RouteViewport.Size.X - 100) _routeScroll = Math.Min(RouteScrollLimit, p.X - RouteViewport.Size.X + 100);
    }

    private bool HandleRouteKey(Key key)
    {
        if (_title || _modal.Length > 0 || _run.Screen != RunScreen.Map) return false;
        EnsureRouteLayout();
        if (key is Key.Left or Key.Right or Key.Pageup or Key.Pagedown)
        {
            ScrollRoute(key is Key.Left or Key.Pageup ? -260 : 260);
            return true;
        }
        if (key is not (Key.Home or Key.End)) return false;
        _focus = "node-" + (key == Key.Home ? _run.Nodes[0].Id : _run.Nodes.Last().Id);
        _keyboardFocus = true;
        EnsureRouteFocusVisible();
        return true;
    }

    private bool HandleRouteInput(InputEvent @event)
    {
        if (_title || _modal.Length > 0 || _run.Screen != RunScreen.Map) { _routeDragStart = null; return false; }
        EnsureRouteLayout();
        if (@event is InputEventMouseMotion motion && _routeDragStart.HasValue)
        {
            Vector2 point = (motion.Position - _origin) / _scale;
            float distance = point.X - _routeDragStart.Value.X;
            _routeDragged |= Math.Abs(distance) > 5;
            if (_routeThumbDrag)
            {
                float thumbWidth = RouteScrollTrack.Size.X * Math.Min(1, RouteViewport.Size.X / _routeLayout.Width);
                _routeScroll = Math.Clamp(_routeDragScroll + distance * RouteScrollLimit / Math.Max(1, RouteScrollTrack.Size.X - thumbWidth), 0, RouteScrollLimit);
            }
            else if (_routeDragged) _routeScroll = Math.Clamp(_routeDragScroll - distance, 0, RouteScrollLimit);
            return true;
        }
        if (@event is not InputEventMouseButton mouse) return false;
        Vector2 position = (mouse.Position - _origin) / _scale;
        if (mouse.Pressed && RouteViewport.HasPoint(position) &&
            mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown or MouseButton.WheelLeft or MouseButton.WheelRight)
        {
            ScrollRoute(mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelLeft ? -180 : 180);
            return true;
        }
        if (mouse.ButtonIndex != MouseButton.Left) return false;
        if (mouse.Pressed && (RouteViewport.HasPoint(position) || RouteScrollTrack.Grow(5).HasPoint(position)))
        {
            _routeDragStart = position;
            _routeDragScroll = _routeScroll;
            _routeThumbDrag = RouteScrollTrack.Grow(5).HasPoint(position);
            _routeDragged = false;
            _keyboardFocus = false;
            _routePressedNode = _hotspots.LastOrDefault(h => h.Id.StartsWith("node-", StringComparison.Ordinal) && h.Contains(position))?.Id;
            return true;
        }
        if (!mouse.Pressed && _routeDragStart.HasValue)
        {
            _routeDragStart = null;
            if (!_routeDragged && !_routeThumbDrag)
                _hotspots.FirstOrDefault(h => h.Id == _routePressedNode && h.Enabled && h.Contains(position))?.Click();
            return true;
        }
        return false;
    }
}
