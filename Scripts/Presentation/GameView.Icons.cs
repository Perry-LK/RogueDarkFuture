using Godot;
using FutureDark.Rules;
using GColour = Godot.Color;

namespace FutureDark.Presentation;

public partial class GameView
{
    private enum Emblem
    {
        Battle, Elite, Vendor, Event, Boss, Troop, Action, Shield, Board, Health,
        Funds, Inventory, Deck, Relic, Menu, Save, Guide, Sound, Mute, Move,
        Mark, Corrosion, Jam, Summon, Rest, Hazard, Heal, Recon, Rifle, Hammer,
        Discard, Lost, Exhaust, Left, Right, Route, Check
    }

    private readonly Dictionary<Emblem, ImageTexture> _emblems = new();

    private void DrawIconStudy()
    {
        Text("Emblems / worn metal", 62, 86, 36, Paper, _bold);
        Text("Map, interface and tactical vocabulary. Colour remains restrained until a combination connects.", 65, 124, 18, Muted);
        Rule(64, 150, 1312);
        int index = 0;
        foreach (Emblem emblem in Enum.GetValues<Emblem>())
        {
            int column = index % 7, row = index / 7;
            float x = 90 + column * 190, y = 173 + row * 116;
            DrawEmblem(emblem, new Rect2(x, y, 84, 84));
            Text(emblem.ToString(), x + 4, y + 100, 16, Muted);
            index++;
        }
    }

    private static string EmblemDrawing(Emblem emblem) => emblem switch
    {
        Emblem.Battle => "<path d='M27 24l12 3 48 62-7 10-13-4-43-60z'/><path d='M95 23l8 14-56 61-12-1-4-13z'/><path d='M24 79l27 22M74 82l25-16' stroke-width='7'/>",
        Emblem.Elite => "<path d='M38 25l26-10 26 10 10 43-14 34-22 10-22-10-14-34z'/><path d='M38 44l22 4-5 22-15-4zm30 4l22-4-2 22-15 4z' fill='#172126'/><path d='M53 81l11-8 11 8-2 18H55z'/><path d='M17 40l-4 32 14 22m84-54l4 32-14 22' fill='none' stroke-width='5'/>",
        Emblem.Vendor => "<path d='M26 45h76v57H26zM20 44l13-24h62l13 24-7 12-13-5-12 5-12-5-12 5-12-5-13 5z'/><path d='M42 70h20v32H42zM75 69h14v16H75z' fill='#253036'/>",
        Emblem.Event => "<path d='M30 21l54-4 19 18-7 75-67-4z'/><path d='M82 18l-2 24 23-7M40 83l23-3m-24 15l42-3' fill='none'/><path d='M48 48q5-18 23-12 19 9-1 26l-7 9' fill='none' stroke-width='8'/><circle cx='63' cy='83' r='4'/>",
        Emblem.Boss => "<path d='M19 104V66l19-13V36l17 8V18h20v26l17-8v17l18 13v38z'/><path d='M31 74q33-28 66 0-33 28-66 0z' fill='#202a30'/><circle cx='64' cy='74' r='11'/><path d='M49 104V92h30v12' fill='#202a30'/>",
        Emblem.Troop => "<path d='M21 109l7-24 20-12h32l20 12 7 24z'/><path d='M36 52l3-24 25-12 25 12 4 29-9 29-20 10-20-10z'/><path d='M30 47h68v13H30zM47 63l14 3-3 12-12-3zm20 3l14-3 1 12-12 3z' fill='#1c272c'/><path d='M56 87h16M43 27l21-6' fill='none' stroke='#ece7d7'/>",
        Emblem.Action => "<path d='M27 27l30-9 33 22 11 28-12 29-29 12-29-24-11-32z'/><path d='M70 27L42 68h20l-7 33 34-48H68z' fill='#eee3c8'/><path d='M24 42l17-10m44 64l14-20' fill='none'/>",
        Emblem.Shield => "<path d='M64 16l39 15-5 48-34 34-34-34-5-48z'/><path d='M64 28l26 10-4 35-22 24-22-24-4-35z' fill='#45545b'/><path d='M64 31v61M43 52h42' fill='none' stroke='#c8cebd' stroke-width='5'/><path d='M29 37l5 17m57 28l-14 16' stroke='#edf0df' fill='none'/>",
        Emblem.Board => "<path d='M18 33l27-15 37 13 29-11-6 80-27 12-34-14-26 11z'/><path d='M46 21l-2 75m38-62l-4 75M21 59l23-9 34 13 30-12M24 80l20-7 34 12 25-9' fill='none' stroke='#27373c' stroke-width='6'/>",
        Emblem.Health => "<path d='M64 107L23 64q-17-34 12-42 18-6 29 15 12-21 30-15 29 8 11 42z'/><path d='M22 64h26l9-20 14 38 9-18h27' fill='none' stroke='#202c30' stroke-width='8'/>",
        Emblem.Funds => "<path d='M18 59h51v41H18zm30-30h59v48H48z'/><ellipse cx='76' cy='30' rx='29' ry='11'/><ellipse cx='44' cy='59' rx='26' ry='11'/><path d='M19 75q26 15 49 0m-49 12q26 15 49 0m10-43q17 5 28-4m-27 17q16 5 27-4' fill='none'/>",
        Emblem.Inventory => "<path d='M43 21h42l9 18 8 67H26l8-67z'/><path d='M43 21v18h42V21M24 47h80v26H24z'/><path d='M49 65h30v22H49z' fill='#253238'/><path d='M35 92h58' stroke='#d9dbc9'/>",
        Emblem.Deck => "<path d='M19 33l61-15 20 80-61 15z' fill='#566065'/><path d='M38 18h69v85H38z'/><path d='M52 35h40v13H52zm0 29h40v23H52z' fill='#2d393d'/><path d='M73 68l-9 8 9 8 9-8z'/>",
        Emblem.Relic => "<path d='M35 18h20l9 28 9-28h20L79 53H49z'/><path d='M64 40l13 13 18 3 3 19-10 15-4 19-20-3-20 3-4-19-10-15 3-19 18-3z'/><circle cx='64' cy='77' r='20' fill='#38474b'/><path d='M64 59l5 12 12 6-12 5-5 13-5-13-12-5 12-6z'/>",
        Emblem.Menu => "<path d='M22 27h84v20H22zm0 28h84v20H22zm0 28h84v20H22z'/><path d='M29 31h12m-12 28h24m-24 28h15' stroke='#f1e9d5'/>",
        Emblem.Save => "<path d='M26 19h65l13 15v75H26z'/><path d='M42 20h40v29H42zm-1 51h49v38H41z' fill='#28363c'/><path d='M49 80h33m-33 13h33' stroke='#bac8c4'/>",
        Emblem.Guide => "<path d='M17 29q24-12 47 0 24-12 47 0v73q-24-12-47 0-24-12-47 0z'/><path d='M64 29v72M29 46l23-1m-23 14l23-1m-23 14l23-1m25-25l22 1m-22 13l22 1m-22 13l22 1' fill='none' stroke='#304048' stroke-width='4'/>",
        Emblem.Sound or Emblem.Mute => "<path d='M21 47h22l28-23v80L43 81H21z'/><path d='M83 43q24 21 0 42m13-59q39 37 0 75' fill='none' stroke='#99aba7' stroke-width='6'/>" +
            (emblem == Emblem.Mute ? "<path d='M22 17l89 96' stroke='#c69482' stroke-width='10'/>" : ""),
        Emblem.Move => "<path d='M31 24h28l5 41 34 15 7 21H25l-4-14 12-27z'/><path d='M25 95h76M35 39h20m-18 12h20m-18 12h20' fill='none'/><path d='M79 22l22 18-22 18V46H66V33h13z'/>",
        Emblem.Mark => "<circle cx='64' cy='64' r='34' fill='none' stroke='#aebdb3' stroke-width='9'/><circle cx='64' cy='64' r='10'/><path d='M64 12v28m0 48v28M12 64h28m48 0h28' stroke='#aebdb3' stroke-width='6'/>",
        Emblem.Corrosion => "<path d='M62 15q-5 22-28 49-19 37 18 47 54 11 51-30-3-22-41-66z'/><path d='M41 77q-4 22 16 23' fill='none' stroke='#e6edce' stroke-width='5'/><circle cx='73' cy='81' r='8' fill='#293631'/><circle cx='80' cy='99' r='4' fill='#293631'/>",
        Emblem.Jam => "<path d='M57 53h14l15 55H42z'/><circle cx='64' cy='40' r='11'/><path d='M40 20q-25 20-3 39m52-39q25 20 3 39' fill='none' stroke-width='7'/><path d='M21 21l87 87' stroke='#c99783' stroke-width='9'/>",
        Emblem.Summon => "<path d='M17 55l22-15 12 13h26l12-13 22 15-14 27-23-7H54l-23 7z'/><circle cx='64' cy='64' r='12' fill='#26373b'/><path d='M58 14h12v12h12v12H70v12H58V38H46V26h12zM52 91h24v17H52z'/>",
        Emblem.Rest => "<path d='M24 36h71v54H24z'/><path d='M95 47h11q14 24-11 29' fill='none' stroke-width='8'/><path d='M20 99h83M39 15v12m20-18v18m20-12v12' fill='none' stroke-width='5'/>",
        Emblem.Hazard => "<path d='M64 16l49 89H15z'/><path d='M57 44h14l-3 33h-8zm2 42h10v10H59z' fill='#273236'/>",
        Emblem.Heal => "<path d='M22 35h84v69H22zM45 21h38v14H45z'/><path d='M55 46h18v17h17v17H73v17H55V80H38V63h17z' fill='#2d3d3b'/>",
        Emblem.Recon => "<path d='M12 64 Q64 0 116 64 Q64 128 12 64 Z' fill='#aeb7ae'/><circle cx='64' cy='64' r='25' fill='#29383b'/><circle cx='64' cy='64' r='13'/><path d='M23 43l-8-9m90 9l8-9M64 15v12' fill='none' stroke='#aebdb3' stroke-width='5'/>",
        Emblem.Rifle => "<path d='M18 90l15-31 20 3 38-41 12 12-35 45-1 17-15 3-8-14-15 21z'/><path d='M58 44l19-19 10 8-19 22z'/><path d='M41 83l13-14' stroke='#edf0d9' stroke-width='4'/>",
        Emblem.Hammer => "<path d='M35 103l-11-11 53-57 11 11z'/><path d='M58 19l17-7 38 38-7 17-17 7-38-38z'/><path d='M78 21l24 26' stroke='#e9e5cb' stroke-width='5'/>",
        Emblem.Discard => "<path d='M35 29h58v79H35z' fill='#596568'/><path d='M22 20h57v67H22z'/><path d='M62 66h19V53l28 26-28 26V90H62z' fill='#bfc9bb'/>",
        Emblem.Lost => "<path d='M35 23h60l9 14v46l-18 27H42L24 83V40z'/><path d='M38 48l20 7-4 19-15-3zm32 7l21-7-1 23-16 3zM56 88h17v17H56z' fill='#253238'/>",
        Emblem.Exhaust => "<path d='M62 14l-2 35 22-14-3 27 21-9q21 46-18 57-61 16-64-26-1-25 23-41l-4 36 18-17z'/><path d='M65 73q-30 33 1 34 22-5-1-34z' fill='#2c393e'/>",
        Emblem.Left => "<path d='M88 20L35 64l53 44V83L65 64l23-19z'/>",
        Emblem.Right => "<path d='M40 20l53 44-53 44V83l23-19-23-19z'/>",
        Emblem.Route => "<path d='M27 104V76l68-26V24' fill='none' stroke='#99aba7' stroke-width='12'/><circle cx='27' cy='103' r='13'/><circle cx='95' cy='24' r='13'/>",
        Emblem.Check => "<path d='M18 63l26 32 67-62-14-14-52 48-13-17z'/>",
        _ => throw new ArgumentOutOfRangeException(nameof(emblem))
    };

    private void DrawEmblem(Emblem emblem, Rect2 rect, GColour? tint = null, Rect2? clip = null)
    {
        if (!_emblems.TryGetValue(emblem, out var texture))
        {
            string drawing = EmblemDrawing(emblem);
            string svg = "<svg xmlns='http://www.w3.org/2000/svg' width='128' height='128' viewBox='0 0 128 128'>" +
                "<defs><linearGradient id='metal' x2='.8' y2='1'><stop stop-color='#e7dfc8'/><stop offset='.48' stop-color='#aeb7ae'/><stop offset='1' stop-color='#6d7c7b'/></linearGradient></defs>" +
                "<g transform='translate(2 4)' fill='#030809' stroke='#030809' stroke-width='7'>" + drawing + "</g>" +
                "<g fill='url(#metal)' stroke='#243137' stroke-width='3' stroke-linejoin='round' stroke-linecap='round'>" + drawing + "</g>" +
                "<g fill='none' stroke='#f1ead4' stroke-width='1' opacity='.22'><path d='M32 38l10-3m36-8l7 4M28 87l12-2m35 8l13-5M49 47l4 8m31 9l8-4M52 101l9-3'/></g>" +
                "<g fill='#080f13' opacity='.4'><path d='M34 44l11-4-3 3zM72 88l18-5-7 5zM45 69l9-3-4 4z'/></g></svg>";
            using var image = new Image();
            Error error = image.LoadSvgFromString(svg, 2);
            if (error != Error.Ok) throw new InvalidOperationException($"Cannot render {emblem} emblem: {error}");
            texture = ImageTexture.CreateFromImage(image);
            _emblems.Add(emblem, texture);
        }
        Rect2 visible = clip.HasValue ? rect.Intersection(clip.Value) : rect;
        if (!visible.HasArea()) return;
        Vector2 textureSize = texture.GetSize();
        var source = new Rect2((visible.Position - rect.Position) / rect.Size * textureSize, visible.Size / rect.Size * textureSize);
        DrawTextureRectRegion(texture, visible, source, tint ?? Colors.White);
    }

    private static Emblem CategoryEmblem(CardKind kind) => kind switch
    { CardKind.Troop => Emblem.Troop, CardKind.Action => Emblem.Action, CardKind.Block => Emblem.Shield, _ => Emblem.Board };

    private static Emblem RoomEmblem(RoomKind kind) => kind switch
    { RoomKind.Battle => Emblem.Battle, RoomKind.Elite => Emblem.Elite, RoomKind.Vendor => Emblem.Vendor, RoomKind.Event => Emblem.Event, _ => Emblem.Boss };

    private static Emblem SupplyEmblem(string id) => id switch
    { "shield" => Emblem.Shield, "emp" => Emblem.Jam, "stim" => Emblem.Action, "medkit" => Emblem.Heal, "acid-vial" => Emblem.Corrosion, _ => Emblem.Hazard };

    private void IconButton(string id, Emblem emblem, string tooltip, Rect2 rect, Action click, bool enabled = true, string count = "")
    {
        enabled &= _lock <= 0;
        bool hover = enabled && (_keyboardFocus ? _focus == id : rect.HasPoint(_mouse));
        if (hover) Box(rect, Raised, Line.Lightened(.25f));
        float size = Math.Min(rect.Size.Y - 4, count.Length == 0 ? rect.Size.X - 4 : 30);
        DrawEmblem(emblem, new Rect2(rect.Position + new Vector2(count.Length == 0 ? (rect.Size.X - size) / 2 : 2, (rect.Size.Y - size) / 2),
            new Vector2(size, size)), enabled ? Colors.White : new GColour(1, 1, 1, .3f));
        if (count.Length > 0) Text(count, rect.Position.X + size + 8, rect.Position.Y + rect.Size.Y / 2 + 6, 16, enabled ? Paper : Muted, _mono);
        _hotspots.Add(new Hotspot(id, rect, click, enabled, Tooltip: tooltip));
    }

    private void DrawTooltip(Hotspot hotspot)
    {
        float width = Math.Min(540, _font.GetStringSize(hotspot.Tooltip, fontSize: Mathf.RoundToInt(16 * _textScale)).X + 28);
        float height = 18 + WrapParagraph(hotspot.Tooltip, width - 28, 16, _font).Count() * 16 * _textScale * 1.35f;
        float x = Math.Clamp(hotspot.Rect.GetCenter().X - width / 2, 16, 1424 - width);
        float y = hotspot.Rect.End.Y + 9;
        if (y + height > 888) y = Math.Max(12, hotspot.Rect.Position.Y - height - 9);
        Box(new Rect2(x, y, width, height), Ink, Line.Lightened(.25f));
        Paragraph(hotspot.Tooltip, x + 14, y + 9 + 16 * _textScale, width - 28, 16, Paper);
    }
}
