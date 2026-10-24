using Godot;

namespace Mech.Tests;

public partial class SmokeTests : Node
{
    private CommandApp _app = null!;
    private int _checks;
    private bool _screenshots;
    private string _saveFile = "";

    public override async void _Ready()
    {
        try
        {
            _screenshots = OS.GetCmdlineUserArgs().Contains("--screenshots");
            TestGeometry();
            CampaignChecks.Run(Check);
            _app = ResourceLoader.Load<PackedScene>("res://Scenes/Command.tscn").Instantiate<CommandApp>();
            _app.SavePath = $"user://smoke-{Guid.NewGuid():N}.json";
            _saveFile = ProjectSettings.GlobalizePath(_app.SavePath);
            AddChild(_app);
            await Frames();
            Check(_app.MainMenu.Visible && !_app.Office.Visible && !_app.StarMap.Visible, "starts in the main menu");
            Check(_app.MainMenu.LoadButton.Disabled, "load disabled before first manual save");
            await Capture("main-menu");
            await PressKey(Key.Enter);
            Check(_app.Office.Visible && !_app.StarMap.Visible, "starts in the office");
            Check(_app.Office.ArtworkLoaded && _app.StarMap.Viewport.MapLoaded, "both local images load");
            Check(_app.Office.Hotspot.HasFocus(), "initial keyboard focus is on the wall map");
            var wallMapRect = _app.Office.WallMap.GetRect();
            var wallMapTexture = _app.Office.WallMap.Texture;
            Check(wallMapTexture is not null && wallMapTexture == _app.StarMap.Viewport.Texture, "wall chart shares the complete reference texture with the opened map");
            Near(wallMapTexture!.GetSize().X / wallMapTexture.GetSize().Y, 7f / 6, "wall chart uses the full 7:6 reference image");
            Check(_app.Office.WallMap.StretchMode == TextureRect.StretchModeEnum.KeepAspectCentered, "wall chart fits the image without cropping or distortion");
            await Capture("office");
            await PressKey(Key.Enter);
            Check(_app.StarMap.Visible && !_app.Office.Visible, "keyboard activation opens the chart");
            Check(_app.StarMap.Viewport.HasFocus(), "map receives keyboard focus");
            await Capture("map-fit");
            await PressKey(Key.Plus);
            Near(_app.StarMap.Viewport.Transform.Zoom, 1.35f, "keyboard zoom");
            var offset = _app.StarMap.Viewport.Transform.Offset;
            await PressKey(Key.Down);
            Check(_app.StarMap.Viewport.Transform.Offset != offset, "arrow keys pan the map");
            await Capture("map-zoom");
            await PressKey(Key.Home);
            Near(_app.StarMap.Viewport.Transform.Zoom, 1, "Home fits the map");
            Check(_app.StarMap.ZoomOut.Disabled, "minimum zoom disables zoom out");
            _app.StarMap.ZoomIn.EmitSignal(BaseButton.SignalName.Pressed);
            Near(_app.StarMap.Viewport.Transform.Zoom, 1.35f, "zoom button changes the viewport");
            _app.StarMap.FitButton.EmitSignal(BaseButton.SignalName.Pressed);
            Near(_app.StarMap.Viewport.Transform.Zoom, 1, "Fit button restores the overview");
            await TestMouse();
            Check(_app.Office.WallMap.Texture == wallMapTexture && _app.Office.WallMap.GetRect() == wallMapRect,
                "wall chart remains unchanged after zooming and panning the opened map");
            var zoom = _app.StarMap.Viewport.Transform.Zoom;
            var mapPosition = _app.StarMap.Viewport.Transform.Offset;
            await PressKey(Key.Escape);
            Check(_app.Office.Visible && _app.Office.Hotspot.HasFocus(), "Escape returns to the office and restores focus");
            _app.Office.Hotspot.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames();
            Near(_app.StarMap.Viewport.Transform.Zoom, zoom, "zoom survives navigation");
            Check(_app.StarMap.Viewport.Transform.Offset.IsEqualApprox(mapPosition), "pan survives navigation");
            _app.StarMap.BackButton.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames();
            Check(_app.Office.Hotspot.HasFocus(), "Back button restores wall-map focus");
            await TestResizing();
            TestMissingArtwork();
            await TestCampaignUi();
            await Frames();
            _app.QueueFree();
            await Frames();
            GD.Print($"PASS: {_checks} checks; campaign time, payments, bankruptcy, saves, menus, office/map input and resizing.");
            GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PushError($"FAIL after {_checks} checks: {error}");
            GetTree().Quit(1);
        }
        finally
        {
            if (System.IO.File.Exists(_saveFile)) System.IO.File.Delete(_saveFile);
        }
    }

    private void TestGeometry()
    {
        var model = new MapTransform(new Vector2(7000, 6000));
        model.Resize(new Vector2(1368, 692));
        Near(model.Zoom, 1, "initial fit zoom");
        Check(model.ImageRect.Position.X >= 0 && model.ImageRect.End.X <= model.ViewSize.X + .1f
            && model.ImageRect.Position.Y >= 0 && model.ImageRect.End.Y <= model.ViewSize.Y + .1f, "fit includes the complete chart");
        model.ZoomAt(4, model.ViewSize / 2);
        var anchor = new Vector2(600, 320);
        var point = model.ImagePoint(anchor);
        model.ZoomAt(5, anchor);
        Check(model.ImagePoint(anchor).DistanceTo(point) < .01f, "zoom preserves the image point under the cursor");
        var center = model.ImagePoint(model.ViewSize / 2);
        model.Resize(new Vector2(900, 500));
        Check(model.ImagePoint(model.ViewSize / 2).DistanceTo(center) < .01f, "resize preserves the viewed center");
        model.ZoomAt(100, model.ViewSize / 2);
        Near(model.Zoom, 12, "upper zoom bound");
        model.Pan(new Vector2(100000, 100000));
        Check(model.Offset.IsEqualApprox(Vector2.Zero), "pan clamps to the top/left edges");
        model.Pan(new Vector2(-100000, -100000));
        Check(model.ImageRect.End.IsEqualApprox(model.ViewSize), "pan clamps to the bottom/right edges");
        model.ZoomAt(.001f, model.ViewSize / 2);
        Near(model.Zoom, 1, "lower zoom bound");
        model.Fit();
        Check(model.ImageRect.GetCenter().DistanceTo(model.ViewSize / 2) < .01f, "fit recenters both axes");
    }

    private async Task TestMouse()
    {
        var viewport = _app.StarMap.Viewport;
        var at = viewport.GlobalPosition + viewport.Size / 2;
        MouseMotion(at, Vector2.Zero);
        MouseButton(at, Godot.MouseButton.WheelUp, true, 30);
        await Frames();
        Near(viewport.Transform.Zoom, 12, "mouse wheel reaches the zoom cap");
        Check(_app.StarMap.ZoomIn.Disabled, "maximum zoom disables zoom in");
        var before = viewport.Transform.Offset;
        MouseButton(at, Godot.MouseButton.Left, true);
        MouseMotion(at + new Vector2(80, 60), new Vector2(80, 60));
        MouseButton(at + new Vector2(80, 60), Godot.MouseButton.Left, false);
        await Frames();
        Check(viewport.Transform.Offset != before, "mouse drag pans the image");
        Check(!viewport.IsDragging, "mouse release ends dragging");
        MouseButton(at, Godot.MouseButton.Left, true);
        await Frames();
        _app.ShowOffice();
        Check(!viewport.IsDragging, "navigation cancels an active drag");
        MouseButton(at, Godot.MouseButton.Left, false);
        _app.ShowMap();
        await Frames();
    }

    private async Task TestResizing()
    {
        _app.ShowOffice();
        foreach (var size in new[] { new Vector2I(1920, 1080), new Vector2I(1024, 768), new Vector2I(900, 500), new Vector2I(1200, 1100) })
        {
            GetWindow().Size = size;
            await Frames();
            var office = _app.Office;
            var art = office.ArtworkRect;
            var hotspot = office.Hotspot;
            Check(art.Size.X > 0 && art.Size.Y > 0, $"office remains visible at {size}");
            Near(art.Size.X / art.Size.Y, 1672f / 940, $"office aspect ratio at {size}");
            var scale = art.Size.X / 1672;
            foreach (var (control, region) in new (Control, Rect2)[]
            {
                (hotspot, OfficeScreen.ChartRegion), (office.CalendarButton, OfficeScreen.CalendarRegion),
                (office.ReminderButton, OfficeScreen.ReminderRegion), (office.SafeButton, OfficeScreen.SafeRegion),
                (office.FilingButton, OfficeScreen.FilingRegion), (office.WallMap, OfficeScreen.MapRegion)
            })
            {
                Check(((control.Position - art.Position) / scale).DistanceTo(region.Position) < .1f
                    && (control.GetGlobalRect().Size / scale).DistanceTo(region.Size) < .1f,
                    $"{control.Name} follows its painted object at {size}");
            }
            Check(office.CalendarButton.TooltipText.Contains(_app.State.DateText)
                && office.SafeButton.TooltipText.Contains("₡ 2,500,000"), $"small-window tooltips include date and balance at {size}");
            Check(office.CalendarButton.GetGlobalRect().Encloses(office.CalendarDate.GetGlobalRect())
                && office.SafeButton.GetGlobalRect().Encloses(office.SafeReadout.GetGlobalRect()), $"readouts stay inside painted objects at {size}");
            await Capture($"office-{size.X}x{size.Y}");
            await Click(hotspot);
            Check(_app.StarMap.Visible, $"actual hotspot click opens the chart at {size}");
            var viewport = _app.StarMap.Viewport;
            var rect = viewport.Transform.ImageRect;
            Check(rect.Intersects(new Rect2(Vector2.Zero, viewport.Size)), $"map remains in view at {size}");
            _app.ShowOffice();
            await Frames();
            var date = _app.State.Date;
            await Click(office.CalendarButton);
            await Delay(.15);
            Check(_app.State.Date == date.AddDays(1), $"painted calendar click advances a day at {size}");
            await Click(office.ReminderButton);
            Check(_app.IsAdvancing, $"painted post-it starts time at {size}");
            await Click(office.CalendarButton);
            Check(!_app.IsAdvancing, $"calendar click stops time at {size}");
            await Click(office.SafeButton);
            Check(_app.Dialog.Heading == "Quarterly finances", $"painted safe opens finances at {size}");
            await Click(DialogButton("Close"));
            await Click(office.FilingButton);
            Check(_app.Dialog.Heading == "Campaign files", $"filing cabinet opens files at {size}");
            await Click(DialogButton("Close"));
        }
        GetWindow().Size = new Vector2I(1440, 900);
    }

    private void TestMissingArtwork()
    {
        _app.Office.SetArtwork("res://Assets/missing-office.png");
        Check(!_app.Office.ArtworkLoaded, "missing office artwork is handled");
        Check(_app.Office.Hotspot.Size.X > 0, "wall-map navigation survives missing office artwork");
        _app.Office.SetArtwork(_app.Office.ArtworkPath);
        _app.ShowMap();
        _app.StarMap.SetMap("res://Assets/missing-map.jpg");
        Check(_app.Office.WallMap.Texture is null, "wall chart clears when the shared reference cannot load");
        Check(_app.StarMap.Status.Visible && _app.StarMap.Status.Text.Contains("could not load"), "missing map is explained");
        Check(_app.StarMap.ZoomIn.Disabled && _app.StarMap.ZoomOut.Disabled && _app.StarMap.FitButton.Disabled, "map controls are disabled on load failure");
        _app.StarMap.BackButton.EmitSignal(BaseButton.SignalName.Pressed);
        Check(_app.Office.Visible, "office remains reachable after a map load failure");
        _app.StarMap.SetMap(_app.StarMap.MapPath);
        Check(_app.StarMap.Viewport.MapLoaded, "map can be restored after a load failure");
        Check(_app.Office.WallMap.Texture == _app.StarMap.Viewport.Texture, "restored reference texture returns to the wall chart");
    }

    private async Task TestCampaignUi()
    {
        _app.StartNewCompany();
        await Frames();
        Check(_app.Office.CalendarDate.Text == "01 JAN 3025" && _app.Office.QuarterText.Text == "bills due in 90 days", "calendar shows date and bills countdown");
        Check(_app.Office.SafeReadout.Text == "₡ 2,500,000", "new company balance appears in the painted safe");
        _app.State.Schedule(new("arrival", new DateOnly(3025, 1, 4), CampaignEventKind.Arrival, "Arrived at destination"));
        var delivered = 0;
        _app.EventReached += _ => delivered++;
        await Click(_app.Office.ReminderButton);
        await Delay(.4);
        Check(!_app.IsAdvancing && _app.State.Date == new DateOnly(3025, 1, 4) && delivered == 1, "reminder ticks daily and stops at scheduled event");
        Check(_app.Dialog.Visible && _app.Dialog.Heading == "Scheduled event", "event is surfaced to the player");
        await Click(DialogButton("Continue"));
        await Click(_app.Office.CalendarButton);
        await Delay(.15);
        Check(_app.State.Date == new DateOnly(3025, 1, 5) && !_app.IsAdvancing, "calendar click advances exactly one day");

        await Click(_app.Office.ReminderButton);
        await Delay(.22);
        Check(_app.IsAdvancing && _app.State.Date > new DateOnly(3025, 1, 5), "fast-forward visibly advances through dates");
        var stopped = _app.State.Date;
        MouseButton(new Vector2(5, 85), Godot.MouseButton.Left, true);
        MouseButton(new Vector2(5, 85), Godot.MouseButton.Left, false);
        await Delay(.15);
        Check(!_app.IsAdvancing && _app.State.Date == stopped, "click on empty space halts time");
        foreach (var control in new[] { _app.Office.CalendarButton, _app.Office.ReminderButton })
        {
            await Click(_app.Office.ReminderButton);
            stopped = _app.State.Date;
            await Click(control);
            await Delay(.15);
            Check(!_app.IsAdvancing && _app.State.Date == stopped, $"{control.Name} stopping click does not restart time");
        }
        await Click(_app.Office.ReminderButton);
        await Click(_app.Office.SafeButton);
        Check(!_app.IsAdvancing && _app.Dialog.Heading == "Quarterly finances", "safe click both stops time and opens finances");
        for (var index = 0; index < 5; index++)
        {
            await PressKey(Key.Tab);
            Check(_app.Dialog.IsAncestorOf(GetViewport().GuiGetFocusOwner()), "modal retains keyboard focus");
        }
        foreach (var key in new[] { Key.Left, Key.Right, Key.Up, Key.Down })
        {
            await PressKey(key);
            Check(_app.Dialog.IsAncestorOf(GetViewport().GuiGetFocusOwner()), "arrow navigation stays inside modal");
        }
        await Capture("finances");
        GetWindow().Size = new Vector2I(900, 500);
        await Frames();
        await Capture("finances-900x500");
        GetWindow().Size = new Vector2I(1440, 900);
        await Frames();
        await Click(DialogButton("Close"));
        Check(!System.IO.File.Exists(_saveFile), "UI advancement does not autosave");

        while (_app.State.Date < new DateOnly(3025, 3, 31)) _app.State.AdvanceOneDay();
        _app.Office.RefreshCampaign(_app.State);
        Check(_app.Office.QuarterText.Text == "bills due in 1 day", "calendar uses singular day for the final day");
        await Click(_app.Office.CalendarButton);
        await Delay(.15);
        Check(_app.State.PaymentPending && _app.State.Date == new DateOnly(3025, 4, 1), "quarter boundary blocks animated advancement");
        Check(_app.Office.CalendarButton.Disabled && _app.Office.ReminderButton.Disabled && _app.Office.QuarterText.Text == "Payment Due", "pending quarter disables controls and shows payment due");
        _app.Office.RefreshCampaign(_app.State);
        _app.Office._Process(.46);
        Check(_app.Office.SafeReadout.Modulate.A < .5f && _app.Office.SafeButton.Modulate == Colors.White,
            "only the digital balance blinks while payment is pending");
        _app.Office._Process(.45);
        Check(_app.Office.SafeReadout.Modulate == Colors.White, "digital balance returns to its bright phase");
        Check(_app.Office.QuarterText.GetThemeColor("font_color") == new Color("ff6c63"), "payment due is red");
        await Capture("payment-due");
        await Click(_app.Office.FilingButton);
        Check(_app.Dialog.Heading == "Campaign files", "cabinet opens save/load menu");
        await Click(DialogButton("Save Game"));
        Check(System.IO.File.Exists(_saveFile) && _app.Dialog.Heading == "Company saved", "first manual save writes campaign");
        await Click(DialogButton("Close"));
        await Click(_app.Office.SafeButton);
        await Click(DialogButton("Pay Later"));
        Check(_app.Dialog.Heading == "Defer quarterly payment?" && _app.State.PaymentPending, "warning precedes deferral");
        await Click(DialogButton("Cancel"));
        Check(_app.State.PaymentPending && _app.State.Cash == 2_500_000, "cancel warning preserves payment block");
        await Click(DialogButton("Pay Later"));
        await Click(DialogButton("Pay Later"));
        Check(!_app.State.PaymentPending && _app.State.DeferredDebt == 100_000 && _app.State.Cash == 2_500_000, "confirmed deferral preserves cash");
        Check(_app.Office.QuarterText.Text == "bills due in 91 days" && _app.Office.QuarterText.GetThemeColor("font_color") == new Color("ff6c63"), "deferred countdown stays red");
        Check(_app.Office.SafeReadout.Modulate == Colors.White && _app.Office.SafeReadout.Text == "₡ 2,500,000", "safe stops blinking and preserves balance after deferral");
        await Capture("payment-deferred");

        await Click(_app.Office.FilingButton);
        await Click(DialogButton("Load Game"));
        Check(_app.Dialog.Heading == "Load saved company?", "load asks before discarding progress");
        await Click(DialogButton("Load Game"));
        Check(_app.State.PaymentPending && _app.State.DeferredDebt == 0 && _app.Office.QuarterText.Text == "Payment Due", "load restores unpaid quarter and calendar block");
        await Click(_app.Office.SafeButton);
        await Click(DialogButton("Pay Now"));
        Check(_app.State.Cash == 2_400_000 && _app.State.CanAdvance && !_app.Dialog.Visible, "Pay Now collects full bill and unlocks time");
        Check(_app.Office.SafeReadout.Text == "₡ 2,400,000" && _app.Office.SafeButton.TooltipText.Contains("₡ 2,400,000"), "payment refreshes safe balance and tooltip");
        Check(_app.Office.QuarterText.GetThemeColor("font_color") == Ui.Gold, "full payment restores normal countdown color");
        await Click(_app.Office.FilingButton);
        await Click(DialogButton("Save Game"));
        Check(_app.Dialog.Heading == "Overwrite campaign save?", "overwrite requires confirmation");
        await Click(DialogButton("Save Game"));
        Check(new CampaignStore(_saveFile).Load().Cash == 2_400_000, "confirmed overwrite persists changed finances");
        await Click(DialogButton("Close"));

        var deadline = Campaign.Restore(new Campaign().Snapshot() with
        {
            Date = new DateOnly(3025, 6, 30), Cash = 199_999,
            DeferredDebt = 100_000, NextQuarter = new DateOnly(3025, 7, 1)
        });
        new CampaignStore(_saveFile).Save(deadline);
        var beforeBankruptcy = System.IO.File.ReadAllText(_saveFile);
        await Click(_app.Office.FilingButton);
        await Click(DialogButton("Load Game"));
        await Click(DialogButton("Load Game"));
        Check(_app.Office.SafeReadout.Text == "₡ 199,999", "loading a different company balance refreshes the safe");
        await Click(_app.Office.CalendarButton);
        await Delay(.15);
        Check(_app.State.Bankrupt && _app.Dialog.Heading == "Company bankrupt", "grace deadline opens bankruptcy dialog");
        await Capture("bankruptcy");
        await Click(DialogButton("Main Menu"));
        Check(_app.MainMenu.Visible && !_app.Office.Visible && !_app.StarMap.Visible, "bankruptcy dismissal returns to menu");
        Check(System.IO.File.ReadAllText(_saveFile) == beforeBankruptcy, "bankruptcy preserves manual save");
        await Click(_app.MainMenu.LoadButton);
        Check(_app.Office.Visible && _app.State.Date == deadline.Date && _app.State.DeferredDebt == 100_000, "main menu loads complete saved campaign");
        _app.ShowMainMenu();
        await Frames();
        await Click(_app.MainMenu.NewButton);
        Check(_app.State.Date == Campaign.StartDate && _app.State.Cash == Campaign.StartingCash, "New Company resets state");
        Check(_app.Office.SafeReadout.Text == "₡ 2,500,000" && _app.Office.SafeReadout.Modulate == Colors.White, "New Company resets the painted balance and stops blinking");
        Check(System.IO.File.ReadAllText(_saveFile) == beforeBankruptcy, "New Company preserves manual save");
        System.IO.File.WriteAllText(_saveFile, "invalid save");
        var live = _app.State;
        await Click(_app.Office.FilingButton);
        await Click(DialogButton("Load Game"));
        await Click(DialogButton("Load Game"));
        Check(_app.Dialog.Heading == "Unable to load" && ReferenceEquals(_app.State, live), "corrupt load keeps live company intact");
        await Click(DialogButton("Close"));
    }

    private Button DialogButton(string text) => _app.Dialog.FindChildren("*", "Button", true, false)
        .OfType<Button>().Single(button => button.Text == text && button.IsInsideTree());

    private async Task Click(Control control)
    {
        var at = control.GetGlobalRect().GetCenter();
        MouseMotion(at, Vector2.Zero);
        MouseButton(at, Godot.MouseButton.Left, true);
        MouseButton(at, Godot.MouseButton.Left, false);
        await Frames();
    }

    private async Task Delay(double seconds)
    {
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        await Frames();
    }

    private async Task PressKey(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = true });
        await Frames();
        Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = false });
        await Frames();
    }

    private static void MouseMotion(Vector2 at, Vector2 delta) => Input.ParseInputEvent(new InputEventMouseMotion
    {
        Position = at, GlobalPosition = at, Relative = delta,
        ButtonMask = Input.IsMouseButtonPressed(Godot.MouseButton.Left) ? MouseButtonMask.Left : 0
    });

    private static void MouseButton(Vector2 at, Godot.MouseButton button, bool pressed, float factor = 1) => Input.ParseInputEvent(new InputEventMouseButton
    {
        Position = at, GlobalPosition = at, ButtonIndex = button, Pressed = pressed, Factor = factor
    });

    private async Task Frames()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++;
    }

    private void Near(float actual, float expected, string message) => Check(Math.Abs(actual - expected) < .01f, $"{message}: expected {expected}, got {actual}");

    private async Task Capture(string name)
    {
        if (!_screenshots || DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://TestResults"));
        var result = GetViewport().GetTexture().GetImage().SavePng($"res://TestResults/{name}.png");
        Check(result == Error.Ok, $"saved {name} screenshot");
    }
}
