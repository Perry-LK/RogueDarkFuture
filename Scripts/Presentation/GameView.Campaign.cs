using Godot;
using FutureDark.Rules;
using System.Text.Json;
using GColour = Godot.Color;

namespace FutureDark.Presentation;

public partial class GameView
{
    private static readonly GColour Territory = new("#a98a55");

    private void DrawCityOverview()
    {
        Text("A city, taken back street by street.", 52, 140, 34, Paper, _bold);
        Text("Select a district. Its route opens a new front.", 54, 176, 18, Muted);
        var districts = CityDistricts();
        for (int d = 0; d < districts.Length; d++)
        {
            var polygon = districts[d];
            int act = d + 1;
            bool taken = _run.CompletedActs.Contains(act);
            bool available = act == RunState.FinaleAct ? _run.CentreUnlocked : !taken && (act == 1 || _run.CompletedActs.Contains(act - 1));
            DrawPolygon(polygon, [taken ? new GColour("#302b22") : new GColour("#171f23")]);
            Outline(polygon, taken ? Territory : available ? Paper.Darkened(.2f) : Line, taken ? 3 : 2);
            var random = new Random(d + 441);
            int minX = (int)polygon.Min(p => p.X), maxX = (int)polygon.Max(p => p.X);
            int minY = (int)polygon.Min(p => p.Y), maxY = (int)polygon.Max(p => p.Y);
            for (int i = 0; i < (act == RunState.FinaleAct ? 60 : 210); i++)
            {
                float x = random.Next(minX, maxX) / 12 * 12, y = random.Next(minY, maxY) / 12 * 12;
                var centre = new Vector2(x, y);
                if (!Geometry2D.IsPointInPolygon(centre, polygon)) continue;
                float width = d == 0 ? random.Next(6, 16) : random.Next(10, 23);
                float height = d == 0 ? random.Next(6, 12) : random.Next(14, 34);
                var rect = new Rect2(centre, new Vector2(width, height));
                if (!Geometry2D.IsPointInPolygon(rect.End, polygon)) continue;
                if (d == 0 && i % 3 == 0) DrawRect(rect, Line.Darkened(.2f), false, 1);
                else DrawRect(rect, taken ? Territory.Darkened(.63f) : Line.Darkened(.35f));
                if (d >= 3) DrawLine(centre, centre + new Vector2(0, -height * .3f), Line, 1);
            }
            for (int street = 0; street < 7; street++)
            {
                float y = 285 + street * 48;
                Vector2? previous = null;
                for (int x = 240; x < 1250; x += 12)
                {
                    var point = new Vector2(x, y + (x / 120 % 2) * 13 + (x % 120) * .09f);
                    if (Geometry2D.IsPointInPolygon(point, polygon))
                    {
                        if (previous.HasValue) DrawLine(previous.Value, point, Ink, 3);
                        previous = point;
                    }
                    else previous = null;
                }
            }
            for (int street = 0; street < 10; street++)
            {
                float x = minX + 20 + street * (maxX - minX) / 10f;
                Vector2? previous = null;
                for (int y = minY; y < maxY; y += 9)
                {
                    var point = new Vector2(x + (y / 90 % 2) * 11, y);
                    if (Geometry2D.IsPointInPolygon(point, polygon))
                    {
                        if (previous.HasValue) DrawLine(previous.Value, point, Ink, 3);
                        previous = point;
                    }
                    else previous = null;
                }
            }
            Vector2 label = act == RunState.FinaleAct ? new(647, 436) : DistrictLabel(d);
            Box(new Rect2(label - new Vector2(12, 26), new Vector2(act == RunState.FinaleAct ? 169 : 210, 76)), Ink, taken ? Territory : Line);
            Text(act == RunState.FinaleAct ? "Central Command" : $"{act:00} / {RunState.ActTitle(act)[4..]}", label.X, label.Y, 17, Paper, _bold);
            Text(taken ? "Liberated" : available ? "Enter district >" : "No connection", label.X, label.Y + 30, 15, taken ? Territory : Muted);
            _hotspots.Add(new Hotspot("district-" + act, new Rect2(label - new Vector2(12, 26), new Vector2(act == RunState.FinaleAct ? 169 : 210, 76)),
                () => { if (Act(_run.StartAct(act))) { ClearSelection(); _cardMotions.Clear(); } }, available && _lock <= 0, polygon));
        }
        Rule(54, 753, 1330);
        Text($"{_run.CompletedActs.Count}/{RunState.MainActCount} districts liberated", 54, 794, 23, Territory, _bold);
        Paragraph(_run.CentreUnlocked ? _run.CompletedActs.Count == RunState.MainActCount ?
            "All five districts are yours. Central Command is the final target." :
            "Central Command is accessible. Enter the centre, or secure the remaining district first." :
            "Liberate four districts to expose the centre. The fifth district is an optional challenge.", 54, 833, 1030, 17, Muted, 2);
        Button("city-deck", "REVIEW YOUR DECK", new Rect2(1140, 782, 244, 43), () => OpenDeck("RUN"));
    }

    private void DrawRoomDialog()
    {
        var node = _run.Nodes.SingleOrDefault(n => n.Id == _roomDialog);
        if (node is null) { Text("No room selected.", 450, 400); return; }
        Box(new Rect2(398, 238, 644, 411), Ink, node.Kind == RoomKind.Elite ? Warning : Line);
        DrawEmblem(RoomEmblem(node.Kind), new Rect2(425, 250, 51, 51));
        Text(node.Kind.ToString(), 491, 286, 19, Muted);
        Paragraph(node.Name, 431, 339, 572, 32, Paper, 2, _bold);
        Paragraph(node.Subtitle, 431, 414, 572, 20, Muted, 3);
        Paragraph(node.Kind == RoomKind.Elite ? "Expect mutually supporting specialists. Breaking their setup is safer than trading damage. Relic and increased funds on victory." :
            node.Kind == RoomKind.Battle ? "Enemy types determine their special moves. Plans commit at round start; use Recon to see values and destinations." :
            node.Kind == RoomKind.Boss ? "Bring your supplies. Defeating this command post advances control of the city." :
            node.Kind == RoomKind.Vendor ? "Stock is fixed for this room. Purchased offers do not refresh when loading the same room." :
            "Inspect the choices before committing. No event effect is applied until you choose.", 431, 488, 572, 17, Muted, 3);
        Button("room-cancel", "BACK", new Rect2(431, 584, 164, 39), () => _modal = "");
        Button("room-enter", "ENTER ROOM  >", new Rect2(611, 584, 397, 39), () =>
        { _modal = ""; EnterNode(node.Id); }, _run.Available.Contains(node.Id), true);
    }

    private void DrawRoomEvent()
    {
        Text("A moment between patrols.", 62, 148, 38, Paper, _bold);
        Text(_run.ActName + " / " + _run.EventId, 64, 190, 17, Territory);
        Paragraph(_run.EventStory, 64, 252, 968, 24, Muted, 4);
        DrawRelay(new Vector2(1196, 242));
        int count = _run.EventChoices.Count;
        float width = (1312 - (count - 1) * 20) / count;
        for (int i = 0; i < count; i++)
        {
            var choice = _run.EventChoices[i];
            float x = 64 + i * (width + 20);
            Box(new Rect2(x, 403, width, 320));
            Text($"0{i + 1}", x + 24, 442, 18, Territory, _mono);
            Paragraph(choice.Title, x + 24, 490, width - 48, 28, Paper, 2, _bold);
            Paragraph(choice.Detail, x + 24, 570, width - 48, 18, Muted, 4);
            Button("event-" + choice.Id, choice.Cost > 0 ? $"CHOOSE / {choice.Cost} FUNDS" : "CHOOSE  >", new Rect2(x + 24, 657, width - 48, 42), () =>
            {
                if (choice.Id is "upgrade" or "remove") { _cardChoice = choice.Id; _deckPage = 0; OpenModal("choose-card"); }
                else Act(_run.ResolveEvent(choice.Id));
            }, _run.Funds >= choice.Cost, true);
        }
        Text("One decision. Its consequences stay with this run.", 64, 785, 18, Muted);
        if (_message.Length > 0) Paragraph(_message, 64, 840, 1250, 17, _messageError ? Warning : Paper);
    }

    private void DrawVendor()
    {
        Text("The Night Exchange", 50, 139, 38, Paper, _bold);
        Text("No names. No credit. Your next combination is on the table.", 53, 178, 18, Muted);
        for (int i = 0; i < _run.Shop.Count; i++)
        {
            int index = i;
            var offer = _run.Shop[i];
            bool sold = _run.Purchased.Contains(i);
            if (offer.Kind == "card")
            {
                float x = 54 + i * 220;
                DrawCard(new Card(0, Content.Card(offer.DefinitionId)), new Rect2(x, 218, 204, 189));
                Button("buy-" + i, sold ? "SOLD" : $"{offer.Price} FUNDS", new Rect2(x, 418, 204, 33),
                    () => Act(_run.Purchase(index)), !sold && _run.Funds >= offer.Price);
                if (new Rect2(x, 218, 204, 189).HasPoint(_mouse))
                    Paragraph(Content.RulesText(new Card(0, Content.Card(offer.DefinitionId))), 54, 482, 1310, 17, Paper, 2);
            }
            else
            {
                int j = i - 6;
                float x = 54 + j * 264;
                string name = offer.Kind == "supply" ? Content.Supply(offer.DefinitionId).Name : Content.Relic(offer.DefinitionId).Name;
                string detail = offer.Kind == "supply" ? Content.Supply(offer.DefinitionId).Description : Content.Relic(offer.DefinitionId).Description;
                Box(new Rect2(x, 530, 248, 172));
                Text(offer.Kind == "supply" ? "Supply" : "Relic", x + 14, 554, 14, Territory);
                Paragraph(name, x + 14, 581, 220, 20, Paper, 2, _bold);
                Paragraph(detail, x + 14, 625, 220, 14, Muted, 3);
                Button("buy-" + i, sold ? "SOLD" : $"{offer.Price} FUNDS", new Rect2(x, 713, 248, 33),
                    () => Act(_run.Purchase(index)), !sold && _run.Funds >= offer.Price);
            }
        }
        for (int i = 0; i < _run.Supplies.Count; i++)
        {
            int slot = i;
            var supply = Content.Supply(_run.Supplies[i]);
            Button("sell-" + i, $"SELL {supply.Name.ToUpperInvariant()} / {supply.Price / 2}", new Rect2(54 + i * 280, 775, 264, 34), () => Act(_run.SellSupply(slot)));
        }
        Button("vendor-remove", $"REMOVE CARD / {_run.RemovalPrice}", new Rect2(54, 834, 295, 40), () =>
        { _cardChoice = "vendor-remove"; _deckPage = 0; OpenModal("choose-card"); }, _run.Deck.Count > 8 && _run.Funds >= _run.RemovalPrice);
        Button("leave-vendor", "LEAVE EXCHANGE  >", new Rect2(1059, 829, 310, 45), () => Act(_run.LeaveVendor()), primary: true);
        if (_message.Length > 0) Paragraph(_message, 373, 854, 660, 15, _messageError ? Warning : Paper, 1);
    }

    private void DrawCardChoice()
    {
        Box(new Rect2(40, 97, 1360, 752), Ink, Line);
        bool upgrade = _cardChoice == "upgrade";
        Text(upgrade ? "Choose a card to improve." : "Choose a card to remove.", 73, 148, 32, Paper, _bold);
        Text(upgrade ? "Preview shows its upgraded effect. Only this instance changes." :
            "This instance leaves your deck permanently. At least eight cards must remain.", 76, 183, 17, Muted);
        var cards = _run.Deck.Where(c => !upgrade || !c.Upgraded).ToArray();
        int pages = Math.Max(1, (cards.Length + 9) / 10);
        _deckPage = Math.Clamp(_deckPage, 0, pages - 1);
        for (int i = 0; i < Math.Min(10, cards.Length - _deckPage * 10); i++)
        {
            var card = cards[_deckPage * 10 + i];
            float x = 76 + i % 5 * 260, y = 216 + i / 5 * 268;
            DrawCard(upgrade ? card with { Upgraded = true } : card, new Rect2(x, y, 244, 196));
            Button("choose-" + card.Id, upgrade ? "UPGRADE" : "REMOVE", new Rect2(x, y + 204, 244, 34), () =>
            {
                var result = _cardChoice == "vendor-remove" ? _run.RemoveCard(card.Id) : _run.ResolveEvent(_cardChoice, card.Id);
                if (Act(result)) _modal = "";
            });
        }
        Button("choice-back", "CANCEL", new Rect2(76, 787, 180, 39), () => _modal = "");
        Button("choice-prev", "<", new Rect2(947, 787, 49, 39), () => _deckPage--, _deckPage > 0);
        Text($"Page {_deckPage + 1}/{pages}", 1019, 813, 17, Muted);
        Button("choice-next", ">", new Rect2(1168, 787, 49, 39), () => _deckPage++, _deckPage + 1 < pages);
    }

    private void DrawInventory()
    {
        Box(new Rect2(97, 105, 1246, 705), Ink, Line);
        Text("What the resistance carries.", 132, 154, 34, Paper, _bold);
        Text($"{_run.Funds} funds  /  {_run.Deck.Count} cards  /  {_run.Supplies.Count}/3 supply slots", 135, 193, 19, Territory);
        for (int i = 0; i < 3; i++)
        {
            float x = 135 + i * 390;
            Box(new Rect2(x, 229, 370, 111));
            if (i < _run.Supplies.Count)
            {
                var item = Content.Supply(_run.Supplies[i]);
                Text(item.Name, x + 16, 264, 23, Paper, _bold);
                Paragraph(item.Description, x + 16, 295, 336, 17, Muted, 2);
            }
            else Text("Empty supply slot", x + 16, 285, 20, Muted);
        }
        Text("Relics / active for this run", 135, 386, 23, Paper, _bold);
        if (_run.Relics.Count == 0) Text("Win elite fights, defeat bosses or visit a vendor to acquire relics.", 135, 432, 18, Muted);
        for (int i = 0; i < _run.Relics.Count; i++)
        {
            var relic = Content.Relic(_run.Relics[i]);
            float x = 135 + i % 3 * 390, y = 424 + i / 3 * 71;
            Text(relic.Name, x, y, 19, Territory, _bold);
            Paragraph(relic.Description, x, y + 23, 363, 14, Muted, 2);
        }
        Button("inventory-deck", "INSPECT DECK", new Rect2(135, 744, 244, 40), () => OpenDeck("RUN"));
        Button("inventory-close", "CLOSE", new Rect2(1097, 744, 205, 40), () => _modal = "", primary: true);
    }

    private static bool PersistenceError(Exception error) => error is System.IO.IOException or UnauthorizedAccessException or JsonException or InvalidOperationException;

    private bool TrySaveRun()
    {
        try
        {
            _saves.Save(_run);
            return true;
        }
        catch (Exception error) when (PersistenceError(error))
        {
            GD.PushError(error.ToString());
            Act(CommandResult.Reject("Save failed: " + error.Message));
            return false;
        }
    }

    private void DeleteFinishedSave()
    {
        SettleRun();
    }

    private void OpenSaves()
    {
        try
        {
            _saveEntries = _saves.List();
            _savePage = 0;
            foreach (var entry in _saveEntries.Where(e => e.Error is not null))
                GD.PushError($"Save {entry.RunId}: {entry.Error}");
            OpenModal("saves");
        }
        catch (Exception error) when (PersistenceError(error))
        {
            GD.PushError(error.ToString());
            Act(CommandResult.Reject("Could not list saves: " + error.Message));
        }
    }

    private void LoadRun(string id)
    {
        if (!_servicesReady) { Act(CommandResult.Reject("Progression storage is unavailable.")); return; }
        try
        {
            if (_profile.AwardedRuns.Contains(id))
            {
                _saves.Delete(id);
                Act(CommandResult.Reject("That run has already ended. Its stale checkpoint was removed."));
                return;
            }
            var loaded = _saves.Load(id);
            _saves.Save(loaded);
            Restart();
            _run = loaded;
            _title = false;
            _modal = "";
            NotifyTurn("Signal restored", "Loaded at the room's entrance. The same route and content are preserved.");
        }
        catch (Exception error) when (PersistenceError(error))
        {
            GD.PushError(error.ToString());
            Act(CommandResult.Reject("Could not load this run: " + error.Message));
        }
    }

    private void DrawSaves()
    {
        Box(new Rect2(188, 117, 1064, 687), Ink, Line);
        Text("Continue a resistance cell.", 222, 167, 33, Paper, _bold);
        Text("Three concurrent runs. Autosave at room entry. Continue resumes the latest played.", 225, 205, 17, Muted);
        int pages = Math.Max(1, (_saveEntries.Count + 4) / 5);
        if (_saveEntries.Count == 0) Text("No active runs. Start a new resistance cell.", 225, 302, 21, Muted);
        for (int i = 0; i < Math.Min(5, _saveEntries.Count - _savePage * 5); i++)
        {
            var entry = _saveEntries[_savePage * 5 + i];
            float y = 244 + i * 87;
            Box(new Rect2(222, y, 996, 73));
            if (entry.Snapshot is { } saved)
            {
                Text($"Act {saved.Act} / seed {saved.Seed}", 241, y + 28, 21, Paper, _bold);
                Text($"{saved.Hp} HP  |  {saved.Funds} funds  |  {entry.Modified.ToLocalTime():g}", 241, y + 53, 15, Muted);
                Button("load-" + entry.RunId, "RESUME", new Rect2(931, y + 17, 121, 39), () => LoadRun(entry.RunId));
            }
            else Paragraph("Unreadable save: " + entry.Error, 241, y + 28, 660, 16, Warning, 2);
            Button("abandon-" + entry.RunId, "ABANDON", new Rect2(1070, y + 17, 128, 39), () => { _abandonId = entry.RunId; OpenModal("abandon-slot"); });
        }
        Button("saves-close", "BACK", new Rect2(222, 742, 180, 39), () => _modal = "");
        Button("saves-prev", "<", new Rect2(865, 742, 48, 39), () => _savePage--, _savePage > 0);
        Text($"Page {_savePage + 1}/{pages}", 933, 768, 17, Muted);
        Button("saves-next", ">", new Rect2(1055, 742, 48, 39), () => _savePage++, _savePage + 1 < pages);
    }
}
