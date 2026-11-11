using Godot;

namespace Mech.Tests;

public partial class SmokeTests
{
    private async Task ResumeFromMenu()
    {
        _app.ShowMainMenu(); await Frames();
        Check(_app.MainMenu.Visible && _app.MainMenu.ResumeButton.Visible && !_app.Dialog.Visible,
            "entering menu retains a resumable campaign without a confirmation");
        await Click(_app.MainMenu.ResumeButton);
        Check(!_app.MainMenu.Visible && !_app.IsAdvancing, "Resume leaves campaign time paused");
    }

    private async Task TestMenuResumeUi()
    {
        _app.ShowOffice(); await Frames();
        var state = _app.State;
        _app.StartAdvancing(true);
        _app.ShowMainMenu();
        var date = state.Date;
        await Delay(.15);
        Check(!_app.IsAdvancing && state.Date == date, "campaign time stays frozen in the menu");
        await Click(_app.MainMenu.ResumeButton);
        Check(_app.Office.Visible && ReferenceEquals(state, _app.State) && !_app.IsAdvancing,
            "office Resume retains the live company and pauses its clock");

        _app.ShowMap(); await Frames();
        _app.StarMap.Viewport.ZoomBy(2);
        _app.StarMap.Viewport.Transform.Pan(new Vector2(50, 40));
        _app.StarMap.Search.Text = "Galatea";
        _app.StarMap.Search.GrabFocus();
        var zoom = _app.StarMap.Viewport.Transform.Zoom;
        var offset = _app.StarMap.Viewport.Transform.Offset;
        await ResumeFromMenu();
        Check(_app.StarMap.Visible && !_app.Office.Visible && _app.StarMap.Search.Text == "Galatea"
            && _app.StarMap.Search.HasFocus() && _app.StarMap.Viewport.Transform.Zoom == zoom
            && _app.StarMap.Viewport.Transform.Offset.IsEqualApprox(offset),
            "Resume restores the map view, search and focus");
        _app.StarMap.Search.Text = "";
        _app.StarMap.Viewport.Fit();

        _app.ShowHangar(); await Frames();
        _app.Hangar.Select(2);
        var selected = _app.Hangar.SelectedMech;
        _app.ShowMainMenu(); await Frames();
        await PressKey(Key.Escape);
        Check(_app.Hangar.Visible && !_app.MainMenu.Visible && !_app.Office.Visible && _app.Hangar.SelectedMech == selected,
            "Escape in the main menu restores the hangar mech without rebuilding controls");
        _app.ShowMainMenu(); await Frames();
        await Click(_app.MainMenu.ResumeButton);
        Check(_app.Hangar.Visible && !_app.Office.Visible && _app.Hangar.SelectedMech == selected,
            "Resume restores hangar mech without rebuilding controls");
        _app.ShowWarriors(); await Frames();
        Check(_app.Rolodex.Visible && _app.Hangar.Visible, "personnel opens over hangar before resuming");
        await Click(_app.Rolodex.NextButton);
        var card = _app.Rolodex.CardName.Text;
        await ResumeFromMenu();
        Check(_app.Rolodex.Visible && _app.Hangar.Visible && _app.Rolodex.CardName.Text == card,
            $"Resume restores the personnel card over its original hangar (personnel={_app.Rolodex.Visible}, hangar={_app.Hangar.Visible}, card={_app.Rolodex.CardName.Text == card})");
        await PressKey(Key.Escape);
        Check(!_app.Rolodex.Visible && _app.Hangar.Visible && _app.Hangar.WarriorsButton.HasFocus(),
            "resumed personnel still returns to its hangar parent");

        _app.ShowOffice(); _app.ShowMissions(); await Frames();
        var mission = _app.Missions.MissionName.Text;
        _app.Missions.CloseButton.GrabFocus();
        await ResumeFromMenu();
        Check(_app.Missions.Visible && _app.Office.Visible && _app.Missions.MissionName.Text == mission
            && _app.Missions.CloseButton.HasFocus(), "Resume restores the mission panel and focus");
        await PressKey(Key.Escape);

        // A menu confirmation must not replace the contents of a suspended strategic dialog.
        state.AdvanceOneDay();
        SeedSave(state);
        _app.ShowFinances(); await Frames();
        var heading = _app.Dialog.Heading;
        var close = DialogButton("Close"); close.GrabFocus();
        _app.ShowMainMenu(); await Frames();
        await Click(_app.MainMenu.NewButton);
        Check(_app.UnsavedDialog.Visible && _app.UnsavedDialog.Heading == "Start a new company?",
            "new company warns about unsaved changes");
        await PressKey(Key.Escape);
        await Click(_app.MainMenu.ResumeButton);
        Check(_app.Dialog.Visible && _app.Dialog.Heading == heading && close.HasFocus(),
            "canceling replacement preserves suspended dialog contents and focus");
        GetWindow().EmitSignal(Window.SignalName.CloseRequested); await Frames();
        Check(_app.UnsavedDialog.Visible && _app.Dialog.Visible && _app.UnsavedDialog.Heading == "Quit game?",
            "window close protects unsaved progress over an existing dialog");
        GetWindow().EmitSignal(Window.SignalName.CloseRequested); await Frames();
        await RightClick(DialogButton("Cancel").GetGlobalRect().GetCenter());
        Check(!_app.UnsavedDialog.Visible && _app.Dialog.Visible && close.HasFocus() && ReferenceEquals(state, _app.State),
            "canceling repeated window close preserves the original modal and company");
        await Click(close);
        Check(!_app.Dialog.Visible && _app.Office.SafeButton.HasFocus(), "original modal still closes with its original return focus");
    }

    private async Task SaveMenuTestCompany()
    {
        _app.ShowOffice(); _app.ShowFiling(); await Frames();
        await Click(SlotButton(0)); await Click(DialogButton("Save"));
        Check(_app.Saves.Status.Text.StartsWith("Saved ") && !_app.HasUnsavedChanges,
            "successful manual save clears unsaved changes");
        await Click(_app.Saves.CloseButton);
    }

    private async Task TestUnsavedChangesUi()
    {
        _app.StartNewCompany(); await FinishDraft();
        Check(_app.HasUnsavedChanges, "newly established company is unsaved before its first save");
        await SaveMenuTestCompany();
        await ResumeFromMenu();
        _app.ShowMap(); await Frames(); _app.StarMap.Viewport.ZoomBy(2);
        _app.ShowHangar(); await Frames(); _app.Hangar.Select(1);
        Check(!_app.HasUnsavedChanges, "navigation, zoom and selections do not mark campaign data unsaved");
        _app.ShowOffice(); _app.ShowMissions(); await Frames();
        Check(_app.HasUnsavedChanges, "marking new mission offers seen counts as unsaved campaign data");
        await PressKey(Key.Escape); await SaveMenuTestCompany();

        var roster = _app.State.Roster!;
        var mech = roster.Mechs[0]; var warrior = mech.WarriorId;
        roster.Assign(mech.Id, null);
        Check(_app.HasUnsavedChanges, "assignment changes are detected without UI notifications");
        roster.Assign(mech.Id, warrior);
        Check(!_app.HasUnsavedChanges, "reverting campaign data to its saved values clears unsaved status");
        _app.State.AdvanceOneDay();
        Check(_app.HasUnsavedChanges, "advancing campaign time marks progress unsaved");

        _app.ShowMainMenu(); await Frames();
        var state = _app.State;
        await Click(_app.MainMenu.QuitButton);
        Check(_app.UnsavedDialog.Visible && _app.UnsavedDialog.Heading == "Quit game?"
            && DialogButton("Quit Without Saving").IsVisibleInTree(), "menu Quit offers an explicit discard action");
        await Click(DialogButton("Cancel"));
        Check(_app.MainMenu.Visible && !_app.UnsavedDialog.Visible && _app.HasUnsavedChanges
            && ReferenceEquals(state, _app.State) && _app.MainMenu.QuitButton.HasFocus(),
            "canceling Quit keeps the company, changes and menu focus");
        await Click(_app.MainMenu.LoadButton);
        Check(_app.Saves.Visible && !_app.SaveDialog.Visible && ReferenceEquals(state, _app.State), "menu Load opens the save grid without asking yet");
        await Click(SlotButton(0));
        Check(_app.SaveDialog.Visible && _app.SaveDialog.Body.Contains("unsaved changes"), "dirty menu Load requests confirmation on choosing a save");
        await Click(DialogButton("Cancel"));
        Check(ReferenceEquals(state, _app.State) && _app.HasUnsavedChanges && _app.Saves.Visible && SlotButton(0).HasFocus(),
            "canceling Load retains unsaved progress and returns to the grid");
        await Click(SlotButton(0)); await Click(DialogButton("Load Game"));
        Check(!ReferenceEquals(state, _app.State) && !_app.HasUnsavedChanges && !_app.Saves.Visible && _app.Office.Visible,
            "successful load establishes a clean comparison baseline");
        _app.ShowMainMenu(); await Frames(); await Click(_app.MainMenu.LoadButton); await Click(SlotButton(0));
        Check(_app.Office.Visible && !_app.SaveDialog.Visible && !_app.HasUnsavedChanges, "clean menu Load skips discard confirmation");
        _app.ShowFiling(); await Frames(); await Click(_app.Saves.LoadTab); await Click(SlotButton(0));
        Check(_app.Office.Visible && !_app.Saves.Visible && !_app.SaveDialog.Visible, "clean filing Load skips discard confirmation");

        _app.State.AdvanceOneDay();
        _app.ShowFinances(); await Frames();
        var close = DialogButton("Close");
        var newest = _app.SoloSaves.PathOf(_app.SoloSaves.List()[0].Id);
        var saved = System.IO.File.ReadAllText(newest);
        System.IO.File.WriteAllText(newest, "invalid save");
        _app.ShowMainMenu(); await Frames();
        state = _app.State;
        await Click(_app.MainMenu.LoadButton); await Click(SlotNamed("Unreadable save")); await Click(DialogButton("Load Game"));
        Check(_app.SaveDialog.Heading == "Unable to load" && ReferenceEquals(state, _app.State) && _app.HasUnsavedChanges,
            "failed menu load retains company and dirty baseline");
        await Click(DialogButton("Close")); await Click(_app.Saves.CloseButton);
        Check(_app.MainMenu.Visible && _app.MainMenu.LoadButton.HasFocus(), "closing the grid returns to Load Game");
        await Click(_app.MainMenu.ResumeButton);
        Check(_app.Dialog.Visible && _app.Dialog.Heading == "Quarterly finances" && GodotObject.IsInstanceValid(close),
            "failed menu load also preserves the suspended strategic dialog");
        await Click(close);
        System.IO.File.WriteAllText(newest, saved);

        // Replacing the solo save folder with a file forces a real, isolated storage failure.
        var folder = _app.SoloSaves.Directory; var aside = folder + "-aside";
        System.IO.Directory.Move(folder, aside); System.IO.File.WriteAllText(folder, "");
        try
        {
            _app.ShowFiling(); await Frames(); await Click(SlotButton(0)); await Click(DialogButton("Save"));
            Check(_app.SaveDialog.Heading == "Unable to save" && _app.HasUnsavedChanges, "failed save does not clear unsaved changes");
            await Click(DialogButton("Back"));
            Check(_app.Saves.Visible && !_app.SaveDialog.Visible, "Back from a failed save returns to the grid");
            await Click(_app.Saves.CloseButton);
        }
        finally
        {
            System.IO.File.Delete(folder);
            System.IO.Directory.Move(aside, folder);
        }
        _app.ShowOffice();
        await SaveMenuTestCompany();
        _app.ShowMainMenu(); await Frames(); await Click(_app.MainMenu.NewButton);
        Check(_app.Drafting.Visible && !_app.UnsavedDialog.Visible && !_app.MainMenu.ResumeButton.IsVisibleInTree(),
            "clean New Company skips confirmation and discards the old resume destination");
        await FinishDraft(); await SaveMenuTestCompany();
    }
}
