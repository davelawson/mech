using Godot;

namespace Mech.Tests;

public partial class SmokeTests
{
    private void CheckRoute(CommandScreen current, params CommandScreen[] visible)
    {
        var surfaces = new Dictionary<CommandScreen, Control>
        {
            [CommandScreen.Office] = _app.Office, [CommandScreen.Map] = _app.StarMap,
            [CommandScreen.Hangar] = _app.Hangar, [CommandScreen.MainMenu] = _app.MainMenu,
            [CommandScreen.Draft] = _app.Drafting, [CommandScreen.Tactical] = _app.Tactical,
            [CommandScreen.Rolodex] = _app.Rolodex, [CommandScreen.Missions] = _app.Missions,
            [CommandScreen.Dialog] = _app.Dialog, [CommandScreen.UnsavedDialog] = _app.UnsavedDialog
        };
        Check(_app.Navigation.CurrentScreen == current
            && surfaces.All(item => item.Value.Visible == visible.Contains(item.Key)),
            $"explicit {current} route projects exactly these surfaces: {string.Join(", ", visible)}");
    }

    private async Task NavigationBack(bool rightClick, Control target)
    {
        if (rightClick) await RightClick(target.GetGlobalRect().GetCenter());
        else await PressKey(Key.Escape);
    }

    private async Task TestNavigationTransitions()
    {
        _app.ShowOffice(); await Frames();
        // Exercise the same nested route using both gestures, including a native popup window.
        foreach (var rightClick in new[] { false, true })
        {
            _app.ShowMap(); await Frames(); _app.StarMap.Search.GrabFocus();
            CheckRoute(CommandScreen.Map, CommandScreen.Map);
            await NavigationBack(rightClick, _app.StarMap.Search);
            CheckRoute(CommandScreen.Office, CommandScreen.Office);
            Check(_app.Office.Hotspot.HasFocus(), "both back gestures leave map search and restore chart focus");

            _app.ShowHangar(); await Frames(); _app.Hangar.Select(2);
            var selected = _app.Hangar.SelectedMech;
            var tabs = _app.Hangar.FindChildren("*", "TabContainer", true, false).OfType<TabContainer>().Single();
            tabs.CurrentTab = 2;
            _app.ShowWarriors(); await Frames();
            CheckRoute(CommandScreen.Rolodex, CommandScreen.Hangar, CommandScreen.Rolodex);
            await Click(_app.Rolodex.Section);
            Check(_app.Rolodex.Section.GetPopup().Visible, "personnel section popup opens above the route");
            await NavigationBack(rightClick, _app.Rolodex.Section);
            Check(!_app.Rolodex.Section.GetPopup().Visible && _app.Rolodex.Section.HasFocus(),
                "popup back restores its owner without closing personnel");
            CheckRoute(CommandScreen.Rolodex, CommandScreen.Hangar, CommandScreen.Rolodex);

            _app.ShowMainMenu(); _app.ShowMainMenu(); await Frames();
            CheckRoute(CommandScreen.MainMenu, CommandScreen.MainMenu);
            await RightClick(new Vector2(20, 20));
            await Click(_app.MainMenu.ResumeButton);
            CheckRoute(CommandScreen.Rolodex, CommandScreen.Hangar, CommandScreen.Rolodex);
            await NavigationBack(rightClick, _app.Rolodex.BackButton);
            CheckRoute(CommandScreen.Hangar, CommandScreen.Hangar);
            Check(_app.Hangar.SelectedMech == selected && tabs.CurrentTab == 2 && _app.Hangar.WarriorsButton.HasFocus(),
                "repeated menu entry preserves the nested route, hangar selection, tab and return focus");
            await NavigationBack(rightClick, _app.Hangar.BackButton);
            CheckRoute(CommandScreen.Office, CommandScreen.Office);
            Check(_app.Office.HangarButton.HasFocus(), "nested route ultimately returns to the office hangar hotspot");
        }

        // Queue several transitions before deferred focus runs. Only the last destination may win.
        _app.ShowMap(); _app.ShowMainMenu(); _app.ShowOffice(); await Frames();
        CheckRoute(CommandScreen.Office, CommandScreen.Office);
        Check(_app.Office.Hotspot.HasFocus(), "stale deferred navigation focus cannot override the latest route");

        _app.ShowMap(); await Frames();
        var temporaryFocus = new Button { Text = "Temporary search action" };
        _app.StarMap.AddChild(temporaryFocus); temporaryFocus.GrabFocus();
        _app.ShowMainMenu(); temporaryFocus.QueueFree(); await Frames();
        await Click(_app.MainMenu.ResumeButton);
        CheckRoute(CommandScreen.Map, CommandScreen.Map);
        Check(_app.StarMap.Viewport.HasFocus(), "Resume falls back to the screen focus when its captured control was freed");
        await Click(_app.StarMap.Destinations);
        Check(_app.StarMap.Destinations.GetPopup().Visible, "map destination popup opens before suspension");
        _app.ShowMainMenu(); await Frames(); await Click(_app.MainMenu.ResumeButton);
        Check(!_app.StarMap.Destinations.GetPopup().Visible, "menu suspension dismisses native popups before restoring a route");

        // Keep an actual confirmation above a panel while a second modal and the menu come and go.
        _app.StartNewCompany(); await FinishDraft();
        _app.ShowMissions(); await Frames(); await Click(_app.Missions.AcceptButton);
        _app.ShowMissions(); await Frames(); await Click(_app.Missions.AbandonButton);
        var cancel = DialogButton("Cancel"); cancel.GrabFocus();
        CheckRoute(CommandScreen.Dialog, CommandScreen.Office, CommandScreen.Missions, CommandScreen.Dialog);
        _app.RequestQuit(); await Frames();
        CheckRoute(CommandScreen.UnsavedDialog, CommandScreen.Office, CommandScreen.Missions, CommandScreen.Dialog, CommandScreen.UnsavedDialog);
        await RightClick(DialogButton("Cancel").GetGlobalRect().GetCenter());
        Check(cancel.HasFocus() && _app.State.Missions.Current is not null, "unsaved back restores the original confirmation and keeps the mission");
        _app.ShowMainMenu(); await Frames(); await Click(_app.MainMenu.ResumeButton);
        CheckRoute(CommandScreen.Dialog, CommandScreen.Office, CommandScreen.Missions, CommandScreen.Dialog);
        Check(cancel.HasFocus() && _app.Dialog.Heading == "Abandon mission?", "Resume retains a confirmation above its mission panel");
        await PressKey(Key.Escape);
        CheckRoute(CommandScreen.Missions, CommandScreen.Office, CommandScreen.Missions);
        Check(_app.Missions.AbandonButton.HasFocus() && _app.State.Missions.Current is not null,
            "back from the resumed confirmation restores the mission action without abandoning");
        _app.StartAdvancing(true);
        Check(!_app.IsAdvancing, "an explicit modal route blocks campaign time even with the office visible");
        await PressKey(Key.Escape);

        await SaveMenuTestCompany();
        var saved = System.IO.File.ReadAllText(_saveFile);
        _app.StartNewCompany(); await Frames();
        CheckRoute(CommandScreen.Draft, CommandScreen.Draft);
        var draft = _app.Drafting.Draft;
        var inspect = _app.Drafting.FindChildren("*", "Button", true, false).OfType<Button>().First(button => button.Text == "…");
        await Click(inspect); await PressKey(Key.Escape);
        CheckRoute(CommandScreen.Draft, CommandScreen.Draft);
        Check(inspect.HasFocus(), "draft inspection back stays inside the draft and restores the inspected action");
        _app.Drafting.NameInput.GrabFocus(); await PressKey(Key.Escape);
        CheckRoute(CommandScreen.Dialog, CommandScreen.Draft, CommandScreen.Dialog);
        await RightClick(DialogButton("Cancel").GetGlobalRect().GetCenter());
        Check(ReferenceEquals(draft, _app.Drafting.Draft) && _app.Drafting.NameInput.HasFocus(),
            "canceling abandonment from the name field retains the draft and its focus");
        await PressKey(Key.Escape); await Click(DialogButton("Abandon draft"));
        CheckRoute(CommandScreen.MainMenu, CommandScreen.MainMenu);
        Check(!_app.Navigation.CanResume && !_app.MainMenu.ResumeButton.Visible && System.IO.File.ReadAllText(_saveFile) == saved,
            "abandoning a draft clears its route without creating Resume or changing the manual save");
        await Click(_app.MainMenu.NewButton); await FinishDraft();

        _app.ShowHangar(); await Frames(); _app.Hangar.Select(1);
        var selectedMech = _app.Hangar.SelectedMech;
        _app.ShowDeployment(); await Frames();
        CheckRoute(CommandScreen.Tactical, CommandScreen.Tactical);
        await RightClick(_app.Tactical.RandomizedEnemyButton.GetGlobalRect().GetCenter());
        CheckRoute(CommandScreen.Tactical, CommandScreen.Tactical);
        Check(_app.Tactical.Modal == CombatModal.OpponentSelection, "tactical right-click bypasses strategic back even during deployment");
        await PressKey(Key.Escape);
        CheckRoute(CommandScreen.Hangar, CommandScreen.Hangar);
        Check(_app.Hangar.SelectedMech == selectedMech && _app.Hangar.DeploymentButton.HasFocus(),
            "tactical exit restores its original hangar and deployment focus");
        _app.ShowDeployment(); _app.ShowMainMenu(); await Frames();
        Check(!_app.Navigation.CanResume && !_app.MainMenu.ResumeButton.Visible, "tactical routes are excluded from strategic Resume");
        _app.ShowOffice(); await Frames();
    }
}
