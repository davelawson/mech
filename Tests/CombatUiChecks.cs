using Godot;
using System.Text.Json;

namespace Mech.Tests;

public partial class SmokeTests
{
    private async Task TestCombatUi()
    {
        // Rendered command checks need a real focused window: combat intentionally ignores
        // commands after desktop focus loss. Do not bypass the production focus guard.
        if (_screenshots) { GetWindow().GrabFocus(); await Frames(); }
        await Click(_app.Office.HangarButton);
        await Click(_app.Hangar.RosterButtons[1]);
        await Click(_app.Hangar.DeploymentButton);
        var tactical = _app.Tactical;
        Check(tactical.Visible && !_app.Hangar.Visible && tactical.Modal == CombatModal.OpponentSelection && tactical.Simulation is null,
            "hangar simulation opens opponent selection before creating a battle");
        GetWindow().Size = new Vector2I(900, 500); await Frames();
        Check(tactical.GetGlobalRect().Encloses(tactical.RandomizedEnemyButton.GetGlobalRect())
            && tactical.GetGlobalRect().Encloses(tactical.MirrorMatchButton.GetGlobalRect())
            && tactical.GetGlobalRect().Encloses(tactical.BackButton.GetGlobalRect()), "opponent choices fit the minimum window");
        await Capture("deployment-opponent-900x500");
        await PressKey(Key.Escape);
        Check(_app.Hangar.Visible && !tactical.Visible, "Escape cancels opponent selection and returns to hangar");
        GetWindow().Size = new Vector2I(1440, 900); await Frames();
        await Click(_app.Hangar.DeploymentButton); await Click(tactical.MirrorMatchButton);
        Check(tactical.Modal == CombatModal.ForceSelection && tactical.Opponent == SimulationOpponent.Mirror,
            "mirror choice opens force selection");
        Check(!tactical.LaunchButton.Disabled && tactical.ForceChoices.All(item => !item.Disabled), "drafted company can deploy immediately with its assigned pilots");
        await PressKey(Key.Escape);
        var roster = _app.State.Roster!;
        var reserve = roster.Mechs[3]; roster.SetLance(reserve.Id, null);
        _app.Hangar.Refresh(); await Frames();
        await Click(_app.Hangar.DeploymentButton); await Click(tactical.MirrorMatchButton);
        Check(tactical.ForceChoices.Count == roster.Mechs.Count(mech => mech.Lance == 0)
            && !TacticalText().Any(text => text.Contains("In reserve")), "force selection leaves out reserve mechs");
        await PressKey(Key.Escape);
        roster.SetLance(reserve.Id, 0); _app.Hangar.Refresh(); await Frames();
        foreach (var mech in roster.Mechs) roster.Assign(mech.Id, null);
        _app.Hangar.Refresh();
        await Frames(); await Click(_app.Hangar.DeploymentButton);
        await Click(tactical.MirrorMatchButton);
        Check(tactical.LaunchButton.Disabled && tactical.ForceChoices.All(item => item.Disabled), "unassigned roster cannot launch simulation");
        await Capture("deployment-unavailable");
        await PressKey(Key.Escape);
        Check(_app.Hangar.Visible && _app.Hangar.SelectedMech == _app.State.Roster!.Mechs[1], "cancelling deployment preserves hangar mech");
        for (var i = 0; i < 4; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
        _app.Hangar.Refresh();
        await Frames();
        var before = JsonSerializer.Serialize(_app.State.Snapshot());
        await Click(_app.Hangar.DeploymentButton);
        await Click(tactical.MirrorMatchButton);
        Check(tactical.ForceChoices[1].ButtonPressed && tactical.ForceChoices.Count(item => item.ButtonPressed) == 1, "force selection preselects eligible hangar mech");
        await Click(tactical.ForceChoices[0]);
        tactical.TerrainSeedInput.Text = "invalid";
        tactical.TerrainSeedInput.EmitSignal(LineEdit.SignalName.TextChanged, "invalid");
        Check(tactical.LaunchButton.Disabled, "invalid terrain seed prevents launch with explanation");
        await Click(tactical.NewTerrainButton);
        Check(!tactical.LaunchButton.Disabled && int.TryParse(tactical.TerrainSeedInput.Text, out _), "New terrain supplies a valid seed");
        tactical.TerrainSeedInput.Text = "183";
        tactical.TerrainSeedInput.EmitSignal(LineEdit.SignalName.TextChanged, "183");
        Check(tactical.BiomePicker.ItemCount == 1 + Biomes.All.Count && tactical.BiomePicker.Selected == 0
            && tactical.BiomePicker.GetItemText(0) == "Random", "biome picker offers Random and every biome, defaulting to Random");
        Check(TacticalText().Any(text => Biomes.All.All(biome => text.Contains(biome.Name))), "Random lists every biome's climate");
        tactical.BiomePicker.Select(1 + Biomes.All.ToList().FindIndex(spec => spec.Biome == Biome.Valley));
        tactical.BiomePicker.EmitSignal(OptionButton.SignalName.ItemSelected, (long)tactical.BiomePicker.Selected);
        Check(TacticalText().Any(text => text.Contains(Biomes.Spec(Biome.Valley).ClimateText)), "choosing a biome shows its ambient temperature and cooling efficiency");
        await Capture("deployment-selection");
        GetWindow().Size = new Vector2I(900, 500); await Frames();
        Check(tactical.GetGlobalRect().Encloses(tactical.TerrainSeedInput.GetGlobalRect())
            && tactical.GetGlobalRect().Encloses(tactical.NewTerrainButton.GetGlobalRect())
            && tactical.GetGlobalRect().Encloses(tactical.LaunchButton.GetGlobalRect()), "terrain seed and launch controls fit the minimum window");
        await Capture("deployment-terrain-900x500");
        GetWindow().Size = new Vector2I(1440, 900); await Frames();
        await Click(tactical.LaunchButton); await CheckDropUi(tactical);
        var sim = tactical.Simulation!; var field = tactical.Battlefield;
        Check(tactical.Modal == CombatModal.None && sim.Paused && tactical.UnitButtons.Count == 2, "launch creates selected mirrored force and starts paused");
        Check(tactical.Selected.Count == 1 && !sim.Contacts(CombatSide.Friendly).Any(), "first friendly selected and enemy starts hidden");
        var slots = sim.Units.Where(unit => unit.Side == CombatSide.Friendly).ToArray();
        await PressKey(Key.Key2);
        Check(tactical.Selected.SequenceEqual([slots[1].Id]), "actual number input selects its friendly slot through the app");
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Exclam, PhysicalKeycode = Key.Key1, ShiftPressed = true, Pressed = true });
        await Frames();
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Exclam, PhysicalKeycode = Key.Key1, ShiftPressed = true, Pressed = false });
        Check(tactical.Selected.Count == 2, "actual shifted number input adds a friendly without replacing the selection");
        await PressKey(Key.Key1);
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Asciitilde, PhysicalKeycode = Key.Quoteleft, ShiftPressed = true, Pressed = true });
        await Frames();
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Asciitilde, PhysicalKeycode = Key.Quoteleft, ShiftPressed = true, Pressed = false });
        Check(tactical.BattleLog.Visible, "actual tilde input opens the battle log through the app");
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Quoteleft, ShiftPressed = true, Pressed = true });
        await Frames();
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Quoteleft, ShiftPressed = true, Pressed = false });
        Check(!tactical.BattleLog.Visible, "shift-backtick closes the battle log and restores the battlefield width");
        _app.StartAdvancing(true); Check(!_app.IsAdvancing, "campaign calendar cannot run during combat");
        var friendly = sim.Units.First(item => item.Side == CombatSide.Friendly);
        await CheckMaintainRangeRow(tactical, sim);
        Check(sim.Terrain.Seed == 183 && sim.Terrain.Biome == Biome.Valley, "deployment honors the entered terrain seed and chosen biome");
        Check(CombatScreen.HeatReadout(friendly).Contains("× 100%") && CombatScreen.HeatReadout(friendly).Contains("Ambient 10 · +0.20 heat/s"),
            "selected details split cooling into sinks × efficiency and ambient exchange");
        var hilltop = HighestGround(sim.Terrain);
        Check(field.GroundPoint(field.ScreenPoint(hilltop)).DistanceTo(hilltop) < .6f, "height-aware picking matches rendered hill elevation");
        var rock = sim.Terrain.Patches.First(patch => patch.Kind == TerrainKind.Rock);
        await PressKey(Key.Home);
        Check(field.GroundPoint(field.ScreenPoint(rock.Center, rock.Height)).DistanceTo(rock.Center) < .6f, "clicking a rock top picks its footprint rather than ground behind it");
        await RightClick(field.GlobalPosition + field.ScreenPoint(rock.Center, rock.Height));
        Check(friendly.Order == CombatOrder.Move && sim.Terrain.Passable(friendly.Destination)
            && friendly.Destination.DistanceTo(rock.Center) < rock.Radius + 90,
            $"actual ground click inside rock shows a nearby reachable destination: order={friendly.Order}, destination={friendly.Destination}, rock={rock.Center}, focus={GetWindow().HasFocus()}/{field.HasFocus()}");
        await Capture("terrain-overview");
        var destination = field.GlobalPosition + field.ScreenPoint(new Vector2(0, 480));
        await RightClick(destination);
        Check(tactical.Visible && !_app.Office.Visible && friendly.Order == CombatOrder.Move && sim.Elapsed == 0, "actual right-click queues Move while paused without campaign back navigation");
        await Capture("combat-paused");
        // Actual drag selects both friendly stand-ins.
        var points = sim.Units.Where(item => item.Side == CombatSide.Friendly).Select(item => field.GlobalPosition + field.ScreenPoint(item.Position, 15 * CombatRules.BodyScale)).ToArray();
        var from = new Vector2(points.Min(point => point.X) - 24, points.Min(point => point.Y) - 24);
        var to = new Vector2(points.Max(point => point.X) + 24, points.Max(point => point.Y) + 24);
        MouseMotion(from, Vector2.Zero); MouseButton(from, Godot.MouseButton.Left, true);
        MouseMotion(to, to - from); MouseButton(to, Godot.MouseButton.Left, false); await Frames();
        Check(tactical.Selected.Count == 2, "actual drag selection selects multiple friendly mechs");
        await RightClick(destination);
        Check(sim.Units.Where(item => item.Side == CombatSide.Friendly).Select(item => item.Destination).Distinct().Count() == 2, "UI group Move assigns distinct destinations");
        var leader = sim.Units.Last(item => item.Side == CombatSide.Friendly);
        var leaderDestination = leader.Destination;
        await RightClick(field.GlobalPosition + field.ScreenPoint(leader.Position, 15 * CombatRules.BodyScale));
        Check(friendly.Order == CombatOrder.Follow && friendly.FollowTarget == leader.Id
            && leader.Order == CombatOrder.Move && leader.Destination == leaderDestination && tactical.Selected.Count == 2,
            "right-click friendly queues Follow for the group without changing the selected leader's order");
        await Delay(.15);
        Check(!tactical.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.IsVisibleInTree()
            && (label.Text.Contains("Order: ") || label.Text.Contains("Priority: ") || label.Text.StartsWith("Jump "))),
            "selected-unit details no longer list the order, priority or jump lines");
        await Capture("combat-follow-order");
        await RightClick(destination);
        var oldSize = field.Camera.Size;
        var center = field.GlobalPosition + field.Size / 2;
        MouseButton(center, Godot.MouseButton.WheelUp, true);
        MouseButton(center, Godot.MouseButton.WheelUp, false); await Frames();
        Check(field.Camera.Size < oldSize, "battlefield wheel zoom works while paused");
        await TestCombatCamera();
        await TestCombatShortcuts();
        await PressKey(Key.Home);
        await PressKey(Key.Space);
        await Delay(.15);
        Check(!sim.Paused && sim.Elapsed > 0, "Space resumes real-time simulation");
        await PressKey(Key.Escape);
        var pausedAt = sim.Elapsed;
        Check(tactical.Modal == CombatModal.Menu && sim.Paused, "Escape opens suspended combat menu");
        await Delay(.15); Check(sim.Elapsed == pausedAt, "combat menu freezes combat clock");
        await Capture("combat-menu");
        await TestMissionDetails(tactical, sim);
        await TestCombatAudioSettings(tactical, sim);
        await Click(tactical.ResumeButton);
        Check(!sim.Paused && tactical.Modal == CombatModal.None, "menu Resume restores running state");
        await PressKey(Key.Space); Check(sim.Paused, "Space pauses resumed combat");
        await PressKey(Key.Escape); await PressKey(Key.Escape);
        Check(sim.Paused && tactical.Modal == CombatModal.None, "menu Resume preserves an already-paused state");
        await TestTimePanel();

        // Bring a contact into view to exercise priority targeting and enemy inspection through mouse input.
        var enemy = sim.Units.First(item => item.Side == CombatSide.Enemy);
        enemy.Position = friendly.Position + new Vector2(-150, -60); sim.RefreshDetection(); await Frames();
        var enemyAt = field.GlobalPosition + field.ScreenPoint(enemy.Position, 15 * CombatRules.BodyScale);
        var scroll = tactical.SidebarScroll;
        MouseButton(enemyAt, Godot.MouseButton.Right, true);
        CameraMotion(enemyAt + new Vector2(CombatViewport.RightDragThreshold + 1, 0), new Vector2(CombatViewport.RightDragThreshold + 1, 0));
        MouseButton(enemyAt, Godot.MouseButton.Right, false); await Frames();
        Check(tactical.Selected.Select(sim.Unit).All(item => item.PriorityTarget is null), "right-drag over an enemy orbits without choosing a priority target");
        var ordersBefore = tactical.Selected.Select(sim.Unit).Select(item => item.Order).ToArray();
        await RightClick(enemyAt);
        Check(tactical.Selected.Select(sim.Unit).All(item => item.PriorityTarget == enemy.Id && item.AttackTarget is null)
            && tactical.Selected.Select(sim.Unit).Select(item => item.Order).SequenceEqual(ordersBefore),
            "right-click on a detected enemy sets the group's priority target and issues no order");
        MouseButton(enemyAt, Godot.MouseButton.Left, true); MouseButton(enemyAt, Godot.MouseButton.Left, false); await Delay(.15);
        Check(tactical.Selected.Count == 2 && field.InspectedEnemy == enemy.Id, "enemy inspection retains the friendly command group and range reference");
        var displayed = tactical.FindChildren("*", "Label", true, false).OfType<Label>().Where(label => label.IsVisibleInTree()).Select(label => label.Text).ToArray();
        Check(tactical.Paperdoll.Unit == enemy && !displayed.Any(text => text.Contains("ARMOR / STOCK") || text.Contains("SYSTEMS ·")),
            "close enemy inspection shows condition on the paperdoll, without armour or systems text");
        Check(!tactical.Paperdoll.ShowsFuel, "detailed enemy inspection shows heat but no fuel bar");
        var radius = CombatRules.DetectionRadius(friendly, enemy);
        Check(field.DetectionRanges.Count == 2 && field.DetectionRanges.All(range => range.Identification == CombatRules.DetectionRadius(range.Observer, enemy)
            && range.Detail == range.Identification * .5f) && field.SensorRangeText.StartsWith("Selected mechs"),
            "enemy inspection shows target-specific identification and status ranges for every selected active friendly");
        scroll.ScrollVertical = 0; await Frames(); await Capture("combat-detailed-contact");
        var closePosition = enemy.Position;
        var otherFriends = sim.Units.Where(unit => unit.Side == CombatSide.Friendly && unit != friendly).ToArray();
        var previousPositions = otherFriends.Select(unit => unit.Position).ToArray();
        foreach (var unit in otherFriends) unit.Position = new Vector2(-1350, -1350);
        enemy.Position = Enumerable.Range(0, 32).Select(index => friendly.Position + Vector2.FromAngle(index * Mathf.Tau / 32) * radius * .75f)
            .First(point => point == CombatRules.Clamp(point) && sim.Terrain.Passable(point) && sim.Terrain.ClearSight(friendly.Position, point));
        sim.RefreshDetection(); await Delay(.15);
        displayed = tactical.FindChildren("*", "Label", true, false).OfType<Label>().Where(label => label.IsVisibleInTree()).Select(label => label.Text).ToArray();
        Check(sim.Detected(CombatSide.Friendly, enemy.Id) && !sim.DetailsVisible(CombatSide.Friendly, enemy.Id)
            && tactical.Paperdoll.Unit is null && !tactical.Paperdoll.Visible && displayed.Contains(enemy.Spec.Chassis)
            && !displayed.Any(text => text.Contains(enemy.Spec.Variant)),
            "model-only inspection hides variant, armor, internals, systems and stale detailed readouts");
        await Capture("combat-model-contact");
        sim.SetPriority([friendly.Id], null);
        await RightClick(field.GlobalPosition + field.ScreenPoint(enemy.Position, 15 * CombatRules.BodyScale));
        Check(friendly.PriorityTarget == enemy.Id && friendly.AttackTarget is null, "model-only contact accepts an actual mouse priority target");
        enemy.Position = new Vector2(-1350, -300); sim.RefreshDetection(); await Delay(.15);
        Check(field.InspectedEnemy is null && tactical.Paperdoll.Unit == friendly,
            "losing inspection contact clears live enemy details and restores friendly information");
        for (var i = 0; i < otherFriends.Length; i++) otherFriends[i].Position = previousPositions[i];
        enemy.Position = closePosition; sim.RefreshDetection();
        await Click(tactical.UnitButtons[0]);
        Check(field.DetectionRanges is [{ Detail: null }] && field.SensorRangeText.Contains("20–100t"),
            "without an inspected target the circle uses 100t and labels the 20–100t detection span");
        for (var zoom = 0; zoom < 4; zoom++)
        {
            var at = field.GlobalPosition + field.Size / 2;
            MouseButton(at, Godot.MouseButton.WheelUp, true); MouseButton(at, Godot.MouseButton.WheelUp, false);
        }
        await Frames();
        await PressKey(Key.Space); await Delay(.15); await PressKey(Key.Space);
        foreach (var size in new[] { new Vector2I(1440, 900), new Vector2I(900, 500) })
        {
            GetWindow().Size = size; await Frames();
            Check(tactical.GetGlobalRect().Encloses(tactical.FastForwardButton.GetGlobalRect()) && field.Size.X > 500 && field.Size.Y > 300, $"combat field and time controls survive {size}");
            await Capture($"combat-{size.X}x{size.Y}");
        }
        GetWindow().Size = new Vector2I(1440, 900); await Frames();
        await TestTerrainUi();
        await PressKey(Key.Escape); await Click(tactical.EndButton);
        Check(tactical.Modal == CombatModal.Results && sim.Outcome == CombatOutcome.Ended, "End Simulation shows damage summary");
        await Capture("combat-results");
        await TestResultsTabs(tactical, sim);
        await Click(tactical.ReturnButton);
        Check(_app.Hangar.Visible && !tactical.Visible && _app.Hangar.SelectedMech == roster.Mechs[1], "summary returns to preserved hangar selection");
        Check(JsonSerializer.Serialize(_app.State.Snapshot()) == before, "UI simulation preserves campaign date, finances, roster, equipment, assignments and warriors");
        Check(_app.Hangar.DeploymentButton.HasFocus(), "return restores focus to simulation launcher");
        // Relaunch and exercise automatic completion through the UI.
        await Click(_app.Hangar.DeploymentButton); await Click(tactical.RandomizedEnemyButton);
        Check(tactical.Modal == CombatModal.ForceSelection && tactical.Opponent == SimulationOpponent.Randomized && tactical.Simulation is null,
            "randomized choice defers enemy generation until launch");
        await Click(tactical.ForceChoices[0]);
        tactical.TerrainSeedInput.Text = "183";
        tactical.TerrainSeedInput.EmitSignal(LineEdit.SignalName.TextChanged, "183");
        tactical.ChooseBiome(Biome.Lunar);
        await Click(tactical.LaunchButton); await Land(tactical);
        var randomFriends = tactical.Simulation!.Units.Where(unit => unit.Side == CombatSide.Friendly).ToArray();
        var randomEnemies = tactical.Simulation.Units.Where(unit => unit.Side == CombatSide.Enemy).ToArray();
        Check(randomFriends.Length == 2 && randomEnemies.Length == 2 && randomFriends.Zip(randomEnemies)
            .All(pair => pair.First.Spec.WeightClass == pair.Second.Spec.WeightClass) && tactical.Simulation.Terrain.Seed == 183
            && tactical.Simulation.Terrain.Biome == Biome.Lunar && tactical.Simulation.Units.All(unit => unit.Climate == Biomes.Spec(Biome.Lunar)),
            "randomized launch honors selected force categories, count and terrain seed");
        foreach (var unit in tactical.Simulation!.Units.Where(item => item.Side == CombatSide.Enemy)) unit.ApplyDamage(MechLocation.CenterTorso, 10000);
        tactical.TogglePause(); await Delay(.15);
        Check(tactical.Modal == CombatModal.None && tactical.Simulation.WindingDown, "the first defeat begins a wind-down before results");
        await Delay(CombatRules.WindDownSeconds);
        Check(tactical.Modal == CombatModal.Results && tactical.Simulation.Outcome == CombatOutcome.Victory, "automatic combat outcome opens results");
        await PressKey(Key.Escape);
        Check(_app.Hangar.Visible && JsonSerializer.Serialize(_app.State.Snapshot()) == before, "result Escape preserves campaign and returns to hangar");
        await PressKey(Key.Escape);
    }
    /// <summary>The results' three tabs; clicking a mech's row opens and closes its weapon table.</summary>
    private async Task TestResultsTabs(CombatScreen tactical, CombatSimulation sim)
    {
        var tabs = tactical.ResultsTabs;
        Check(tabs.GetTabCount() == 3 && tabs.GetTabTitle(0) == "Outcome" && tabs.GetTabTitle(1) == "Mechs" && tabs.GetTabTitle(2) == "Highlights"
            && tabs.CurrentTab == 0, "the results open on Outcome, beside Mechs and Highlights");
        Check(tabs.GetGlobalRect().Size.X > tactical.Size.X * .8f, "the results fill most of the screen");
        Check(TacticalText().Contains("YOUR FORCE") && sim.Units.Where(unit => unit.Side == CombatSide.Friendly).All(unit => TacticalText()
            .Any(text => text.StartsWith($"{unit.Spec.DisplayName} · “{unit.Warrior.Callsign}”") && text.Contains($": {BattleSummary.Condition(unit)} · "))),
            "the Outcome tab lists each company mech with its condition and its pilot's");
        Check(tactical.SkillGainLines.Count == 0, "a simulated battle rolls no skill gain checks");
        tabs.CurrentTab = 1; await Frames();
        Check(tactical.ResultRows.Count == sim.Units.Count && TacticalText().Contains("LANCE 1") && TacticalText().Contains("OPPOSITION")
            && TacticalText().Contains("DAMAGE TAKEN") && TacticalText().Contains("DAMAGE DEALT"),
            "the Mechs tab has a row for every mech, in a table per lance and for the opposition");
        var unit = sim.Units[0]; var (row, weapons) = tactical.ResultRows[unit.Id];
        var at = row.GetGlobalRect().Position + new Vector2(24, row.Size.Y / 2);
        Check(!weapons.Visible, "a mech's weapon table starts closed");
        MouseMotion(at, Vector2.Zero); MouseButton(at, Godot.MouseButton.Left, true); MouseButton(at, Godot.MouseButton.Left, false); await Frames();
        Check(weapons.Visible && tactical.ExpandedResults.Contains(unit.Id), "clicking a mech's row opens its weapon table");
        Check(row.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.TooltipText.Contains("Gunnery")),
            "hovering the pilot shows their skills");
        await Capture("combat-results-mechs");
        MouseButton(at, Godot.MouseButton.Left, true); MouseButton(at, Godot.MouseButton.Left, false); await Frames();
        Check(!weapons.Visible && !tactical.ExpandedResults.Contains(unit.Id), "clicking it again closes the table");
        tabs.CurrentTab = 2; await Frames();
        Check(tactical.HighlightList.GetChildCount() == Math.Max(1, sim.Highlights.Count), "the Highlights tab lists the battle's highlights");
        tabs.CurrentTab = 0; await Frames();
    }
    /// <summary>Quick Launch fights the accepted mission from the hangar and settles it into the campaign.</summary>
    /// <summary>Audio settings from the open Combat menu, over the paused battle.</summary>
    private async Task TestMissionDetails(CombatScreen tactical, CombatSimulation sim)
    {
        var pausedAt = sim.Elapsed;
        await Click(tactical.MissionDetailsButton);
        Check(tactical.Modal == CombatModal.MissionDetails && sim.Paused, "the Combat menu's Mission Details opens over the paused battle");
        var text = string.Join("\n", TacticalText());
        Check(text.Contains("MISSION") && text.Contains("no campaign consequences") && text.Contains("BATTLEFIELD")
            && text.Contains($"BIOME  {Biomes.Spec(Biome.Valley).Name}") && text.Contains($"CLIMATE  {Biomes.Spec(Biome.Valley).ClimateText}")
            && text.Contains("TERRAIN  ") && text.Contains("COMBAT TIME"), $"Mission Details describe the practice battle and its battlefield:\n{text}");
        Check(tactical.FindChildren("*", "LineEdit", true, false).OfType<LineEdit>().Any(edit => edit.IsVisibleInTree() && !edit.Editable && edit.Text == "183"),
            "Mission Details offer the terrain seed for copying");
        Check(!TacticalText().Any(line => line.Contains("Terrain seed: ")), "the seed has moved from the Combat menu into Mission Details");
        GetWindow().Size = new Vector2I(900, 500); await Frames();
        Check(tactical.GetGlobalRect().Encloses(tactical.MissionDetailsBackButton.GetGlobalRect()), "Mission Details' Back fits the minimum window");
        await Capture("combat-mission-details-900x500");
        GetWindow().Size = new Vector2I(1440, 900); await Frames();
        await Capture("combat-mission-details");
        await PressKey(Key.Escape);
        Check(tactical.Modal == CombatModal.Menu && sim.Paused, "Escape returns from Mission Details to the Combat menu, still paused");
        await Click(tactical.MissionDetailsButton);
        var center = tactical.MissionDetailsBackButton.GetGlobalRect().GetCenter() + new Vector2(0, -60);
        MouseMotion(center, Vector2.Zero);
        MouseButton(center, Godot.MouseButton.Right, true); MouseButton(center, Godot.MouseButton.Right, false); await Frames();
        Check(tactical.Modal == CombatModal.Menu, "right-click returns from Mission Details to the Combat menu");
        await Click(tactical.MissionDetailsButton);
        await Click(tactical.MissionDetailsBackButton);
        Check(tactical.Modal == CombatModal.Menu && sim.Paused && sim.Elapsed == pausedAt, "Back returns to the Combat menu without advancing the battle");
    }

    private async Task TestCombatAudioSettings(CombatScreen tactical, CombatSimulation sim)
    {
        var pausedAt = sim.Elapsed;
        await Click(tactical.AudioButton);
        Check(_app.Navigation.CurrentScreen == CommandScreen.Audio && _app.AudioOptions.Visible && tactical.Visible && tactical.Modal == CombatModal.Menu,
            "the Combat menu's Audio opens the audio settings over the battle");
        await Frames();
        Check(_app.Sound.Music == MusicContext.Combat, "the battle theme keeps playing under the audio settings");
        var before = _app.AudioLevels.Effects;
        _app.AudioOptions.EffectsSlider.Value = before == 50 ? 55 : 50;
        Check(AudioSettings.Load(_settingsFile).Effects == (int)_app.AudioOptions.EffectsSlider.Value, "a level changed in battle saves at once");
        await Delay(.15);
        Check(sim.Paused && sim.Elapsed == pausedAt, "the battle stays paused while the audio settings are open");
        GetWindow().Size = new Vector2I(900, 500); await Frames();
        Check(_app.GetViewportRect().Encloses(_app.AudioOptions.BackButton.GetGlobalRect())
            && _app.GetViewportRect().Encloses(_app.AudioOptions.MuteToggle.GetGlobalRect()), "the audio settings fit the minimum window in battle");
        await Capture("combat-audio-900x500");
        GetWindow().Size = new Vector2I(1440, 900); await Frames();
        await PressKey(Key.Escape); await Frames();
        Check(_app.Navigation.CurrentScreen == CommandScreen.Tactical && !_app.AudioOptions.Visible && tactical.Modal == CombatModal.Menu
            && sim.Paused && tactical.ResumeButton.HasFocus(), "Escape closes the audio settings back to the Combat menu with Resume focused");
        await Click(tactical.AudioButton); await RightClick(new Vector2(20, 20));
        Check(_app.Navigation.CurrentScreen == CommandScreen.Tactical && tactical.Modal == CombatModal.Menu && sim.Paused,
            "right-click closes the audio settings back to the Combat menu");
        await Click(tactical.AudioButton); await Click(_app.AudioOptions.BackButton);
        Check(_app.Navigation.CurrentScreen == CommandScreen.Tactical && tactical.Modal == CombatModal.Menu, "Back returns to the Combat menu");
        _app.AudioOptions.EffectsSlider.Value = before;
    }

    private async Task TestQuickLaunchUi()
    {
        var hangar = _app.Hangar; var tactical = _app.Tactical; var state = _app.State;
        if (!hangar.Visible) { if (!_app.Office.Visible) _app.ShowOffice(); await Frames(); await Click(_app.Office.HangarButton); }
        Check(hangar.Visible && hangar.QuickLaunchButton.Disabled && hangar.QuickLaunchButton.TooltipText.Contains("office phone"),
            "Quick Launch waits for an accepted mission");
        await PressKey(Key.Escape);
        var offer = state.Missions.Available[0]; state.AcceptMission(offer.Id);
        await Click(_app.Office.HangarButton);
        Check(!hangar.QuickLaunchButton.Disabled, "an accepted mission can be launched from the hangar wherever the DropShip is");
        GetWindow().Size = new Vector2I(900, 500); await Frames();
        Check(new[] { hangar.ResetButton, hangar.ApplyButton, hangar.BackButton, hangar.QuickLaunchButton, hangar.DeploymentButton }
            .All(button => hangar.GetGlobalRect().Encloses(button.GetGlobalRect())), "hangar actions fit the minimum window");
        await Capture("hangar-quick-launch-900x500");
        GetWindow().Size = new Vector2I(1440, 900); await Frames();
        var weapon = hangar.SelectedMech.Spec.Weapons.First(item => item.AmmoKind is null);
        var (panel, block) = FindBlock(hangar, item => item.ItemId == weapon.Id);
        await RightClick(BlockCentre(panel, block));
        Check(hangar.HasPendingChanges && hangar.QuickLaunchButton.Disabled && hangar.QuickLaunchButton.TooltipText.Contains("loadout changes"),
            "pending loadout changes block Quick Launch");
        await Click(hangar.ResetButton);
        Check(!hangar.QuickLaunchButton.Disabled, "resetting the changes allows Quick Launch again");

        await Click(hangar.QuickLaunchButton);
        var biome = StarChart.Instance[offer.SystemId].Biome ?? Biome.Valley;
        Check(tactical.Visible && tactical.MissionMode && tactical.Modal == CombatModal.ForceSelection && tactical.BiomePicker.Disabled
            && tactical.BiomePicker.Selected == 1 + Biomes.All.ToList().FindIndex(spec => spec.Biome == biome) && tactical.LaunchButton.Text == "Launch mission",
            "Quick Launch goes straight to force selection on the mission system's biome");
        await PressKey(Key.Escape);
        Check(hangar.Visible && state.Missions.Current == offer && hangar.QuickLaunchButton.HasFocus(), "backing out keeps the mission and restores focus");

        var date = state.Date; var cash = state.Cash;
        await Click(hangar.QuickLaunchButton); await Click(tactical.LaunchButton); await Land(tactical);
        var sim = tactical.Simulation!;
        Check(sim.Units.Count(unit => unit.Side == CombatSide.Enemy) == offer.Opposition.Length && sim.Terrain.Biome == biome,
            "the mission fields its opposition");
        var friend = sim.Units.First(unit => unit.Side == CombatSide.Friendly); friend.ApplyDamage(MechLocation.LeftArm, 20);
        await PressKey(Key.Escape);
        Check(tactical.EndButton.Text == "Withdraw", "the combat menu offers withdrawal from a mission");
        await Click(tactical.EndButton);
        Check(tactical.Modal == CombatModal.Results && TacticalText().Any(text => text.Contains("Mission failed"))
            && TacticalText().Any(text => text.Contains("No payment") && text.Contains("Insurance") && text.Contains(state.DateText)),
            "withdrawing fails the mission and shows its settlement");
        Check(tactical.SalvagePool.Count == 0 && tactical.SalvageChoices.Count == 0, "a failed mission offers no salvage");
        var growth = sim.Units.Where(unit => unit.Side == CombatSide.Friendly).SelectMany(unit => sim.SkillGains(unit)
            .Select(skill => $"      {SkillGrowth.Describe(unit.Warrior, unit.Progress, skill)}")).ToArray();
        Check(tactical.SkillGainLines.Select(label => label.Text).SequenceEqual(growth)
            && tactical.SkillGainLines.All(label => label.IsVisibleInTree() && label.GetParent() == tactical.SkillGainLines[0].GetParent()),
            $"the Outcome tab lists each successful skill gain check under its pilot ({string.Join(" / ", growth).Trim()})");
        Check(sim.Units.Where(unit => unit.Side == CombatSide.Friendly).All(unit =>
            {
                var (spec, progress) = (unit.Warrior, unit.Progress);
                foreach (var skill in sim.SkillGains(unit)) (spec, progress, _) = SkillGrowth.Gain(spec, progress, skill);
                var pilot = state.Roster!.Warriors.Single(item => item.Id == unit.Warrior.Id);
                return pilot.Spec == spec && pilot.Progress == progress;
            }), "even a failed mission banks its pilots' skill gains");
        Check(state.Missions.Current is null && state.Date == date.AddDays(1) && state.Cash >= cash && state.Cash - cash <= offer.InsuranceLimit
            && state.Roster!.Mechs.Single(mech => mech.Id == friend.Id).Damaged, "the settlement keeps damage, pays only insurance and takes a day");
        await Capture("mission-results");
        await Click(tactical.ReturnButton);
        Check(hangar.Visible && !tactical.Visible && hangar.BackButton.HasFocus() && hangar.QuickLaunchButton.Disabled && _app.HasUnsavedChanges,
            "returning to the hangar shows the settled campaign");
        await PressKey(Key.Escape);
    }
    /// <summary>A won mission offers the field's salvage on the results, and settles once it is claimed.</summary>
    private async Task TestSalvageUi()
    {
        var hangar = _app.Hangar; var tactical = _app.Tactical; var state = _app.State;
        var offer = state.Missions.Available.First(item => item.Kind is MissionKind.Raid or MissionKind.Defense); state.AcceptMission(offer.Id);
        if (!_app.Office.Visible) _app.ShowOffice();
        await Frames(); await Click(_app.Office.HangarButton);
        var mechs = state.Roster!.Mechs.Count;
        await Click(hangar.QuickLaunchButton); await Click(tactical.LaunchButton); await Land(tactical);
        var sim = tactical.Simulation!;
        // The lightest enemy loses both legs, disabled but whole (claimable within any allotment plus the extra picks); the rest lose their center torso.
        var enemies = sim.Units.Where(unit => unit.Side == CombatSide.Enemy).OrderBy(unit => unit.Spec.WeightClass).ToArray();
        foreach (var leg in new[] { MechLocation.LeftLeg, MechLocation.RightLeg })
            enemies[0].ApplyDamage(leg, enemies[0].Locations[leg].Armor + enemies[0].Locations[leg].Structure);
        foreach (var enemy in enemies.Skip(1)) enemy.ApplyDamage(MechLocation.CenterTorso, 100000);
        sim.Paused = false;
        // The battle winds down for a few seconds after the last enemy falls.
        for (var step = 0; step < 2000 && sim.Outcome == CombatOutcome.Ongoing; step++) { sim.Advance(CombatRules.StepSeconds); if (step % 50 == 0) await Frames(); }
        await Frames();
        Check(sim.Outcome == CombatOutcome.Victory && enemies[0].Status == CombatStatus.Disabled && tactical.Modal == CombatModal.Results,
            "defeating the opposition wins the mission");
        Check(tactical.SalvagePool.Any(option => option.WholeMech && option.UnitId == enemies[0].Id) && tactical.SalvageChoices.Count == tactical.SalvagePool.Count
            && state.Missions.Current == offer && TacticalText().Any(text => text.Contains("SALVAGE")), "a won mission offers salvage before settling");
        Check(tactical.ClaimSalvageButton.Disabled && tactical.SalvageCount.Text.StartsWith($"Picks 0 of {offer.SalvagePicks}"), "nothing is claimed until picks are chosen");
        var salvagedGun = tactical.SalvagePool.FirstOrDefault(option => DefinitionText.WeaponDetails(option.StorageId) is not null);
        Check(salvagedGun is null || tactical.SalvageChoices[salvagedGun.Claim].TooltipText.Contains(DefinitionText.WeaponDetails(salvagedGun.StorageId)!)
            && tactical.SalvageChoices[salvagedGun.Claim].TooltipText.EndsWith("Goes into hangar storage", StringComparison.Ordinal),
            "a salvaged weapon's tooltip gives its mass, damage, heat, range and ammunition");
        await PressKey(Key.Escape);
        Check(tactical.Visible && tactical.Modal == CombatModal.Results && state.Missions.Current == offer, "Escape cannot skip choosing salvage");
        var whole = tactical.SalvagePool.Single(option => option.WholeMech);
        tactical.SalvageChoices[whole.Claim].ButtonPressed = true;
        Check(tactical.SalvagePool.Where(option => option.UnitId == whole.UnitId && !option.WholeMech).All(option => tactical.SalvageChoices[option.Claim].Disabled),
            "claiming a mech whole disables stripping it");
        var item = tactical.SalvagePool.FirstOrDefault(option => !option.WholeMech && option.UnitId != whole.UnitId);
        if (item is not null) tactical.SalvageChoices[item.Claim].ButtonPressed = true;
        var picks = whole.Picks + (item is null ? 0 : 1); var limit = offer.SalvagePicks + SalvageTuning.ExtraPicks;
        Check(tactical.SalvageCount.Text.StartsWith($"Picks {picks} of {offer.SalvagePicks}") && tactical.ClaimSalvageButton.Disabled == picks > limit
            && tactical.SalvageCount.Text.Contains("over the allotment") == picks > offer.SalvagePicks, "the count follows the picks and shows the cost of extra ones");
        GetWindow().Size = new Vector2I(900, 500); await Frames();
        Check(new[] { tactical.ClaimSalvageButton, tactical.TakeNothingButton }.All(button => tactical.GetGlobalRect().Encloses(button.GetGlobalRect())),
            "the salvage actions fit the minimum window");
        await Capture("mission-salvage-900x500");
        GetWindow().Size = new Vector2I(1440, 900); await Frames();
        var keepItem = item is not null && picks <= limit;
        if (item is not null && !keepItem) tactical.SalvageChoices[item.Claim].ButtonPressed = false;
        var stored = item is null ? 0 : state.Roster.Stored(item.StorageId!);
        await Click(tactical.ClaimSalvageButton);
        Check(state.Missions.Current is null && tactical.SalvagePool.Count == 0 && state.Roster.Mechs.Count == mechs + 1
            && state.Roster.Mechs[^1].Definition.Id == enemies[0].Spec.Id && state.Roster.Mechs[^1].Lance is null,
            "claiming settles the mission and the whole mech joins the reserves");
        Check(!keepItem || state.Roster.Stored(item!.StorageId!) == stored + 1, "a claimed item joins storage");
        Check(TacticalText().Any(text => text.Contains("Payment received") && text.Contains("Insurance") && text.Contains("Salvaged whole")),
            "the results list the payment, insurance and salvage");
        await Click(tactical.ReturnButton);
        Check(hangar.Visible && hangar.RosterButtons.Count == mechs + 1, "the hangar shows the salvaged mech");
        await PressKey(Key.Escape);
    }
    private async Task TestPatrolUi()
    {
        var roster = ForceRoster.Create("Patrol checks", new[] { "wolverine", "jenner" }.Select(DraftCatalog.Mech), TestPilots.Warriors.Take(2));
        for (var i = 0; i < 2; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
        var tactical = _app.Tactical; _app.Navigation.Push(CommandScreen.Tactical, _app.Hangar.DeploymentButton);
        tactical.Open(roster, roster.Mechs[0].Id); await Frames();
        GetWindow().Size = new Vector2I(900, 500); await Frames();
        Check(tactical.GetGlobalRect().Encloses(tactical.PatrolButton.GetGlobalRect()) && tactical.GetGlobalRect().Encloses(tactical.BackButton.GetGlobalRect()),
            "the Patrol choice fits the minimum window");
        GetWindow().Size = new Vector2I(1440, 900); await Frames();
        await Click(tactical.PatrolButton);
        Check(tactical.Opponent == SimulationOpponent.Patrol && tactical.Modal == CombatModal.ForceSelection, "Simulate Deployment offers a patrol");
        if (!tactical.ForceChoices[1].ButtonPressed) await Click(tactical.ForceChoices[1]);
        tactical.ChooseBiome(Biome.Grassland);
        await Click(tactical.LaunchButton); await Land(tactical);
        var sim = tactical.Simulation!;
        Check(sim.Patrol is { } patrol && patrol.Groups.Count == patrol.Layout.PatrolPoints && sim.Units.Where(unit => unit.Side == CombatSide.Enemy).All(sim.Passive)
            && TacticalText().Any(text => text.Contains($"Patrol point 1 of {patrol.Layout.PatrolPoints}")),
            "a simulated patrol launches with passive enemy groups and an objective readout");
        await Frames(); await Capture("combat-patrol-start");
        sim.EnemyAiEnabled = false;
        var scout = sim.Units.First(unit => unit.Side == CombatSide.Friendly);
        scout.Position = sim.Patrol!.Layout.Locations[0];
        var paused = sim.Paused; sim.Paused = false; sim.Advance(.1); sim.Paused = paused;
        // The readout refreshes ten times a second.
        await ToSignal(GetTree().CreateTimer(.25), SceneTreeTimer.SignalName.Timeout); await Frames();
        var logged = tactical.BattleLog.FindChildren("*", "RichTextLabel", true, false).OfType<RichTextLabel>()
            .Any(text => text.GetParsedText().Contains("Patrol point 1 of"));
        Check(sim.Patrol.Visited == 1 && logged && TacticalText().Any(text => text.Contains(sim.Patrol.AllVisited ? "Extract:" : "Patrol point 2 of")),
            "visiting a patrol point advances the readout and is logged");
        tactical.Battlefield.GrabFocus(); await PressKey(Key.Home); await Frames();
        await Capture("combat-patrol-arena");
        await PressKey(Key.Escape); await Click(tactical.EndButton); await Click(tactical.ReturnButton);
    }
    private async Task TestTakeAndHoldUi()
    {
        var roster = ForceRoster.Create("Hold checks", new[] { "wolverine", "jenner" }.Select(DraftCatalog.Mech), TestPilots.Warriors.Take(2));
        for (var i = 0; i < 2; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
        var tactical = _app.Tactical; _app.Navigation.Push(CommandScreen.Tactical, _app.Hangar.DeploymentButton);
        tactical.Open(roster, roster.Mechs[0].Id); await Frames();
        GetWindow().Size = new Vector2I(900, 500); await Frames();
        Check(tactical.GetGlobalRect().Encloses(tactical.TakeAndHoldButton.GetGlobalRect()) && tactical.GetGlobalRect().Encloses(tactical.BackButton.GetGlobalRect()),
            "the Take and Hold choice fits the minimum window");
        GetWindow().Size = new Vector2I(1440, 900); await Frames();
        await Click(tactical.TakeAndHoldButton);
        Check(tactical.Opponent == SimulationOpponent.TakeAndHold && tactical.Modal == CombatModal.ForceSelection, "Simulate Deployment offers a Take and Hold");
        if (!tactical.ForceChoices[1].ButtonPressed) await Click(tactical.ForceChoices[1]);
        tactical.ChooseBiome(Biome.Grassland);
        await Click(tactical.LaunchButton); await Land(tactical);
        var sim = tactical.Simulation!;
        var bar = tactical.ZoneBars.SingleOrDefault();
        Check(sim.TakeAndHold is not null && bar is not null && bar.Zone == sim.Objective && tactical.ZonePanel.Visible && bar.IsVisibleInTree()
            && TacticalText().Contains("Take the objective") && TacticalText().Any(text => text.StartsWith("Reinforcements: ", StringComparison.Ordinal))
            && bar.Caption == "OBJECTIVE · 0%",
            "a simulated Take and Hold shows the objective, its control bar and the reinforcements to come");
        Check(bar!.GetGlobalRect().Position.Y >= tactical.ObjectiveLabel.GetGlobalRect().End.Y && bar.GetGlobalRect().Position.X < 40
            && tactical.ReinforcementLabel.GetGlobalRect().Position.Y >= bar.GetGlobalRect().End.Y,
            "the control bar sits at the top left directly under the objective, with the reinforcements line below it");
        await Frames(); await Capture("combat-take-and-hold-start");
        sim.EnemyAiEnabled = false;
        foreach (var unit in sim.Units) unit.Firing = FiringMode.HoldFire;
        var scout = sim.Units.First(unit => unit.Side == CombatSide.Friendly);
        scout.Position = sim.Terrain.ReachableDestination(sim.FriendlyZone!.Anchor, sim.Objective!.Center);
        foreach (var guard in sim.Units.Where(unit => unit.Wave == 0)) guard.Position = sim.Objective.Center + new Vector2(2000, 0);
        var paused = sim.Paused; sim.Paused = false; sim.Advance(6); sim.Paused = paused;
        await ToSignal(GetTree().CreateTimer(.25), SceneTreeTimer.SignalName.Timeout); await Frames();
        var logged = tactical.BattleLog.FindChildren("*", "RichTextLabel", true, false).OfType<RichTextLabel>()
            .Any(text => text.GetParsedText().Contains("Objective contested"));
        Check(sim.Objective.Contested && bar.Caption.StartsWith("OBJECTIVE · 2", StringComparison.Ordinal) && bar.Caption.EndsWith("CONTESTED", StringComparison.Ordinal)
            && logged, $"taking the objective fills its bar, marks it contested and is logged ({bar.Caption})");
        tactical.Battlefield.GrabFocus(); await PressKey(Key.Home); await Frames();
        await Capture("combat-take-and-hold-arena");
        await PressKey(Key.Escape); await Click(tactical.EndButton);
        Check(TacticalText().Any(text => text.StartsWith("Combat time", StringComparison.Ordinal)), "the Take and Hold ends to its results");
        await Click(tactical.ReturnButton);
    }
    private string[] TacticalText() => _app.Tactical.FindChildren("*", "Label", true, false).OfType<Label>()
        .Where(label => label.IsVisibleInTree()).Select(label => label.Text).ToArray();
    /// <summary>The highest passable grid point: the generated relief's hilltop.</summary>
    private static Vector2 HighestGround(BattlefieldTerrain terrain)
    {
        var best = Vector2.Zero;
        for (var z = -2850f; z <= 2850; z += 30) for (var x = -2850f; x <= 2850; x += 30)
            if (terrain.HeightAt(new(x, z)) > terrain.HeightAt(best) && terrain.Passable(new(x, z))) best = new(x, z);
        return best;
    }
    private async Task TestTerrainUi()
    {
        var tactical = _app.Tactical; var sim = tactical.Simulation!; var terrain = sim.Terrain;
        var friends = sim.Units.Where(unit => unit.Side == CombatSide.Friendly).ToArray();
        var target = sim.Units.First(unit => unit.Side == CombatSide.Enemy);
        foreach (var unit in sim.Units) { sim.Hold([unit.Id]); unit.Firing = FiringMode.HoldFire; }
        static IEnumerable<Vector2> Around(Vector2 center, float radius) => Enumerable.Range(0, 24).Select(index => center + Vector2.FromAngle(index * Mathf.Tau / 24) * radius);
        var rock = terrain.Patches.First(patch => patch.Kind == TerrainKind.Rock && terrain.SlopeAt(patch.Center) < .1f
            && Around(patch.Center, patch.Radius + 90).All(terrain.Passable));
        friends[0].Position = rock.Center + Vector2.Left * (rock.Radius + 90);
        target.Position = rock.Center + Vector2.Right * (rock.Radius + 90);
        friends[1].Position = Around(target.Position, 180).First(point => terrain.Passable(point)
            && terrain.ClearSight(point, target.Position) && point.DistanceTo(friends[0].Position) > 90);
        sim.RefreshDetection(); tactical.SelectUnits([friends[0].Id], false); await Frames();
        sim.Attack([friends[0].Id], target.Id); await Delay(.15);
        Check(tactical.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.Text.Contains("Line of fire blocked by terrain")),
            "combat information explains why a shared contact cannot be fired at through rock");
        await PressKey(Key.Home); await Capture("terrain-blocked-fire");
        var vegetation = terrain.Patches.First(patch => patch.Kind == TerrainKind.Vegetation && terrain.Passable(patch.Center)
            && Around(patch.Center, patch.Radius + 60).Any(point => terrain.Passable(point) && terrain.ClearSight(point, patch.Center)));
        target.Position = vegetation.Center;
        friends[0].Position = Around(target.Position, vegetation.Radius + 60).First(point => terrain.Passable(point) && terrain.ClearSight(point, target.Position));
        sim.RefreshDetection(); tactical.SelectUnits([friends[0].Id], false); await Delay(.15);
        Check(tactical.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.Text.Contains("Vegetation: +")),
            "combat information shows the vegetation hit penalty");
        var hill = HighestGround(terrain);
        friends[0].Position = hill; await Frames();
        Check(tactical.Battlefield.FindChildren("*", "Node3D", true, false).OfType<MechModel>().Any(model =>
            Math.Abs(model.Position.X - hill.X) < .3f && Math.Abs(model.Position.Z - hill.Y) < .3f
            && Math.Abs(model.Position.Y - terrain.HeightAt(hill)) < .3f), "rendered mechs stand at the simulation hill elevation");
        await Capture("terrain-elevation");
        // Each biome renders its own features and buildings; restore the battle afterwards.
        var roster = _app.State.Roster!;
        foreach (var biome in Biomes.All.Select(spec => spec.Biome))
        {
            var view = CombatSimulation.Deploy(roster, [roster.Mechs[0].Id], 41, 183, biome: biome);
            tactical.Battlefield.Display(view); await Frames();
            var scene = tactical.Battlefield.FindChildren("Terrain", "Node3D", true, false).OfType<Node3D>().Single();
            // Features are named by kind or style and index.
            int Named(string name) => scene.GetChildren().Count(node => node.Name.ToString().Contains(name));
            Check(Enum.GetNames<BuildingStyle>().Sum(Named) == view.Terrain.Buildings.Count
                && (Named("Trees") == 1) == view.Terrain.Patches.Any(patch => patch.Kind == TerrainKind.Vegetation)
                && Named("Rock") == view.Terrain.Patches.Count(patch => patch.Kind == TerrainKind.Rock)
                && Enum.GetValues<LiquidShape>().All(shape => Named(shape.ToString()) == view.Terrain.Liquids.Count(body => body.Shape == shape))
                && Named("Roads") == (view.Terrain.Roads.Count > 0 ? 1 : 0) && Named("Bridge") == view.Terrain.Bridges.Count,
                $"{biome} renders its buildings, rocks, trees, lakes, rivers, sea, roads and bridges");
            await Capture($"terrain-{biome.ToString().ToLowerInvariant()}");
            await PressKey(Key.Home); await Capture($"terrain-{biome.ToString().ToLowerInvariant()}-overview");
        }
        tactical.Battlefield.Display(sim); await Frames();
    }


    /// <summary>The Maintain Range row: checkbox, label, slider and reading, and the commands they send.</summary>
    private async Task CheckMaintainRangeRow(CombatScreen tactical, CombatSimulation sim)
    {
        var slider = tactical.DistanceSlider; var toggle = tactical.MaintainRangeToggle;
        var row = toggle.GetParent<HBoxContainer>().GetChildren().ToArray();
        Check(row.Length == 4 && row[0] == toggle && row[1] is Label { Text: "Maintain Range" } && row[2] == slider && row[3] == tactical.DistanceValue,
            "the row reads checkbox, Maintain Range, slider, distance");
        Check(slider.MinValue == 200 && slider.MaxValue == 1800 && slider.Step == 100 && !slider.Scrollable,
            "the slider covers the preferred-distance rules in 100 m steps and ignores the mouse wheel");
        Check(!tactical.FindChildren("*", "SpinBox", true, false).Any(), "the tactical view has no text entry to steal hotkeys");
        var units = tactical.Selected.Select(sim.Unit).ToArray();
        var saved = units.Select(unit => (unit.PreferredDistance, unit.MaintainRange)).ToArray();
        foreach (var unit in units) unit.PreferredDistance = 300;
        await Delay(.15);
        Check(toggle.ButtonPressed && tactical.DistanceValue.Text == "300 m" && slider.Value == 300, "the row shows the selection's range");
        Check(!tactical.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.IsVisibleInTree() && label.Text.Contains("Distance:")),
            "the details no longer repeat the distance");
        if (units.Length > 1)
        {
            units[1].PreferredDistance = 700; await Delay(.15);
            Check(tactical.DistanceValue.Text == "Mixed" && slider.Value == units[0].PreferredDistance, "differing distances read Mixed at the primary's value");
        }
        toggle.ButtonPressed = false; await Delay(.15);
        Check(units.All(unit => !unit.MaintainRange) && slider.Modulate.A < 1, "unchecking stops the selection maintaining range and dims the slider");
        slider.EmitSignal(Godot.Range.SignalName.ValueChanged, 500.0);
        Check(units.All(unit => !unit.MaintainRange), "moving the handle alone sends nothing");
        slider.EmitSignal(Slider.SignalName.DragStarted); slider.Value = 500; slider.EmitSignal(Slider.SignalName.DragEnded, true);
        await Delay(.15);
        Check(units.All(unit => unit.MaintainRange && unit.PreferredDistance == 500) && toggle.ButtonPressed && tactical.DistanceValue.Text == "500 m",
            "letting go of the slider sets the range for the group and ticks Maintain Range");
        for (var i = 0; i < units.Length; i++) (units[i].PreferredDistance, units[i].MaintainRange) = saved[i];
    }
}
