using Godot;

namespace Mech.Tests;

public partial class SmokeTests : Node
{
    private CommandApp _app = null!;
    private int _checks;
    private bool _screenshots;
    /// <summary>The app's own save folder (holding <c>solo</c> and <c>coop</c>) and its pre-slot save path, both temporary.</summary>
    private string _saveRoot = "", _legacyFile = "", _settingsFile = "";
    // Every app drafts from this seed, so a failure in a drafted group reproduces; `--draft-seed=N` tries another company.
    private int _draftSeed = 3025;

    /// <summary>How a test group starts: model checks only, a fresh app at the main menu, or a fresh app with a drafted company.</summary>
    private enum Setup { None, Menu, Drafted }

    /// <summary>A screen-sized slice of the suite. Each group must pass alone; see docs/testing.md.</summary>
    private sealed record TestGroup(string Name, Setup Setup, Func<Task> Run, bool InFullRun = true);

    // Engine-free suites (catalog, combat-sim and the model checks behind several groups) run only in Tests.Unit;
    // scripts/mech test runs them alongside these groups. See docs/testing.md.
    private TestGroup[] Groups() =>
    [
        new("menu", Setup.Menu, async () =>
        {
            await TestMainMenuStart();
            await Click(_app.MainMenu.NewButton); await FinishDraft();
            // Unsaved-change checks come last: exit checks rely on the clean company they leave behind.
            await TestBackNavigationUi(); await TestNavigationTransitions(); await TestMenuResumeUi(); await TestUnsavedChangesUi();
        }),
        new("draft", Setup.Menu, async () =>
        {
            await Click(_app.MainMenu.NewButton); await TestScenarioUi(); await TestDraftUi(); await TestLegacyDraftUi();
        }),
        new("office", Setup.Drafted, async () => { await TestOfficeUi(); TestMissingArtwork(); }),
        new("map", Setup.Drafted, async () =>
        {
            TestGeometry();
            await TestStarMapUi(); await TestShipLocatorUi(); await TestTravelUi();
        }),
        new("roster", Setup.Drafted, TestRosterUi),
        new("hangar", Setup.Drafted, async () => { await TestHangarAssignmentsUi(); await TestHangarScreen(); await TestHangarLoadout(); await TestHangarInstall(); await TestHangarArmor(); await TestHangarMarket(); await TestHangarRepair(); }),
        new("missions", Setup.Menu, TestMissionsUi),
        new("finance", Setup.Menu, TestCampaignUi),
        new("combat-layout", Setup.None, TestCombatLayouts, InFullRun: false),
        new("shot-effects", Setup.Drafted, TestShotEffects, InFullRun: false),
        new("combat-ui", Setup.Drafted, async () =>
        {
            await TestCombatLayouts(); await TestBattlefieldPicking(); await TestDetectionOverlay(); await TestShotEffects();
            await TestCombatUi(); await TestQuickLaunchUi(); await TestSalvageUi(); await TestCombatJumping(); await TestPatrolUi(); await TestStressUi();
        }),
        new("mech-visuals", Setup.None, async () =>
        {
            await TestMechVisuals(); await TestChassisVisuals(); await TestAdditionalChassis(); await TestFinalChassis(); await TestAdditionalBatch(); await TestWeaponMounts(); await TestJumpVisuals(); await TestStandUpVisuals();
        }),
        new("layout", Setup.Drafted, TestResizing),
        new("audio", Setup.Menu, async () => { await TestAudioUi(); await TestCombatAudio(); }),
        new("multiplayer", Setup.Menu, async () =>
        {
            await TestHostingUi();
            await StopApp(); await StartApp(false); await TestJoiningUi();
        }),
    ];

    public override async void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        var groups = Groups();
        if (args.Contains("--list-groups"))
        {
            foreach (var group in groups) GD.Print(group.InFullRun ? group.Name : $"{group.Name} (subset; not in the full run)");
            GetTree().Quit(0); return;
        }
        // An exit check leaves the last app running, then ends the process through a quit path.
        var exitCheck = args.Contains("--discard-quit") || args.Contains("--clean-window-quit") || args.Contains("--menu-quit");
        try
        {
            _screenshots = args.Contains("--screenshots");
            if (args.FirstOrDefault(arg => arg.StartsWith("--draft-seed=")) is { } seed) _draftSeed = int.Parse(seed["--draft-seed=".Length..]);
            // Synthetic headless mouse events need the enter notification a desktop window normally supplies.
            if (DisplayServer.GetName() == "headless") GetViewport().NotifyMouseEntered();
            var requested = args.FirstOrDefault(arg => arg.StartsWith("--groups="))?["--groups=".Length..]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var unknown = requested?.Where(name => groups.All(group => group.Name != name)).ToArray() ?? [];
            if (unknown.Length > 0)
                throw new ArgumentException($"Unknown test group {string.Join(", ", unknown)}; valid groups: {string.Join(", ", groups.Select(group => group.Name))}");
            var selected = requested is null ? groups.Where(group => group.InFullRun).ToArray()
                : groups.Where(group => requested.Contains(group.Name)).ToArray();
            for (var i = 0; i < selected.Length; i++)
            {
                var group = selected[i];
                var before = _checks;
                if (group.Setup != Setup.None) await StartApp(group.Setup == Setup.Drafted);
                await group.Run();
                if (group.Setup != Setup.None && !(exitCheck && i == selected.Length - 1)) await StopApp();
                GD.Print($"  {group.Name}: {_checks - before} checks");
            }
            GD.Print($"PASS: {_checks} checks [{string.Join(", ", selected.Select(group => group.Name))}]");
            if (exitCheck) { await RunExitCheck(); return; }
            GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PushError($"FAIL after {_checks} checks (draft seed {_draftSeed}): {error}");
            GetTree().Quit(1);
        }
        finally
        {
            DeleteSaves();
        }
    }

    /// <summary>Starts an isolated app with its own temporary save, optionally through a completed draft into the office.</summary>
    private async Task StartApp(bool drafted)
    {
        GetWindow().Size = new Vector2I(1440, 900);
        _app = ResourceLoader.Load<PackedScene>("res://Scenes/Command.tscn").Instantiate<CommandApp>();
        var id = Guid.NewGuid().ToString("N");
        _app.SaveRoot = $"user://smoke-{id}"; _app.LegacySavePath = $"user://smoke-{id}.json";
        _saveRoot = ProjectSettings.GlobalizePath(_app.SaveRoot); _legacyFile = ProjectSettings.GlobalizePath(_app.LegacySavePath);
        _app.SettingsPath = $"user://smoke-{id}.cfg"; _settingsFile = ProjectSettings.GlobalizePath(_app.SettingsPath);
        AddChild(_app);
        _app.Drafting.DraftRandom = new Random(_draftSeed);
        await Frames();
        if (drafted) { await Click(_app.MainMenu.NewButton); await FinishDraft(); }
    }

    private async Task StopApp()
    {
        await Frames();
        _app.QueueFree();
        await Frames();
        DeleteSaves();
        GetWindow().Size = new Vector2I(1440, 900);
    }

    /// <summary>Exit paths run in their own process because success terminates the game; an external timeout catches a failed exit.</summary>
    private async Task RunExitCheck()
    {
        var args = OS.GetCmdlineUserArgs();
        if (args.Contains("--discard-quit"))
        {
            _app.State.AdvanceOneDay(); _app.ShowMainMenu(); await Frames();
            await Click(_app.MainMenu.QuitButton);
            // Return through finally to remove the temporary save before the engine exits.
            DialogButton("Quit Without Saving").EmitSignal(BaseButton.SignalName.Pressed);
            return;
        }
        if (args.Contains("--clean-window-quit"))
            GetWindow().EmitSignal(Window.SignalName.CloseRequested);
        else
        {
            _app.ShowMainMenu(); await Frames();
            _app.MainMenu.QuitButton.EmitSignal(BaseButton.SignalName.Pressed);
        }
        Check(!_app.UnsavedDialog.Visible, "clean quit skips confirmation");
    }

    private async Task TestMainMenuStart()
    {
        Check(_app.MainMenu.Visible && !_app.Office.Visible && !_app.StarMap.Visible, "starts in the main menu");
        Check(_app.MainMenu.LoadButton.Disabled, "load disabled before first manual save");
        await PressKey(Key.Escape); await RightClick(new Vector2(20, 20));
        Check(_app.MainMenu.Visible && !_app.Office.Visible, "main menu back gestures do nothing without a company to resume");
        await Capture("main-menu");
    }

    private async Task TestOfficeUi()
    {
        Check(_app.Office.Visible && !_app.StarMap.Visible, "starts in the office");
        Check(_app.Office.ArtworkLoaded && _app.StarMap.Viewport.MapLoaded, "both local images load");
        Check(_app.Office.Hotspot.HasFocus(), "initial keyboard focus is on the wall map");
        var wallMapTexture = _app.Office.WallMap.Texture;
        Check(wallMapTexture is not null && wallMapTexture == _app.StarMap.Viewport.Texture, "wall chart shares the complete reference texture with the opened map");
        Near(wallMapTexture!.GetSize().X / wallMapTexture.GetSize().Y, 7f / 6, "wall chart uses the full 7:6 reference image");
        Check(_app.Office.WallMap.StretchMode == TextureRect.StretchModeEnum.KeepAspectCentered, "wall chart fits the image without cropping or distortion");
        await Capture("office");
    }

    private async Task TestStarMapUi()
    {
        _app.Office.Hotspot.GrabFocus(); await Frames();
        var wallMapRect = _app.Office.WallMap.GetRect();
        var wallMapTexture = _app.Office.WallMap.Texture;
        await PressKey(Key.Enter);
        Check(_app.StarMap.Visible && !_app.Office.Visible, "keyboard activation opens the chart");
        Check(_app.StarMap.Viewport.HasFocus(), "map receives keyboard focus");
        var galatea = StarChart.Instance[StarChart.Instance.StartingSystemId];
        Check(MapViewport.SystemTooltip(galatea, _app.State) == "Galatea · Volcanic\nOwner: Lyran Commonwealth · Neutral 0\nRegressed tech · Developing\nClick to plot a journey",
            "system tooltip names the owner, your standing with it and the market ratings");
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
            Near(GetWindow().GetFinalTransform().X.X, Math.Max(1, Math.Min(size.X / 1440f, size.Y / 900f)),
                $"interface scales up with the window but never below full size at {size}");
            var office = _app.Office;
            var art = office.ArtworkRect;
            var hotspot = office.Hotspot;
            Check(art.Size.X > 0 && art.Size.Y > 0, $"office remains visible at {size}");
            Near(art.Size.X / art.Size.Y, 1672f / 940, $"office aspect ratio at {size}");
            var scale = art.Size.X / 1672;
            foreach (var (control, region) in new (Control, Rect2)[]
            {
                (hotspot, OfficeScreen.ChartRegion), (office.CalendarButton, OfficeScreen.CalendarRegion),
                (office.SnoozeButton, OfficeScreen.SnoozeRegion), (office.SafeButton, OfficeScreen.SafeRegion),
                (office.FilingButton, OfficeScreen.FilingRegion), (office.WallMap, OfficeScreen.MapRegion), (office.LocationStrip, OfficeScreen.LocationRegion),
                (office.PhoneButton, OfficeScreen.PhoneRegion), (office.RolodexButton, OfficeScreen.RolodexRegion), (office.HangarButton, OfficeScreen.HangarRegion),
                (office.JacketButton, OfficeScreen.JacketRegion)
            })
            {
                Check(((control.Position - art.Position) / scale).DistanceTo(region.Position) < .1f
                    && (control.GetGlobalRect().Size / scale).DistanceTo(region.Size) < .1f,
                    $"{control.Name} follows its painted object at {size}");
            }
            Check(office.LocationStrip.Text == _app.State.Travel.LocationText && office.LocationStrip.Text.Length > 0
                && office.WallMap.GetGlobalRect().Grow(1).Encloses(office.LocationStrip.GetGlobalRect()),
                $"current location shows along the bottom of the wall chart at {size}");
            Check(office.CalendarButton.TooltipText.Contains(_app.State.DateText)
                && office.SafeButton.TooltipText.Contains("₡ 2,500,000"), $"small-window tooltips include date and balance at {size}");
            Check(office.CalendarButton.GetGlobalRect().Encloses(office.CalendarDate.GetGlobalRect())
                && office.SafeButton.GetGlobalRect().Encloses(office.SafeReadout.GetGlobalRect()), $"readouts stay inside painted objects at {size}");
            Check(office.GetGlobalRect().Position == Vector2.Zero && _app.FindChildren("*", "Button", true, false).OfType<Button>()
                .All(button => button.Text != "Main menu"), $"office fills the window without a header bar at {size}");
            Vector2 ArtPoint(Vector2 source) => hotspot.GetParent<Control>().GetGlobalTransform() * (art.Position + source * scale);
            Check(office.HangarButton.ContainsGlobal(ArtPoint(new(1600, 248)))
                && !office.HangarButton.ContainsGlobal(ArtPoint(OfficeScreen.HangarRegion.Position + new Vector2(4, 4)))
                && office.JacketButton.ContainsGlobal(ArtPoint(new(1340, 300)))
                && !office.JacketButton.ContainsGlobal(ArtPoint(OfficeScreen.JacketRegion.Position + new Vector2(4, 4)))
                && !office.RolodexButton.ContainsGlobal(ArtPoint(new(702, 556))),
                $"office hotspots follow painted outlines, not their bounding boxes, at {size}");
            await Capture($"office-{size.X}x{size.Y}");
            await Click(office.PhoneButton);
            Check(_app.Missions.Visible && _app.Missions.GetGlobalRect().Encloses(_app.Missions.CloseButton.GetGlobalRect())
                && _app.Missions.GetGlobalRect().Encloses(_app.Missions.AcceptButton.GetGlobalRect()), $"phone and mission actions remain usable at {size}");
            await Capture($"missions-{size.X}x{size.Y}");
            await PressKey(Key.Escape);
            await Click(hotspot);
            Check(_app.StarMap.Visible, $"actual hotspot click opens the chart at {size}");
            var viewport = _app.StarMap.Viewport;
            var rect = viewport.Transform.ImageRect;
            Check(rect.Intersects(new Rect2(Vector2.Zero, viewport.Size)), $"map remains in view at {size}");
            _app.ShowOffice();
            await Frames();
            var date = _app.State.Date;
            await Click(office.SnoozeButton);
            await Delay(.15);
            Check(_app.State.Date == date.AddDays(1), $"painted Snooze post-it advances a day at {size}");
            await Click(office.CalendarButton);
            var screen = GetViewport().GetVisibleRect();
            Check(_app.Calendar.Visible && _app.Calendar.Rows.Count > 0 && screen.Encloses(_app.Calendar.Rows[0].GetGlobalRect())
                && screen.Encloses(_app.Calendar.CloseButton.GetGlobalRect()) && screen.Encloses(_app.Calendar.NextDayButton.GetGlobalRect()),
                $"painted calendar opens the upcoming events on screen at {size}");
            await Capture($"calendar-{size.X}x{size.Y}");
            await Click(_app.Calendar.Rows[^1]);
            Check(_app.IsAdvancing && !_app.Calendar.Visible, $"an upcoming event starts time at {size}");
            await Click(office.SnoozeButton);
            Check(!_app.IsAdvancing, $"Snooze click stops time at {size}");
            await Click(office.SafeButton);
            Check(_app.Dialog.Heading == "Quarterly finances", $"painted safe opens finances at {size}");
            await Click(DialogButton("Close"));
            await Click(office.FilingButton);
            Check(_app.Saves.Visible && GetViewport().GetVisibleRect().Encloses(_app.Saves.CloseButton.GetGlobalRect())
                && GetViewport().GetVisibleRect().Encloses(SlotButton(SaveBrowser.PageSize - 1).GetGlobalRect()), $"filing cabinet opens the save grid on screen at {size}");
            await Capture($"save-grid-{size.X}x{size.Y}");
            await Click(_app.Saves.CloseButton);
        }
        GetWindow().Size = new Vector2I(1440, 900);
    }

    private async Task TestMissionsUi()
    {
        _app.StartNewCompany(); await FinishDraft();
        var office = _app.Office;
        var board = _app.State.Missions;
        Check(office.PhoneFlashing && board.HasUnseenOffers, "phone indicates the initial unseen offers");
        office.RefreshCampaign(_app.State);
        for (var tick = 0; tick < 6; tick++) { office.RefreshCampaign(_app.State); office._Process(.1); }
        Check(!office.PhoneLight.Visible && office.PhoneButton.Modulate == Colors.White, "silent phone light keeps flashing through campaign refreshes");
        office.RefreshCampaign(_app.State);
        await Click(office.PhoneButton);
        Check(_app.Missions.Visible && office.Visible && !board.HasUnseenOffers && !office.PhoneFlashing && !office.PhoneLight.Visible,
            "painted phone opens mission overlay and marks list seen");
        var oldest = board.Available[0];
        Check(_app.Missions.MissionName.Text == oldest.Name && board.Current is null && _app.Missions.MissionButtons.Count == MissionTuning.BoardSize,
            "the oldest of ten offers opens first without accepting it");
        Check(_app.Missions.TermsText.Contains($"BIOME  {StarChart.Instance[oldest.SystemId].BiomeText}"), "mission terms show the host system's biome");
        Check(_app.Missions.ReputationLabels.Select(label => label.Text).SequenceEqual(Factions.All.Select(faction => $"{faction.Name} · Neutral 0")),
            "the phone lists a neutral standing with each faction");
        Check(_app.Missions.TermsText.StartsWith($"EMPLOYER  {oldest.Employer.Name}") && _app.Missions.TermsText.Contains($"\nOPPONENT  {oldest.Opponent.Name}")
            && _app.Missions.MissionButtons.All(button => button.Text.Contains(" vs. ")), "offers show the employer and opponent");
        Check(_app.Missions.GetGlobalRect().Encloses(_app.Missions.AcceptButton.GetGlobalRect()), "first-open mission action stays inside the window");
        await Click(office.SafeButton);
        Check(!_app.Dialog.Visible && _app.Missions.Visible, "mission overlay blocks office clicks and does not dismiss on backdrop click");
        _app.StartAdvancing(false);
        Check(!_app.IsAdvancing, "mission panel blocks time controls");
        _app.Missions.CloseButton.GrabFocus();
        foreach (var key in new[] { Key.Tab, Key.Tab, Key.Right, Key.Down, Key.Tab })
        {
            await PressKey(key);
            Check(_app.Missions.IsAncestorOf(GetViewport().GuiGetFocusOwner()), "mission panel traps keyboard focus");
        }
        await Capture("missions-available");
        await PressKey(Key.Escape);
        Check(!_app.Missions.Visible && !_app.MainMenu.Visible && office.PhoneButton.HasFocus(), "Escape closes mission panel and restores phone focus");
        var cash = _app.State.Cash; var date = _app.State.Date;
        await PressKey(Key.Enter); await Click(_app.Missions.AcceptButton);
        Check(board.Current == oldest && !_app.Missions.Visible && office.Visible && office.PhoneButton.HasFocus(),
            "acceptance returns to office with selected mission");
        Check(_app.State.Cash == cash && _app.State.Date == date, "acceptance changes neither money nor time");
        await SaveFromFiling();
        await Click(office.PhoneButton);
        Check(_app.Missions.MissionButtons.Count == MissionTuning.BoardSize && _app.Missions.MissionName.Text == oldest.Name
            && _app.Missions.AbandonButton.Visible && !_app.Missions.AcceptButton.Visible,
            "reopening defaults to current mission, which has left the board");
        await Capture("missions-current");
        await Click(_app.Missions.AbandonButton);
        Check(_app.Dialog.Heading == "Abandon mission?" && board.Current is not null, "abandonment requires confirmation");
        await PressKey(Key.Escape);
        Check(!_app.Dialog.Visible && _app.Missions.Visible && board.Current is not null && _app.Missions.AbandonButton.HasFocus(),
            "Escape cancels only abandonment confirmation and restores mission focus");
        await Click(_app.Missions.AbandonButton); await Click(DialogButton("Abandon Mission"));
        Check(board.Current is null && board.Find(oldest.Id) is null && _app.Missions.Visible && _app.Missions.AcceptButton.Visible
            && _app.Missions.MissionButtons.Count == MissionTuning.BoardSize - 1 && !office.PhoneFlashing,
            "confirmed abandonment removes the mission for good without flashing");
        Check(_app.State.Cash == cash && _app.State.Date == date, "abandonment has no penalty or time cost");
        await Click(_app.Missions.CloseButton);
        await LoadFromFiling();
        Check(_app.State.Missions.Current?.Id == oldest.Id && !office.PhoneFlashing, "manual load restores current mission and seen state");
        await Click(office.PhoneButton);
        var other = _app.State.Missions.Available[1];
        await Click(_app.Missions.MissionButtons.First(button => button.TooltipText.StartsWith($"Posted {_app.State.DateText}\n") && button.Text.StartsWith(other.Name + "\n")));
        Check(_app.Missions.MissionName.Text == other.Name && _app.Missions.AcceptButton.Visible && _app.Missions.AcceptButton.Disabled
            && !_app.Missions.AbandonButton.Visible && _app.State.Missions.Current?.Id == oldest.Id, "other offers can be perused without replacing current mission");
        await Capture("missions-other-offer");
        await Click(_app.Missions.CloseButton);
        _app.State.Missions.Abandon(); _app.State.AdvanceOneDay(); _app.ShowOffice(); await Frames();
        Check(_app.State.Missions.Available.Count == MissionTuning.BoardSize && office.PhoneFlashing,
            "the next day refills the board and the phone flashes for the new offer");
        ClearSaves();
    }

    private async Task TestBackNavigationUi()
    {
        _app.ShowOffice(); await Frames();
        var date = _app.State.Date;
        _app.StartAdvancing(true);
        await RightClick(_app.Office.CalendarButton.GetGlobalRect().GetCenter());
        Check(!_app.IsAdvancing && _app.State.Date == date && !_app.Office.Visible && _app.MainMenu.Visible,
            "office right-click stops time and opens the main menu");
        await Click(_app.MainMenu.ResumeButton);
        Check(_app.Office.Visible && !_app.IsAdvancing, "Resume returns to the office with time paused after right-click");
        _app.ShowMap(); await Frames();
        await RightClick(_app.StarMap.Search.GetGlobalRect().GetCenter());
        Check(_app.Office.Visible && !_app.StarMap.Visible && _app.Office.Hotspot.HasFocus(),
            "right-click over map search returns to office and restores wall-chart focus");
        _app.ShowMap(); await Frames();
        var viewport = _app.StarMap.Viewport;
        var at = viewport.GlobalPosition + viewport.Size / 2;
        MouseButton(at, Godot.MouseButton.Left, true); await Frames();
        Check(viewport.IsDragging, "map captures a held pan before back navigation");
        await RightClick(at); MouseButton(at, Godot.MouseButton.Left, false); await Frames();
        Check(!viewport.IsDragging && _app.Office.Visible, "right-click ends dragging and consumes the navigation click");
        _app.ShowWarriors(); await Frames(); await RightClick(_app.Rolodex.CardName.GetGlobalRect().GetCenter());
        Check(!_app.Rolodex.Visible && _app.Office.RolodexButton.HasFocus(), "office personnel right-click restores the rolodex hotspot");
        _app.ShowHangar(); await Frames(); _app.Hangar.Select(1);
        var selected = _app.Hangar.SelectedMech;
        await Click(_app.Hangar.WarriorsButton);
        Check(_app.Rolodex.Visible && _app.Hangar.Visible && _app.Rolodex.BackButton.Text == "Back to hangar", "hangar personnel remembers its parent");
        await RightClick(_app.Rolodex.CardName.GetGlobalRect().GetCenter());
        Check(!_app.Rolodex.Visible && _app.Hangar.Visible && !_app.Office.Visible
            && _app.Hangar.SelectedMech == selected && _app.Hangar.WarriorsButton.HasFocus(),
            "one right-click returns to unchanged hangar selection");
        await Click(_app.Hangar.WarriorsButton); await PressKey(Key.Escape);
        Check(!_app.Rolodex.Visible && _app.Hangar.Visible, "nested personnel Escape also returns one level");
        await Click(_app.Hangar.WarriorsButton); await Click(_app.Rolodex.BackButton);
        Check(!_app.Rolodex.Visible && _app.Hangar.Visible, "nested personnel Back button uses the same parent");
        await Click(_app.Hangar.PilotPicker);
        var popup = _app.Hangar.PilotPicker.GetPopup();
        Check(popup.Visible, "pilot picker opens a popup above the hangar");
        await RightClick(_app.Hangar.PilotPicker.GetGlobalRect().GetCenter());
        Check(!popup.Visible && _app.Hangar.Visible && !_app.Office.Visible, "right-click closes only the topmost popup");
        await RightClick(_app.Hangar.BackButton.GetGlobalRect().GetCenter());
        Check(!_app.Hangar.Visible && _app.Office.Visible && _app.Office.HangarButton.HasFocus(), "second back returns from hangar to office");
        _app.ShowMissions(); await Frames(); await RightClick(_app.Missions.CloseButton.GetGlobalRect().GetCenter());
        Check(!_app.Missions.Visible && _app.Office.PhoneButton.HasFocus(), "mission right-click returns to phone in office");
        _app.ShowFinances(); await Frames(); await RightClick(DialogButton("Close").GetGlobalRect().GetCenter());
        Check(!_app.Dialog.Visible && _app.Office.SafeButton.HasFocus(), "finance right-click closes without taking an action");
        SeedSave(_app.State);
        var saved = SaveFingerprint();
        _app.ShowFiling(); await Frames(); await Click(SlotButton(1));
        Check(_app.SaveDialog.Visible && _app.SaveDialog.Heading == "Overwrite save?" && _app.Saves.Visible, "the save name prompt forms a second navigation level");
        await RightClick(DialogButton("Save").GetGlobalRect().GetCenter());
        Check(!_app.SaveDialog.Visible && _app.Saves.Visible && SaveFingerprint() == saved && SlotButton(1).HasFocus(),
            "right-click cancels the overwrite and returns only to the save grid without changing the save");
        await Click(SlotButton(0)); await PressKey(Key.Escape);
        Check(!_app.SaveDialog.Visible && _app.Saves.Visible && SaveFingerprint() == saved, "Escape in the name field cancels only the prompt");
        await RightClick(_app.Saves.CloseButton.GetGlobalRect().GetCenter());
        Check(!_app.Saves.Visible && _app.Office.FilingButton.HasFocus(), "another right-click closes the save grid and restores cabinet focus");
        ClearSaves();
        await Click(_app.Office.JacketButton);
        Check(_app.MainMenu.Visible && !_app.Office.Visible, "jacket opens the main menu");
        await RightClick(new Vector2(20, 20));
        Check(!_app.MainMenu.Visible && _app.Office.Visible && _app.Office.JacketButton.HasFocus(),
            "main menu right-click returns to the office and restores jacket focus");
    }

    private async Task TestShipLocatorUi()
    {
        _app.ShowMap(); await Frames();
        var map = _app.StarMap; var viewport = map.Viewport;
        var travel = System.Text.Json.JsonSerializer.Serialize(_app.State.Travel.Snapshot());
        viewport.Fit(); await Capture("dropship-overview");
        viewport.ZoomBy(12); viewport.FindDropShip();
        Check(viewport.ShipViewportPosition.DistanceTo(viewport.Size / 2) < .1f, "Find DropShip centers current orbit at detail zoom");
        var zoom = viewport.Transform.Zoom;
        viewport.Transform.Pan(new Vector2(100000, 100000)); viewport.ZoomBy(1); await Frames();
        Check(viewport.ShipIsOffscreen && new Rect2(Vector2.Zero, viewport.Size).Encloses(viewport.ShipIndicatorRect),
            "offscreen DropShip has a fully visible edge locator");
        await Capture("dropship-offscreen");
        var arrow = viewport.GlobalPosition + viewport.ShipIndicatorPosition;
        MouseMotion(arrow, Vector2.Zero); MouseButton(arrow, Godot.MouseButton.Left, true);
        MouseButton(arrow, Godot.MouseButton.Left, false); await Frames();
        Check(!viewport.ShipIsOffscreen && viewport.ShipViewportPosition.DistanceTo(viewport.Size / 2) < .1f,
            "clicking edge locator centers the ship");
        Near(viewport.Transform.Zoom, zoom, "edge locator keeps zoom");
        viewport.Transform.Pan(new Vector2(0, 100000)); viewport.ZoomBy(1); await Frames();
        var controls = map.FindShipButton.GetParent<Control>();
        var controlsOnMap = new Rect2(controls.GlobalPosition - viewport.GlobalPosition, controls.Size);
        Check(viewport.ShipIsOffscreen && !viewport.ShipIndicatorRect.Intersects(controlsOnMap),
            "bottom-edge ship locator remains clear of zoom controls");
        await Click(map.FindShipButton);
        viewport.Transform.Pan(new Vector2(-100000, -100000)); viewport.ZoomBy(1); await Frames();
        await Click(map.FindShipButton);
        Check(!viewport.ShipIsOffscreen, "Find DropShip button locates a ship outside the viewed region");
        Near(viewport.Transform.Zoom, zoom, "Find DropShip button keeps zoom");
        Check(System.Text.Json.JsonSerializer.Serialize(_app.State.Travel.Snapshot()) == travel, "locating ship leaves candidate and embarked routes unchanged");
        var offset = viewport.Transform.Offset;
        await RightClick(viewport.GlobalPosition + viewport.Size / 2); _app.ShowMap(); await Frames();
        Check(viewport.Transform.Offset.IsEqualApprox(offset) && viewport.Transform.Zoom == zoom, "reopening chart preserves chosen view");
        foreach (var size in new[] { new Vector2I(900, 500), new Vector2I(1440, 900) })
        {
            GetWindow().Size = size; await Frames(); await Click(map.FindShipButton);
            Check(viewport.ShipViewportPosition.DistanceTo(viewport.Size / 2) < .1f && !viewport.ShipIsOffscreen,
                $"ship stays visible when centered at {size}");
            Check(map.FindShipButton.GetGlobalRect().Position.X >= viewport.GlobalPosition.X
                && map.FindShipButton.GetGlobalRect().End.X <= viewport.GetGlobalRect().End.X,
                $"Find DropShip control fits chart at {size}");
            await Capture($"dropship-{size.X}x{size.Y}");
        }
        viewport.Fit(); await RightClick(viewport.GlobalPosition + viewport.Size / 2);
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
        Check(_app.StarMap.FindShipButton.Disabled, "ship locator disables when map asset is missing");
        _app.StarMap.BackButton.EmitSignal(BaseButton.SignalName.Pressed);
        Check(_app.Office.Visible, "office remains reachable after a map load failure");
        _app.StarMap.SetMap(_app.StarMap.MapPath);
        Check(_app.StarMap.Viewport.MapLoaded, "map can be restored after a load failure");
        Check(_app.Office.WallMap.Texture == _app.StarMap.Viewport.Texture, "restored reference texture returns to the wall chart");
    }

    private async Task TestTravelUi()
    {
        var chart = StarChart.Instance;
        var origin = _app.State.Travel.OrbitSystemId;
        var route = chart.Systems.Select(system => chart.Plot(origin, system.Id))
            .First(path => path is { Length: 3 } && chart.JumpDays(path[0], path[1]) > 1)!;
        var destination = chart[route[^1]];
        _app.ShowMap(); await Frames();
        var map = _app.StarMap;
        var viewport = map.Viewport;
        viewport.ZoomBy(12);
        var point = new Vector2((float)destination.X, (float)destination.Y);
        viewport.Transform.Pan(viewport.Size / 2 - (viewport.Transform.Offset + point * viewport.Transform.Scale));
        viewport.ZoomBy(1); await Frames();
        var at = viewport.GlobalPosition + viewport.Transform.Offset + point * viewport.Transform.Scale;
        MouseMotion(at, Vector2.Zero); MouseButton(at, Godot.MouseButton.Left, true);
        MouseButton(at, Godot.MouseButton.Left, false); await Frames();
        Check(_app.State.Travel.Candidate?[^1] == destination.Id && map.EmbarkButton.Visible,
            $"actual system marker click plots candidate and exposes Embark: expected={destination.Name}/{destination.Id}, actual={_app.State.Travel.Candidate?[^1]}, hover={GetViewport().GuiGetHoveredControl()?.GetPath()}, point={at}, viewport={viewport.GetGlobalRect()}, hit={viewport.HitSystem(at - viewport.GlobalPosition)?.Id}, notice={map.CandidateLabel.Text}");
        Check(!_app.State.Travel.HasJourney && map.CandidateLabel.Text.Contains("CANDIDATE"), "candidate is separate from active journey");
        var bounds = destination.LabelBounds;
        var labelAt = viewport.GlobalPosition + viewport.Transform.Offset
            + new Vector2((float)(bounds[0] + bounds[2] / 2), (float)(bounds[1] + bounds[3] / 2)) * viewport.Transform.Scale;
        MouseMotion(labelAt, Vector2.Zero); MouseButton(labelAt, Godot.MouseButton.Left, true);
        MouseButton(labelAt, Godot.MouseButton.Left, false); await Frames();
        Check(_app.State.Travel.Candidate?[^1] == destination.Id, "system label click selects the correct destination");
        await Capture("map-candidate");
        await Click(map.EmbarkButton);
        Check(_app.State.Travel.InOrbit && _app.State.Travel.HasJourney && map.CancelButton.Visible,
            "Embark remains in orbit and exposes cancellation");
        map.SelectSystem(route[1]); await Frames();
        Check(_app.State.Travel.Journey![^1] == destination.Id && _app.State.Travel.Candidate![^1] == route[1],
            "new blue candidate leaves green journey unchanged");
        await Capture("map-journeys");
        await Click(map.CancelButton);
        Check(!_app.State.Travel.HasJourney && _app.State.Travel.Candidate![^1] == route[1], "Cancel Journey preserves candidate in UI");
        map.SelectSystem(destination.Id); await Frames(); await Click(map.EmbarkButton);
        _app.StartAdvancing(true);
        Check(!_app.IsAdvancing, "time cannot advance from map");
        var departedOn = _app.State.Date;
        _app.ShowOffice(); await Click(_app.Office.SnoozeButton); await Delay(.15);
        Check(!_app.State.Travel.InOrbit && _app.State.AccruedTransitDays == 1, "office calendar departs and accrues first transit day");
        _app.ShowMap(); await Frames();
        Check(!map.EmbarkButton.Visible && !map.CancelButton.Visible && !map.Search.Editable && map.Destinations.Disabled,
            "transit disables plotting, embarking and cancellation controls");
        map.SelectSystem(origin);
        Check(_app.State.Travel.Candidate is null, "map rejects plotting in transit");
        var from = chart[route[0]]; var to = chart[route[1]];
        var expectedShip = new Vector2((float)from.X, (float)from.Y).Lerp(new Vector2((float)to.X, (float)to.Y),
            1f / chart.JumpDays(route[0], route[1]));
        Check(viewport.ShipImagePosition.DistanceTo(expectedShip) < .01f, "DropShip locator tracks actual progress between systems");
        var zoomInTransit = viewport.Transform.Zoom;
        await Click(map.FindShipButton);
        Near(viewport.Transform.Zoom, zoomInTransit, "locating in-transit ship keeps zoom");
        Check(viewport.ShipViewportPosition.DistanceTo(viewport.Size / 2) < .1f && _app.State.Travel.Journey![^1] == destination.Id,
            "in-transit ship can be located without changing destination");
        await Capture("map-transit");
        _app.ShowOffice(); await JumpTo(0); await Delay(.65);
        Check(!_app.IsAdvancing && _app.State.Travel.InOrbit && _app.State.Travel.OrbitSystemId == route[1]
            && _app.State.Date == departedOn.AddDays(chart.JumpDays(origin, route[1])) && _app.Dialog.Visible,
            "calendar stops at intermediate arrival on expected day");
        await Click(DialogButton("Continue")); _app.ShowMap(); await Frames();
        Check(map.CancelButton.Visible && _app.State.Travel.HasJourney, "intermediate orbit permits cancelling onward journey");
        map.SelectSystem(origin); await Frames();
        await Capture("map-intermediate-orbit");
        GetWindow().Size = new Vector2I(900, 500); await Frames();
        map.CancelButton.GrabFocus(); await Frames();
        var scroll = map.FindChildren("*", "ScrollContainer", true, false).OfType<ScrollContainer>().Single(container => container.IsAncestorOf(map.CancelButton));
        Check(scroll.GetGlobalRect().Encloses(map.CancelButton.GetGlobalRect()), "small-window travel panel scrolls focused actions into view");
        await Capture("map-travel-900x500");
        GetWindow().Size = new Vector2I(1440, 900); await Frames();
        var transitDays = _app.State.AccruedTransitDays;
        _app.ShowOffice(); await Click(_app.Office.SnoozeButton); await Delay(.15);
        Check(_app.State.AccruedTransitDays == transitDays + 1 && _app.State.Travel.Candidate is null,
            "next day automatically continues green journey and clears blue candidate");
        if (_app.Dialog.Visible) await Click(DialogButton("Continue"));
        await Click(_app.Office.SafeButton);
        Check(_app.Dialog.FindChildren("*", "Label", true, false).OfType<Label>()
            .Any(label => label.Text.Contains("Transit") && label.Text.Contains("2,000")), "quarterly finances itemize accrued transit");
        await Capture("finances-transit");
        await Click(DialogButton("Close"));
    }

    /// <summary>Opens the office calendar and clicks one of its upcoming events.</summary>
    private async Task JumpTo(Index row)
    {
        await Click(_app.Office.CalendarButton);
        await Click(_app.Calendar.Rows[row]);
    }

    private static string RowText(Control row) => string.Join("|", row.FindChildren("*", "Label", true, false).OfType<Label>().Select(label => label.Text));

    /// <summary>The calendar lists what is ahead, soonest first, and Next Day steps through it with the list open.</summary>
    private async Task TestCalendarPanel()
    {
        var office = _app.Office;
        Check(office.SnoozeButton.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.Text == "Snooze...")
            && office.SnoozeButton.TooltipText == "Advance one day" && office.SnoozeButton.Name == "Snooze"
            && office.CalendarButton.TooltipText.EndsWith("Show upcoming events"), "the post-it reads Snooze... and advances a day; the calendar shows events");
        await Click(office.CalendarButton);
        var events = _app.Calendar.Events;
        Check(_app.Calendar.Visible && !_app.IsAdvancing && _app.Calendar.Rows.Count == events.Count && events.Count == 2
            && RowText(_app.Calendar.Rows[0]) == "3|04 JAN 3025|Arrived at destination"
            && RowText(_app.Calendar.Rows[1]).StartsWith("90|01 APR 3025|Quarterly payment · ") && RowText(_app.Calendar.Rows[1]).EndsWith(" C-bills (projected)"),
            "the calendar lists days to go, date and event, soonest first, ending with the next quarterly payment");
        Check(!_app.Calendar.Note.Visible && !_app.Calendar.NextDayButton.Disabled && _app.Calendar.Rows.All(row => !row.Disabled), "a solo leader can advance from the calendar");
        await Capture("calendar");
        await Click(_app.Calendar.NextDayButton);
        Check(_app.State.Date == new DateOnly(3025, 1, 2) && _app.Calendar.Visible && !_app.IsAdvancing
            && RowText(_app.Calendar.Rows[0]).StartsWith("2|04 JAN 3025|"), "Next Day advances one day and keeps the list open, counting down");
        await PressKey(Key.Escape);
        Check(!_app.Calendar.Visible && _app.Office.Visible && _app.State.Date == new DateOnly(3025, 1, 2), "Escape closes the calendar");
        await Click(office.CalendarButton);
        await RightClick(new Vector2(5, 85));
        Check(!_app.Calendar.Visible, "right-click closes the calendar");
    }

    /// <summary>Clicking a later event runs past earlier ones, whose notices wait until time reaches the chosen date.</summary>
    private async Task TestCalendarJump(Func<int> delivered)
    {
        var before = delivered();
        _app.State.Schedule(new("held-first", new DateOnly(3025, 1, 7), CampaignEventKind.Arrival, "Held first"));
        _app.State.Schedule(new("held-second", new DateOnly(3025, 1, 9), CampaignEventKind.RepairCompletion, "Held second"));
        await Click(_app.Office.CalendarButton);
        Check(_app.Calendar.Events.Select(item => item.Title).Take(2).SequenceEqual(["Held first", "Held second"]), "scheduled events join the list in date order");
        await Click(_app.Calendar.Rows[1]);
        await Delay(.3);
        Check(_app.IsAdvancing && !_app.Dialog.Visible && _app.State.Date >= new DateOnly(3025, 1, 7), "time runs on past an earlier event without stopping");
        await Delay(.4);
        Check(!_app.IsAdvancing && _app.State.Date == new DateOnly(3025, 1, 9) && _app.Dialog.Visible && _app.Dialog.Body == "Held first\nHeld second"
            && delivered() == before + 2, "time stops on the chosen date, then shows the passed events' notices in date order");
        await Click(DialogButton("Continue"));
    }

    private async Task TestCampaignUi()
    {
        _app.StartNewCompany();
        await FinishDraft();
        Check(_app.Office.CalendarDate.Text == "01 JAN 3025" && _app.Office.QuarterText.Text == "bills due in 90 days", "calendar shows date and bills countdown");
        Check(_app.Office.SafeReadout.Text == "₡ 2,500,000", "new company balance appears in the painted safe");
        _app.State.Schedule(new("arrival", new DateOnly(3025, 1, 4), CampaignEventKind.Arrival, "Arrived at destination"));
        var delivered = 0;
        _app.EventReached += _ => delivered++;
        await TestCalendarPanel();
        await JumpTo(0);
        await Delay(.4);
        Check(!_app.IsAdvancing && _app.State.Date == new DateOnly(3025, 1, 4) && delivered == 1, "an upcoming event ticks daily and stops on its date");
        Check(_app.Dialog.Visible && _app.Dialog.Heading == "Scheduled event", "event is surfaced to the player");
        await Click(DialogButton("Continue"));
        await Click(_app.Office.SnoozeButton);
        await Delay(.15);
        Check(_app.State.Date == new DateOnly(3025, 1, 5) && !_app.IsAdvancing, "Snooze click advances exactly one day");
        await TestCalendarJump(() => delivered);

        await JumpTo(^1);
        await Delay(.22);
        Check(_app.IsAdvancing && _app.State.Date > new DateOnly(3025, 1, 5), "fast-forward visibly advances through dates");
        var stopped = _app.State.Date;
        MouseButton(new Vector2(5, 85), Godot.MouseButton.Left, true);
        MouseButton(new Vector2(5, 85), Godot.MouseButton.Left, false);
        await Delay(.15);
        Check(!_app.IsAdvancing && _app.State.Date == stopped, "click on empty space halts time");
        foreach (var control in new[] { _app.Office.CalendarButton, _app.Office.SnoozeButton })
        {
            await JumpTo(^1);
            stopped = _app.State.Date;
            await Click(control);
            await Delay(.15);
            Check(!_app.IsAdvancing && _app.State.Date == stopped && !_app.Calendar.Visible, $"{control.Name} stopping click does not restart time");
        }
        await JumpTo(^1);
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
        Check(SaveFingerprint() == "", "UI advancement does not autosave");

        while (_app.State.Date < new DateOnly(3025, 3, 31)) _app.State.AdvanceOneDay();
        _app.Office.RefreshCampaign(_app.State);
        Check(_app.Office.QuarterText.Text == "bills due in 1 day", "calendar uses singular day for the final day");
        await Click(_app.Office.SnoozeButton);
        await Delay(.15);
        Check(_app.State.PaymentPending && _app.State.Date == new DateOnly(3025, 4, 1), "quarter boundary blocks animated advancement");
        Check(_app.Office.SnoozeButton.Disabled && !_app.Office.CalendarButton.Disabled && _app.Office.QuarterText.Text == "Payment Due", "pending quarter disables Snooze and shows payment due");
        await Click(_app.Office.CalendarButton);
        Check(_app.Calendar.Visible && _app.Calendar.Rows.All(row => row.Disabled) && _app.Calendar.NextDayButton.Disabled
            && _app.Calendar.Note.Visible && _app.Calendar.Note.Text == "Resolve the quarterly payment first.", "a pending payment leaves the calendar viewable but disables advancing");
        await Click(_app.Calendar.CloseButton);
        Check(!_app.Calendar.Visible, "Close dismisses the calendar");
        _app.Office.RefreshCampaign(_app.State);
        _app.Office._Process(.46);
        Check(_app.Office.SafeReadout.Modulate.A < .5f && _app.Office.SafeButton.Modulate == Colors.White,
            "only the digital balance blinks while payment is pending");
        _app.Office._Process(.45);
        Check(_app.Office.SafeReadout.Modulate == Colors.White, "digital balance returns to its bright phase");
        Check(_app.Office.QuarterText.GetThemeColor("font_color") == new Color("ff6c63"), "payment due is red");
        await Capture("payment-due");
        await TestSaveGridUi();
        await Click(_app.Office.SafeButton);
        await Click(DialogButton("Pay Later"));
        Check(_app.Dialog.Heading == "Defer quarterly payment?" && _app.State.PaymentPending, "warning precedes deferral");
        await RightClick(DialogButton("Cancel").GetGlobalRect().GetCenter());
        Check(_app.Dialog.Heading == "Quarterly finances", "right-click deferral warning returns to finances");
        Check(_app.State.PaymentPending && _app.State.Cash == 2_500_000, "cancel warning preserves payment block");
        await Click(DialogButton("Pay Later"));
        await Click(DialogButton("Pay Later"));
        Check(!_app.State.PaymentPending && _app.State.DeferredDebt == Campaign.QuarterlyCharge + _app.State.Roster!.Salaries && _app.State.Cash == 2_500_000, "confirmed deferral preserves cash");
        Check(_app.Office.QuarterText.Text == "bills due in 91 days" && _app.Office.QuarterText.GetThemeColor("font_color") == new Color("ff6c63"), "deferred countdown stays red");
        Check(_app.Office.SafeReadout.Modulate == Colors.White && _app.Office.SafeReadout.Text == "₡ 2,500,000", "safe stops blinking and preserves balance after deferral");
        await Capture("payment-deferred");

        await Click(_app.Office.FilingButton); await Click(_app.Saves.LoadTab);
        Check(_app.Saves.Heading.Text == "LOAD GAME" && _app.Saves.Entries.Count == 1 && SlotButton(1).Disabled, "the Load tab lists only the saves");
        await Click(SlotButton(0));
        Check(_app.SaveDialog.Visible && _app.SaveDialog.Heading == "Load Payment due?", "load asks before discarding progress");
        await Click(DialogButton("Load Game"));
        Check(_app.State.PaymentPending && _app.State.DeferredDebt == 0 && _app.Office.QuarterText.Text == "Payment Due", "load restores unpaid quarter and calendar block");
        await Click(_app.Office.SafeButton);
        var due = _app.State.PaymentDue;
        Check(due == Campaign.QuarterlyCharge + _app.State.BilledSalaries && _app.State.BilledSalaries == _app.State.Roster!.Salaries
            && _app.Dialog.Body.Contains($"Salaries · 4 pilots: {RosterUi.Money(_app.State.BilledSalaries)}"), "the bill includes the company's salaries");
        await Click(DialogButton("Pay Now"));
        Check(_app.State.Cash == 2_500_000 - due && _app.State.CanAdvance && !_app.Dialog.Visible, "Pay Now collects full bill and unlocks time");
        Check(_app.Office.SafeReadout.Text == $"₡ {(2_500_000 - due).ToString("N0", System.Globalization.CultureInfo.InvariantCulture)}"
            && _app.Office.SafeButton.TooltipText.Contains(_app.Office.SafeReadout.Text), "payment refreshes safe balance and tooltip");
        Check(_app.Office.QuarterText.GetThemeColor("font_color") == Ui.Gold, "full payment restores normal countdown color");
        var slot = _app.SoloSaves.List().Single().Id;
        await Click(_app.Office.FilingButton); await Click(SlotButton(1));
        Check(_app.SaveDialog.Heading == "Overwrite save?" && _app.SaveDialog.NameInput!.Text == "Payment due", "overwriting keeps the slot's own name");
        await Click(DialogButton("Save"));
        Check(_app.SoloSaves.List() is [{ Info.Name: "Payment due" } only] && only.Id == slot && _app.SoloSaves.Load(slot).Cash == 2_500_000 - due,
            "confirmed overwrite replaces the slot with the changed finances");
        await Click(_app.Saves.CloseButton);

        var deadline = Campaign.Restore(CampaignChecks.NewCompany().Snapshot() with
        {
            Date = new DateOnly(3025, 6, 30), Cash = 199_999,
            DeferredDebt = 100_000, NextQuarter = new DateOnly(3025, 7, 1)
        });
        SeedSave(deadline, "Deadline");
        var beforeBankruptcy = SaveFingerprint();
        await LoadFromFiling();
        Check(_app.Office.SafeReadout.Text == "₡ 199,999", "loading a different company balance refreshes the safe");
        await Click(_app.Office.SnoozeButton);
        await Delay(.15);
        Check(_app.State.Bankrupt && _app.Dialog.Heading == "Company bankrupt", "grace deadline opens bankruptcy dialog");
        await Capture("bankruptcy");
        await Click(DialogButton("Main Menu"));
        Check(_app.MainMenu.Visible && !_app.Office.Visible && !_app.StarMap.Visible, "bankruptcy dismissal returns to menu");
        Check(SaveFingerprint() == beforeBankruptcy, "bankruptcy preserves the saves");
        await Click(_app.MainMenu.LoadButton);
        Check(_app.Saves.Visible && _app.MainMenu.Visible && !_app.Saves.SaveTab.IsVisibleInTree() && _app.Saves.Heading.Text == "LOAD GAME"
            && SlotName(SlotButton(0)) == "Deadline" && SlotName(SlotButton(1)) == "Payment due", "main menu Load Game lists solo saves, newest first");
        await Click(SlotButton(0));
        if (_app.SaveDialog.Visible) await Click(DialogButton("Load Game"));
        Check(_app.Office.Visible && _app.State.Date == deadline.Date && _app.State.DeferredDebt == 100_000, "main menu loads complete saved campaign");
        _app.ShowMainMenu();
        await Frames();
        await Click(_app.MainMenu.NewButton);
        await FinishDraft();
        Check(_app.State.Date == Campaign.StartDate && _app.State.Cash == Campaign.StartingCash, "New Company resets state");
        Check(_app.Office.SafeReadout.Text == "₡ 2,500,000" && _app.Office.SafeReadout.Modulate == Colors.White, "New Company resets the painted balance and stops blinking");
        Check(SaveFingerprint() == beforeBankruptcy, "New Company preserves the saves");
        System.IO.File.WriteAllText(_app.SoloSaves.PathOf(_app.SoloSaves.List()[0].Id), "invalid save");
        var live = _app.State;
        await Click(_app.Office.FilingButton); await Click(_app.Saves.LoadTab);
        Check(SlotName(SlotButton(1)) == "Unreadable save", "an unreadable save is listed last");
        await Click(SlotButton(1)); await Click(DialogButton("Load Game"));
        Check(_app.SaveDialog.Heading == "Unable to load" && ReferenceEquals(_app.State, live), "corrupt load keeps live company intact");
        await Click(DialogButton("Close"));
        Check(_app.Saves.Visible && !_app.SaveDialog.Visible, "closing the load error returns to the save grid");
        await Click(_app.Saves.CloseButton);
    }

    /// <summary>Picks a starting scenario in the dialog New Company opens.</summary>
    private async Task ChooseScenario(string name = "Established")
    {
        await Frames();
        if (_app.Dialog.Visible && _app.Dialog.Heading == "Starting scenario") await Click(DialogButton(name));
    }

    private async Task FinishDraft()
    {
        await ChooseScenario();
        _app.Drafting.NameInput.Text = "Test Company";
        _app.Drafting.NameInput.EmitSignal(LineEdit.SignalName.TextChanged, "Test Company");
        for (var pool = 0; pool < 8; pool++) await Click(_app.Drafting.Choices[pool, 0]);
        await Click(_app.Drafting.EstablishButton);
    }

    /// <summary>New Company asks how the company begins; Just starting out drafts two light mechs and two junior MechWarriors.</summary>
    private async Task TestScenarioUi()
    {
        await Frames();
        Check(_app.Dialog.Visible && _app.Dialog.Heading == "Starting scenario" && !_app.Drafting.Visible
            && DialogButton("Just starting out").Visible && DialogButton("Established").Visible, "New Company asks for a starting scenario");
        await PressKey(Key.Escape);
        Check(!_app.Dialog.Visible && _app.MainMenu.Visible && !_app.Drafting.Visible, "Escape from the scenario dialog stays at the main menu");
        await Click(_app.MainMenu.NewButton); await ChooseScenario("Just starting out");
        var draft = _app.Drafting.Draft;
        Check(_app.Drafting.Visible && draft.Scenario == StartingScenario.StartingOut && draft.Columns == 2 && draft.Complete && draft.SelectedCount == 4,
            "Just starting out opens a four-pool draft");
        var shown = Enumerable.Range(0, 8).Where(pool => _app.Drafting.Choices[pool, 0].IsVisibleInTree()).ToArray();
        Check(shown.SequenceEqual([0, 1, 4, 5]), $"only the two mech and two MechWarrior pools are shown ({string.Join(",", shown)})");
        var mechs = Enumerable.Range(0, 2).SelectMany(p => Enumerable.Range(0, 3).Select(c => draft.MechCandidate(p, c))).ToArray();
        Check(mechs.All(m => m.WeightClass == MechWeightClass.Light) && mechs.Select(m => m.Id).Distinct().Count() == 6
            && Enumerable.Range(0, 3).All(c => draft.WarriorCandidate(4, c).Rank <= WarriorExperience.Corporal && draft.WarriorCandidate(5, c).Rank <= WarriorExperience.PrivateFirstClass
                && draft.WarriorCandidate(4, c).SkillPoints <= WarriorSkills.Bracket(WarriorExperience.Corporal).Max
                && draft.WarriorCandidate(5, c).SkillPoints <= WarriorSkills.Bracket(WarriorExperience.PrivateFirstClass).Max),
            "both mech pools offer different light mechs, over a Corporal and a Private First Class pool");
        Check(_app.Drafting.Choices[1, 0].GetGlobalRect().Position.X > _app.Drafting.Choices[0, 0].GetGlobalRect().End.X
            && _app.Drafting.Choices[1, 0].GetGlobalRect().End.X < GetViewport().GetVisibleRect().End.X, "the two columns share the screen width");
        await Capture("draft-starting-out");
        _app.Drafting.NameInput.Text = "Fresh Start"; _app.Drafting.NameInput.EmitSignal(LineEdit.SignalName.TextChanged, "Fresh Start");
        await Click(_app.Drafting.EstablishButton);
        var roster = _app.State.Roster;
        Check(_app.Office.Visible && roster is { Mechs.Count: 2, Warriors.Count: 2 } && roster.Mechs.All(m => m.Lance == 0 && m.WarriorId is not null)
            && _app.State.Cash == 500_000, "the company starts with two piloted light mechs in its lance and 500,000 C-bills");
        _app.ShowMainMenu(); await Frames(); await Click(_app.MainMenu.NewButton);
        if (_app.UnsavedDialog.Visible) await Click(DialogButton("New Company"));
        await ChooseScenario();
    }

    private async Task TestDraftUi()
    {
        Check(_app.Drafting.Visible && _app.State.RequiresDraft && !_app.State.CanAdvance, "new company must draft before operations");
        var initialDraft = _app.Drafting.Draft;
        foreach (var portrait in PilotData.Instance.Portraits)
            Check(Ui.LoadTexture($"res://Assets/Roster/{portrait.Id}.png") is not null, $"portrait {portrait.Id} is imported");
        var missingPortrait = TestPilots.Warriors[0] with { Portrait = "test-missing-portrait" };
        Check(RosterUi.Portrait(missingPortrait) == RosterUi.PlaceholderPortrait, "a missing pilot texture shows the placeholder portrait");
        var initialChoices = Enumerable.Range(0, 8).Select(initialDraft.Choice).ToArray();
        for (var pool = 0; pool < 4; pool++) for (var choice = 0; choice < 3; choice++)
        {
            var preview = _app.Drafting.Choices[pool, choice].GetChildren().OfType<MechPreview>().Single();
            Check(preview.Model?.ChassisId == initialDraft.MechCandidate(pool, choice).Id, "draft card displays its randomized candidate model");
        }
        Check(initialDraft.Complete && initialDraft.SelectedCount == 8 && !_app.Drafting.EstablishButton.Disabled,
            "new draft defaults are ready to establish");
        Check(InitialDraft.ValidName(_app.Drafting.NameInput.Text) && _app.Drafting.NameInput.Text == initialDraft.ForceName,
            "generated company name matches the draft model");
        for (var pool = 0; pool < 8; pool++)
            Check(Enumerable.Range(0, 3).Count(choice => _app.Drafting.Choices[pool, choice].ButtonPressed) == 1
                && _app.Drafting.Choices[pool, initialChoices[pool]].ButtonPressed, "each pool displays its randomized selection");
        _app.Drafting.NameInput.Text = "";
        _app.Drafting.NameInput.EmitSignal(LineEdit.SignalName.TextChanged, "");
        Check(_app.Drafting.EstablishButton.Disabled && !initialDraft.Complete, "clearing the generated name prevents establishment");
        _app.Drafting.NameInput.Text = "Edited Company";
        _app.Drafting.NameInput.EmitSignal(LineEdit.SignalName.TextChanged, "Edited Company");
        Check(!_app.Drafting.EstablishButton.Disabled, "editing the generated name restores establishment");
        _app.StartAdvancing(false);
        Check(!_app.IsAdvancing, "draft gates time advancement");
        foreach (var size in new[] { new Vector2I(1440, 900), new Vector2I(900, 500) })
        {
            GetWindow().Size = size; await Frames();
            for (var pool = 0; pool < 8; pool++) for (var choice = 0; choice < 3; choice++)
            {
                var button = _app.Drafting.Choices[pool, choice];
                Check(button.IsVisibleInTree() && _app.Drafting.GetGlobalRect().Encloses(button.GetGlobalRect()) && button.Size.Y >= 36,
                    $"all 24 draft candidates visible and clickable at {size}");
                var panel = (Control)button.GetParent();
                Check(panel.GetChildren().OfType<Button>().Where(other => other.Text == "…").All(inspect => panel.GetGlobalRect().Encloses(inspect.GetGlobalRect())),
                    $"draft inspect buttons stay inside their pool at {size}");
                var stats = button.GetChildren().OfType<Label>().Single(label => label.Text.Split('\n').Length == (pool < 4 ? 3 : 4));
                Check(pool < 4 ? stats.Text.Contains(" tons\n") : stats.Text.StartsWith("Gunnery: ") && stats.Text.Contains("\nPiloting: ")
                    && stats.Text.Contains("\nSensors: ") && stats.Text.Contains("\nGrit: "),
                    "draft cards list one stat per line");
                var font = stats.GetThemeFont("font"); var fontSize = stats.GetThemeFontSize("font_size");
                Check(stats.Text.Split('\n').All(line => font.GetStringSize(line, fontSize: fontSize).X <= stats.Size.X)
                    && stats.GetLineCount() == (pool < 4 ? 3 : 4) && stats.GetVisibleLineCount() == stats.GetLineCount(), $"every draft card stat line fits at {size}");
            }
            await Capture($"draft-{size.X}x{size.Y}");
        }
        GetWindow().Size = new Vector2I(1440, 900); await Frames();
        Check(ReferenceEquals(initialDraft, _app.Drafting.Draft) && Enumerable.Range(0, 8).All(pool => initialDraft.Choice(pool) == initialChoices[pool])
            && initialDraft.ForceName == "Edited Company", "resizing preserves random selections and the edited company name");
        await Click(_app.Drafting.Choices[0, 1]);
        await PressKey(Key.Escape);
        Check(_app.Dialog.Heading == "Abandon initial draft?", "draft Escape asks before abandonment");
        await Click(DialogButton("Cancel"));
        Check(_app.Drafting.Draft.Choice(0) == 1 && _app.Drafting.Visible && _app.Drafting.NameInput.Text == "Edited Company"
            && _app.Drafting.Draft.ForceName == "Edited Company", "cancel abandonment preserves selections and the edited name");
        Check(SaveFingerprint() == "", "partial draft never writes a save");
        var inspect = _app.Drafting.FindChildren("*", "Button", true, false).OfType<Button>().First(button => button.Text == "…");
        await Click(inspect);
        Check(_app.Drafting.FindChildren("*", "Control", true, false).OfType<MechDetails>().Any(), "candidate can be inspected before selection");
        Check(_app.Drafting.FindChildren("*", "Control", true, false).OfType<MechPreview>().Any(preview => preview.Model?.ChassisId == initialDraft.MechCandidate(0, 0).Id), "inspection displays the selected candidate model");
        await Capture("draft-inspect");
        await RightClick(inspect.GetGlobalRect().GetCenter());
        Check(!_app.Dialog.Visible && _app.Drafting.Draft.Choice(0) == 1 && inspect.HasFocus()
            && ReferenceEquals(initialDraft, _app.Drafting.Draft) && initialDraft.ForceName == "Edited Company"
            && _app.Drafting.NameInput.Text == "Edited Company"
            && Enumerable.Range(1, 7).All(pool => initialDraft.Choice(pool) == initialChoices[pool]),
            "candidate inspection preserves draft choices and name while restoring focus");
        await RightClick(_app.Drafting.NameInput.GetGlobalRect().GetCenter());
        Check(_app.Dialog.Heading == "Abandon initial draft?", "draft right-click preserves abandonment confirmation");
        await RightClick(DialogButton("Cancel").GetGlobalRect().GetCenter());
        Check(!_app.Dialog.Visible && _app.Drafting.Visible && _app.Drafting.Draft.Choice(0) == 1, "right-click cancels draft abandonment without losing selections");
        await FinishDraft();
        Check(!_app.Drafting.Visible && !_app.State.RequiresDraft && _app.State.Roster!.Mechs.Count == 4 && _app.State.Roster.Warriors.Count == 4,
            "Establish Company commits four mechs and four warriors");
        Check(_app.State.Roster!.Mechs.Zip(_app.State.Roster.Warriors).All(pair => pair.First.WarriorId == pair.Second.Id)
            && _app.Office.ReadyCount.Text == "4", "new roster assigns four healthy warriors by draft column");
    }

    private async Task TestRosterUi()
    {
        // One banked success each for the first two cards, short of any threshold, so their bars show progress.
        foreach (var learner in _app.State.Roster!.Warriors.Take(2))
            learner.GainSkill(SkillGrowth.Skills.Last(skill => !SkillGrowth.Maxed(SkillGrowth.Rating(learner.Spec, skill))));
        await Click(_app.Office.RolodexButton);
        Check(_app.Rolodex.Visible && _app.Office.Visible && !_app.Hangar.Visible, "rolodex overlays the office");
        _app.StartAdvancing(false); Check(!_app.IsAdvancing, "rolodex pauses company time");
        var heading = _app.Rolodex.FindChildren("*", nameof(Label), true, false).OfType<Label>().Select(label => label.Text).ToArray();
        Check(heading.Contains("PERSONNEL") && !heading.Any(text => text.Contains("ROLODEX") || text.Contains(_app.State.Roster!.ForceName)),
            "personnel heading drops the rolodex label and company line");
        var record = heading.First(text => text.Contains("Deployments:"));
        var pilot = _app.State.Roster!.Warriors[0];
        Check(record.Contains($"Piloting: {_app.State.Roster.Assignment(pilot.Id)!.Spec.DisplayName}\nDeployments: 0\nKills: 0") && !record.Contains("Skill points"),
            "personnel card shows the piloted mech, deployments and kills without skill points");
        var bars = _app.Rolodex.Skills;
        Check(bars.Visible && bars.GetChildCount() == 4 && SkillGrowth.Skills.All(skill => SkillGrowth.Maxed(SkillGrowth.Rating(pilot.Spec, skill))
                ? !bars.Bars.ContainsKey(skill)
                : bars.Bars[skill].MaxValue == SkillGrowth.Threshold(SkillGrowth.Rating(pilot.Spec, skill)) && bars.Bars[skill].Value == pilot.Progress[skill]
                    && bars.Bars[skill].GetParent<Control>().TooltipText == $"{pilot.Progress[skill]} of {bars.Bars[skill].MaxValue} toward {WarriorSkills.Descriptor(SkillGrowth.Rating(pilot.Spec, skill) - 1)}"),
            "an active MechWarrior's card shows a progress bar under each skill short of Legendary, with the count on hover");
        Check(record.StartsWith(pilot.Spec.Experience + "\n"), "the personnel card shows the MechWarrior's rank");
        var first = _app.Rolodex.CardName.Text; await Click(_app.Rolodex.NextButton);
        Check(first != _app.Rolodex.CardName.Text, "rolodex flips between personnel cards");
        await Capture("rolodex");
        for (var i = 0; i < 8; i++) { await PressKey(Key.Tab); Check(_app.Rolodex.IsAncestorOf(GetViewport().GuiGetFocusOwner()), "rolodex retains keyboard focus"); }
        await PressKey(Key.Escape); Check(!_app.Rolodex.Visible && _app.Office.Visible, "rolodex Escape returns to office");
        var roster = _app.State.Roster!;
        foreach (var size in new[] { new Vector2I(1440, 900), new Vector2I(900, 500) })
        {
            GetWindow().Size = size; await Frames();
            await Click(_app.Office.RolodexButton); await Capture($"rolodex-{size.X}x{size.Y}"); await PressKey(Key.Escape);
        }
        GetWindow().Size = new Vector2I(1440, 900); await Frames();
        await PressKey(Key.Escape);
        Check(_app.MainMenu.Visible && _app.MainMenu.ResumeButton.Visible && _app.MainMenu.ResumeButton.Text == "Resume", "office Escape opens menu with Resume");
        await Click(_app.MainMenu.ResumeButton);
        Check(_app.Office.Visible && ReferenceEquals(roster, _app.State.Roster), "Resume retains unsaved roster and assignments");
        roster.SetHealth(roster.Warriors[0].Id, WarriorHealth.Injured, _app.State.Date.AddDays(2), _app.State.Date);
        _app.ShowOffice(); Check(_app.Office.ReadyCount.Text == "3", "rolodex counter excludes injured warriors");
        await Click(_app.Office.RolodexButton); await Capture("rolodex-injured"); await PressKey(Key.Escape);
        roster.SetHealth(roster.Warriors[0].Id, WarriorHealth.Healthy, null, _app.State.Date); _app.ShowOffice();
        roster.SetHealth(roster.Warriors[1].Id, WarriorHealth.Dead, null, _app.State.Date);
        _app.ShowWarriors(); _app.Rolodex.Section.Select(1); _app.Rolodex.Section.EmitSignal(OptionButton.SignalName.ItemSelected, 1L); await Frames();
        Check(_app.Rolodex.CardName.Text.Contains(roster.Warriors[1].Spec.Name) && roster.Assignment(roster.Warriors[1].Id) is null,
            "memorial section retains a deceased warrior and clears assignment");
        Check(_app.Office.ReadyCount.Text == "3", "office count excludes memorial records");
        await Capture("rolodex-memorial"); await PressKey(Key.Escape);
        await TestHiringUi();
    }

    /// <summary>The For hire section: recruiting pays at once and joins the company; Dismiss asks, then removes a MechWarrior for good.</summary>
    private async Task TestHiringUi()
    {
        var state = _app.State; var roster = state.Roster!;
        Check(PilotTuning.Slots(StarChart.Instance[state.Travel.OrbitSystemId]) >= 2, "the starting system lists MechWarriors for hire");
        // A newly generated Private First Class and Corporal.
        var inUse = new PilotsInUse(roster.Warriors.Select(item => item.Spec)); var rolls = new Random(3025);
        state.PlaceRecruits([.. new[] { WarriorExperience.PrivateFirstClass, WarriorExperience.Corporal }.Select(rank => PilotGenerator.Generate(rolls, inUse, rank))]);
        await Click(_app.Office.RolodexButton);
        _app.Rolodex.Section.Select(RolodexPanel.HireSection); _app.Rolodex.Section.EmitSignal(OptionButton.SignalName.ItemSelected, (long)RolodexPanel.HireSection); await Frames();
        var recruit = state.OrbitRecruits.Single(item => item.Id == _app.Rolodex.SelectedId); var cost = state.HireCost(recruit);
        var record = _app.Rolodex.FindChildren("*", nameof(Label), true, false).OfType<Label>().Select(label => label.Text).First(text => text.StartsWith("FOR HIRE"));
        Check(record.Contains($"Salary: {RosterUi.Money(recruit.Salary)} a quarter") && _app.Rolodex.ActionButton.Visible && !_app.Rolodex.ActionButton.Disabled
            && _app.Rolodex.ActionButton.Text == $"Recruit · {RosterUi.Money(cost.Total)}" && _app.Rolodex.ActionButton.TooltipText.Contains("signing bonus")
            && _app.Rolodex.ActionButton.TooltipText.Contains($"{cost.DaysLeft} days left this quarter"), "a card for hire shows the salary, the hiring cost and its breakdown");
        Check(_app.Rolodex.CardName.Text.Contains(recruit.Name) && _app.Rolodex.NextButton.Disabled == false, "both MechWarriors for hire are listed");
        await Capture("rolodex-for-hire");
        var cash = state.Cash;
        await Click(_app.Rolodex.ActionButton);
        Check(state.Cash == cash - cost.Total && roster.Warriors.Any(item => item.Id == recruit.Id) && state.OrbitRecruits.All(item => item.Id != recruit.Id)
            && !_app.Rolodex.CardName.Text.Contains(recruit.Name), "Recruit pays at once, hires the MechWarrior and takes them off the list");
        var other = state.OrbitRecruits.Single(item => item.Id == _app.Rolodex.SelectedId);
        var poor = Campaign.Restore(state.Snapshot() with { Cash = 0 });
        Check(poor.HireProblem(other) is not null, "a hire the company cannot afford is refused");
        _app.Rolodex.Section.Select(RolodexPanel.ActiveSection); _app.Rolodex.Section.EmitSignal(OptionButton.SignalName.ItemSelected, (long)RolodexPanel.ActiveSection); await Frames();
        while (_app.Rolodex.SelectedId != recruit.Id) await Click(_app.Rolodex.NextButton);
        Check(_app.Rolodex.ActionButton.Text == "Dismiss" && !_app.Rolodex.ActionButton.Disabled, "an active card offers Dismiss");
        await PressKey(Key.Escape);
        Check(_app.Office.ReadyCount.Text == "4", "a recruit counts as ready for deployment");
        await Click(_app.Office.HangarButton);
        Check(_app.Hangar.PilotPicker.ItemCount > 0 && Enumerable.Range(0, _app.Hangar.PilotPicker.ItemCount).Any(i => _app.Hangar.PilotPicker.GetItemText(i).StartsWith(recruit.Callsign)),
            "a recruit can be chosen as a pilot in the hangar straight away");
        await Click(_app.Hangar.WarriorsButton);
        while (_app.Rolodex.SelectedId != recruit.Id) await Click(_app.Rolodex.NextButton);
        await Click(_app.Rolodex.ActionButton);
        Check(_app.Dialog.Visible && _app.Dialog.Heading == "Dismiss MechWarrior?", "Dismiss asks first");
        await Click(DialogButton("Cancel"));
        Check(roster.Warriors.Any(item => item.Id == recruit.Id) && _app.Rolodex.Visible, "cancelling keeps the MechWarrior");
        cash = state.Cash;
        await Click(_app.Rolodex.ActionButton); await Click(DialogButton("Dismiss"));
        Check(roster.Warriors.All(item => item.Id != recruit.Id) && state.Cash == cash && _app.Rolodex.Visible
            && _app.Rolodex.SelectedId != recruit.Id, "a dismissed MechWarrior leaves the company without a refund");
        _app.Rolodex.Section.Select(RolodexPanel.MemorialSection); _app.Rolodex.Section.EmitSignal(OptionButton.SignalName.ItemSelected, (long)RolodexPanel.MemorialSection); await Frames();
        Check(!_app.Rolodex.ActionButton.Visible, "memorial cards have no Recruit or Dismiss button");
        await PressKey(Key.Escape);
        Check(_app.Hangar.Visible, "personnel opened from the hangar returns there");
        _app.ShowOffice(); await Frames(); _app.ShowFinances(); await Frames();
        Check(_app.Dialog.Body.Contains($"Salaries · {roster.SalariedWarriors} pilots: {RosterUi.Money(roster.Salaries)}"), "quarterly finances list the salaries");
        await Click(DialogButton("Close"));
    }

    /// <summary>The bottom-left drop list shows one lance's mechs, or the reserves, on the roster strip.</summary>
    private async Task TestHangarLanceFilter()
    {
        var hangar = _app.Hangar; var roster = _app.State.Roster!; var picker = hangar.LancePicker;
        bool Shows(int? lance) => roster.Mechs.Select((mech, i) => hangar.RosterButtons[i].Visible == (mech.Lance == lance)).All(shown => shown);
        Check(hangar.ShownLance == 0 && Shows(0) && picker.ItemCount == roster.LanceCount + 1 && picker.Selected == 0
            && picker.GetItemText(0) == $"Lance 1 · {roster.LanceMechs(0).Count()}/4" && picker.GetItemText(roster.LanceCount).StartsWith("Reserves"),
            "the hangar shows the player's lance, with one drop list item per lance and the reserves");
        var pool = roster.LanceCount;
        if (roster.Mechs.Any(mech => mech.Lance is null))
        {
            picker.Select(pool); picker.EmitSignal(OptionButton.SignalName.ItemSelected, (long)pool); await Frames();
            Check(hangar.ShownLance is null && Shows(null) && hangar.SelectedMech.Lance is null, "choosing the reserves shows only its mechs and selects one");
        }
        else Check(picker.IsItemDisabled(pool) && !hangar.ShowLance(null) && hangar.ShownLance == 0, "empty reserves cannot be chosen");
        await Click(hangar.BackButton); await Click(_app.Office.HangarButton);
        Check(hangar.ShownLance == 0 && Shows(0) && hangar.SelectedMech.Lance == 0 && picker.Selected == 0, "reopening the hangar returns to the player's lance");
    }

    private async Task TestHangarAssignmentsUi()
    {
        // Start with every warrior unassigned so the hangar must make each assignment.
        foreach (var mech in _app.State.Roster!.Mechs) _app.State.Roster.Assign(mech.Id, null);
        _app.ShowOffice(); await Frames();
        await Click(_app.Office.HangarButton);
        Check(_app.Hangar.Visible && !_app.Office.Visible && _app.Hangar.ArtworkLoaded, "porthole opens separate illustrated hangar");
        await Click(_app.Hangar.AssignButton);
        Check(_app.State.Roster!.Mechs[0].WarriorId is not null, "hangar assigns an unassigned warrior");
        var seated = _app.State.Roster.Pilot(_app.State.Roster.Mechs[0])!;
        Check(_app.Hangar.PilotSkills.Visible && _app.Hangar.PilotSkills.GetChildCount() == 4
            && _app.Hangar.PilotSkills.Bars.Count == SkillGrowth.Skills.Count(skill => !SkillGrowth.Maxed(SkillGrowth.Rating(seated.Spec, skill))),
            "the hangar shows the assigned pilot's skills with their progress bars");
        await Click(_app.Hangar.RosterButtons[1]); await Click(_app.Hangar.AssignButton);
        var roster = _app.State.Roster!;
        Check(roster.Mechs[1].WarriorId is not null && roster.Mechs[0].WarriorId != roster.Mechs[1].WarriorId, "second mech gets a different available warrior");
        _app.Hangar.PilotPicker.Select(0); _app.Hangar.PilotPicker.EmitSignal(OptionButton.SignalName.ItemSelected, 0L);
        await Click(_app.Hangar.AssignButton);
        Check(_app.Dialog.Heading == "Swap MechWarriors?", "occupied assignment requires an explicit swap");
        var before = roster.Mechs[1].WarriorId;
        await Click(DialogButton("Cancel")); Check(roster.Mechs[1].WarriorId == before, "cancel swap leaves both assignments intact");
        await Click(_app.Hangar.AssignButton); await Click(DialogButton("Swap"));
        Check(roster.Mechs[1].WarriorId == roster.Warriors[0].Id && roster.Mechs[0].WarriorId == before, "confirmed swap updates both assignments");
        Check(_app.Hangar.LanceText.Text == "Lance 1 · 4/4" && _app.Hangar.LanceButton.Text == "Move to reserves", "hangar shows the selected mech's lance");
        await Click(_app.Hangar.LanceButton);
        Check(roster.Mechs[1].Lance is null && roster.Mechs[1].WarriorId == roster.Warriors[0].Id && _app.Hangar.LanceText.Text == "Reserves"
            && _app.Hangar.ShownLance is null && _app.Hangar.SelectedMech == roster.Mechs[1] && _app.Hangar.RosterButtons[1].Visible
            && !_app.Hangar.RosterButtons[0].Visible, "hangar moves a mech to the reserves with its MechWarrior, and the roster strip follows it");
        await Click(_app.Hangar.LanceButton);
        Check(roster.Mechs[1].Lance == 0 && _app.Hangar.LanceButton.Text == "Move to reserves" && _app.Hangar.ShownLance == 0
            && _app.Hangar.SelectedMech == roster.Mechs[1], "hangar adds a reserve mech back to the player's lance, and the strip follows it");
        await TestHangarLanceFilter();
        foreach (var size in new[] { new Vector2I(1440, 900), new Vector2I(900, 500) })
        {
            GetWindow().Size = size; await Frames(); await Capture($"hangar-{size.X}x{size.Y}");
            Check(_app.Hangar.GetGlobalRect().Encloses(_app.Hangar.BackButton.GetGlobalRect()) && _app.Hangar.GetGlobalRect().Encloses(_app.Hangar.RosterButtons[0].GetGlobalRect())
                && _app.Hangar.GetGlobalRect().Encloses(_app.Hangar.LancePicker.GetGlobalRect())
                && !_app.Hangar.LancePicker.GetGlobalRect().Intersects(_app.Hangar.RosterButtons[0].GetGlobalRect())
                && !_app.Hangar.LancePicker.GetGlobalRect().Intersects(_app.Hangar.StoragePanel.GetGlobalRect()), "hangar navigation survives resizing");
            var mechButton = _app.Hangar.RosterButtons[0].GetGlobalRect(); var backButton = _app.Hangar.BackButton.GetGlobalRect();
            Check(Mathf.IsEqualApprox(mechButton.Position.Y, backButton.Position.Y) && Mathf.IsEqualApprox(mechButton.Size.Y, backButton.Size.Y),
                $"mech buttons sit level with, and as tall as, the other bottom buttons at {size}: {mechButton} vs {backButton}");
        }
        GetWindow().Size = new Vector2I(1440, 900); await Frames();
    }

    private async Task TestLegacyDraftUi()
    {
        var legacy = CampaignChecks.NewCompany().Snapshot() with
        {
            Version = 1, Roster = null, Missions = null, Cash = 1_234_567, Date = new DateOnly(3025, 2, 10),
            Events = [new("legacy-arrival", new DateOnly(3025, 2, 12), CampaignEventKind.Arrival, "Legacy arrival")]
        };
        var legacyFile = _app.SoloSaves.PathOf("legacy");
        System.IO.Directory.CreateDirectory(_app.SoloSaves.Directory);
        System.IO.File.WriteAllText(legacyFile, System.Text.Json.JsonSerializer.Serialize(legacy));
        var original = System.IO.File.ReadAllText(legacyFile);
        _app.ShowMainMenu(); await Frames(); await Click(_app.MainMenu.LoadButton);
        Check(SlotName(SlotButton(0)) == "Company" && SlotButton(0).GetNode<Label>("Lines/SlotDate").Text == "10 FEB 3025",
            "a save from before slots is listed with its date");
        await Click(SlotButton(0));
        if (_app.SaveDialog.Visible) await Click(DialogButton("Load Game"));
        Check(_app.Drafting.Visible && _app.State.RequiresDraft && _app.State.Cash == legacy.Cash, "loading a legacy save routes through mandatory draft");
        var abandonedDraft = _app.Drafting.Draft;
        Check(abandonedDraft.Complete && !_app.Drafting.EstablishButton.Disabled && _app.Drafting.NameInput.Text == abandonedDraft.ForceName,
            "legacy draft also starts with valid random defaults");
        Check(System.IO.File.ReadAllText(legacyFile) == original, "loading a legacy save does not rewrite it");
        await PressKey(Key.Escape);
        Check(_app.Dialog.Visible && _app.Dialog.Heading == "Abandon initial draft?", "Escape from focused company-name field still offers abandonment");
        await Click(DialogButton("Abandon draft"));
        Check(_app.MainMenu.Visible && !_app.MainMenu.ResumeButton.Visible && System.IO.File.ReadAllText(legacyFile) == original,
            "abandon legacy draft hides Resume and preserves the save");
        await Click(_app.MainMenu.LoadButton); await Click(SlotButton(0));
        if (_app.SaveDialog.Visible) await Click(DialogButton("Load Game"));
        Check(!ReferenceEquals(abandonedDraft, _app.Drafting.Draft) && _app.Drafting.Draft.Complete
            && _app.Drafting.NameInput.Text == _app.Drafting.Draft.ForceName && !_app.Drafting.EstablishButton.Disabled,
            "entering a new draft creates fresh valid defaults");
        var expectedRoster = _app.Drafting.Draft.Establish();
        await Click(_app.Drafting.EstablishButton);
        Check(!_app.State.RequiresDraft && !_app.Drafting.Visible && _app.State.Roster!.ForceName == expectedRoster.ForceName
            && _app.State.Roster.Mechs.Select(item => item.Spec.Id).SequenceEqual(expectedRoster.Mechs.Select(item => item.Spec.Id))
            && _app.State.Roster.Warriors.Select(item => item.Spec.Id).SequenceEqual(expectedRoster.Warriors.Select(item => item.Spec.Id)),
            "establishment commits the displayed random defaults without requiring edits");
        Check(_app.State.Cash == legacy.Cash && _app.State.Date == legacy.Date && _app.State.NextEventDate == legacy.Events[0].Date,
            "completing legacy draft preserves finances and calendar");
        Check(System.IO.File.ReadAllText(legacyFile) == original, "completing legacy draft waits for a manual save");
        _app.ShowFiling(); await Frames(); await Click(SlotButton(1));
        Check(_app.SaveDialog.Heading == "Overwrite save?", "legacy upgrade asks before replacing the save");
        await Click(DialogButton("Save"));
        Check(_app.Saves.Status.Text.StartsWith("Saved "), "legacy upgrade reports a successful save");
        Check(_app.SoloSaves.Load("legacy").Roster!.Warriors.Count == 4, "explicit save upgrades legacy roster");
        await Click(_app.Saves.CloseButton);
    }

    private Button DialogButton(string text) => (_app.UnsavedDialog.Visible ? _app.UnsavedDialog : _app.SaveDialog.Visible ? _app.SaveDialog : _app.Dialog)
        .FindChildren("*", "Button", true, false).OfType<Button>().Single(button => button.Text == text && button.IsInsideTree());

    private void DeleteSaves()
    {
        if (_saveRoot.Length > 0 && System.IO.Directory.Exists(_saveRoot)) System.IO.Directory.Delete(_saveRoot, true);
        if (_saveRoot.Length > 0 && System.IO.File.Exists(_saveRoot)) System.IO.File.Delete(_saveRoot);
        if (_legacyFile.Length > 0 && System.IO.File.Exists(_legacyFile)) System.IO.File.Delete(_legacyFile);
        if (_settingsFile.Length > 0 && System.IO.File.Exists(_settingsFile)) System.IO.File.Delete(_settingsFile);
    }

    /// <summary>Removes every save, solo and co-op.</summary>
    private void ClearSaves() { if (System.IO.Directory.Exists(_saveRoot)) System.IO.Directory.Delete(_saveRoot, true); }

    /// <summary>Writes a save straight to disk, as if saved earlier, and returns its slot id.</summary>
    private string SeedSave(Campaign campaign, string? name = null) =>
        (campaign.PlayerNames is null ? _app.SoloSaves : _app.CoopSaves).Save(campaign, name);

    /// <summary>Every save file's name and contents, to show that nothing on disk changed.</summary>
    private string SaveFingerprint() => !System.IO.Directory.Exists(_saveRoot) ? ""
        : string.Join("\n", System.IO.Directory.GetFiles(_saveRoot, "*", System.IO.SearchOption.AllDirectories).Order(StringComparer.Ordinal)
            .Select(file => file + "=" + System.IO.File.ReadAllText(file)));

    private string SlotName(Button slot) => slot.GetNode<Label>("Lines/SlotName").Text;

    private Button SlotButton(int index) => _app.Saves.SlotButtons[index];

    private Button SlotNamed(string name) => _app.Saves.SlotButtons.First(button => !button.Disabled && SlotName(button) == name);

    /// <summary>Saves through the filing cabinet into <paramref name="slot"/> (0 is the empty slot), then closes the grid.</summary>
    private async Task SaveFromFiling(int slot = 0, string? name = null)
    {
        _app.ShowOffice(); await Click(_app.Office.FilingButton);
        await Click(SlotButton(slot));
        if (name is not null) _app.SaveDialog.NameInput!.Text = name;
        await Click(DialogButton("Save"));
        Check(!_app.SaveDialog.Visible && _app.Saves.Status.Text.StartsWith("Saved ") && !_app.HasUnsavedChanges, "the filing cabinet saves into the chosen slot");
        await Click(_app.Saves.CloseButton);
    }

    /// <summary>Loads <paramref name="slot"/> through the filing cabinet, confirming when asked.</summary>
    private async Task LoadFromFiling(int slot = 0)
    {
        _app.ShowOffice(); await Click(_app.Office.FilingButton);
        await Click(_app.Saves.LoadTab); await Click(SlotButton(slot));
        if (_app.SaveDialog.Visible) await Click(DialogButton("Load Game"));
    }

    private async Task RightClick(Vector2 at)
    {
        MouseMotion(at, Vector2.Zero);
        MouseButton(at, Godot.MouseButton.Right, true);
        MouseButton(at, Godot.MouseButton.Right, false);
        await Frames();
    }

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

    /// <summary>Parsed input arrives in window pixels; convert from the scaled interface's coordinates.</summary>
    private static Vector2 ToWindow(Vector2 at) => ((SceneTree)Engine.GetMainLoop()).Root.GetFinalTransform() * at;
    private static Vector2 ToWindowDelta(Vector2 delta) => ((SceneTree)Engine.GetMainLoop()).Root.GetFinalTransform().BasisXform(delta);

    private static void MouseMotion(Vector2 at, Vector2 delta) => Input.ParseInputEvent(new InputEventMouseMotion
    {
        Position = ToWindow(at), GlobalPosition = ToWindow(at), Relative = ToWindowDelta(delta),
        ButtonMask = Input.IsMouseButtonPressed(Godot.MouseButton.Left) ? MouseButtonMask.Left : 0
    });

    private static void MouseButton(Vector2 at, Godot.MouseButton button, bool pressed, float factor = 1) => Input.ParseInputEvent(new InputEventMouseButton
    {
        Position = ToWindow(at), GlobalPosition = ToWindow(at), ButtonIndex = button, Pressed = pressed, Factor = factor
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

    // Metre-scale values carry proportionally larger float error than angles and ratios.
    private void Near(float actual, float expected, string message) => Check(Math.Abs(actual - expected) < Math.Max(.01f, Math.Abs(expected) * 1e-5f), $"{message}: expected {expected}, got {actual}");

    private async Task Capture(string name)
    {
        if (!_screenshots || DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://TestResults"));
        var result = GetViewport().GetTexture().GetImage().SavePng($"res://TestResults/{name}.png");
        Check(result == Error.Ok, $"saved {name} screenshot");
    }
}
