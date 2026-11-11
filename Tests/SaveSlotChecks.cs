using Godot;

namespace Mech.Tests;

/// <summary>The save grid from the filing cabinet: the empty slot, the name prompt, what a slot shows, and paging.</summary>
public partial class SmokeTests
{
    /// <summary>Leaves one solo save named "Payment due" of the current company, with no unsaved changes.</summary>
    private async Task TestSaveGridUi()
    {
        var company = _app.State.Roster!.ForceName;
        await Click(_app.Office.FilingButton);
        Check(_app.Saves.Visible && _app.Saves.Saving && _app.Saves.Heading.Text == "SAVE GAME" && _app.Saves.SaveTab.IsVisibleInTree()
            && SlotName(SlotButton(0)) == "Empty slot" && SlotButton(1).Disabled && !_app.Saves.PreviousButton.IsVisibleInTree(),
            "the cabinet opens the save grid with the empty slot first");
        await Capture("save-grid-empty");
        await Click(SlotButton(0));
        var input = _app.SaveDialog.NameInput!;
        Check(_app.SaveDialog.Visible && _app.SaveDialog.Heading == "Save game" && input.Text == company && input.HasFocus(),
            "the name prompt starts with the company name");
        input.Text = "  "; input.EmitSignal(LineEdit.SignalName.TextChanged, "  ");
        Check(DialogButton("Save").Disabled, "a blank name cannot be saved");
        await Click(DialogButton("Cancel"));
        Check(!_app.SaveDialog.Visible && _app.Saves.Visible && SaveFingerprint() == "", "Cancel writes nothing");
        await Click(SlotButton(0));
        _app.SaveDialog.NameInput!.Text = "Payment due";
        await Click(DialogButton("Save"));
        Check(_app.SoloSaves.List() is [{ Info: { Name: "Payment due" } info }] && info.Date == _app.State.Date && info.Cash == _app.State.Cash
            && !_app.HasUnsavedChanges && _app.CoopSaves.List().Count == 0, "saving writes a named solo slot");
        Check(SlotName(SlotButton(1)) == "Payment due" && SlotButton(1).GetNode<Label>("Lines/SlotDate").Text == _app.State.DateText
            && SlotButton(1).GetNode<Label>("Lines/SlotCash").Text == HangarScreen.Money(_app.State.Cash) && SlotName(SlotButton(0)) == "Empty slot",
            "the new save follows the empty slot and shows its name, in-game date and balance");
        await Capture("save-grid");
        await Click(_app.Saves.CloseButton);

        var seeded = Enumerable.Range(1, 11).Select(n => SeedSave(_app.State, $"Extra {n}")).ToList();
        await Click(_app.Office.FilingButton);
        Check(_app.Saves.PageCount == 2 && _app.Saves.PreviousButton.IsVisibleInTree() && _app.Saves.PageLabel.Text == "Page 1 of 2"
            && _app.Saves.PreviousButton.Disabled && !_app.Saves.NextButton.Disabled
            && SlotName(SlotButton(1)) == "Extra 11" && SlotName(SlotButton(8)) == "Extra 4", "a full grid pages, newest first, after the empty slot");
        await Capture("save-grid-paged");
        await Click(_app.Saves.NextButton);
        Check(_app.Saves.Page == 1 && _app.Saves.PageLabel.Text == "Page 2 of 2" && _app.Saves.NextButton.Disabled && !_app.Saves.PreviousButton.Disabled
            && SlotName(SlotButton(0)) == "Extra 3" && SlotName(SlotButton(3)) == "Payment due" && SlotButton(4).Disabled,
            "Next shows the oldest saves on the last page");
        await Click(_app.Saves.PreviousButton);
        Check(_app.Saves.Page == 0 && SlotName(SlotButton(1)) == "Extra 11", "Previous returns to the newest saves");
        await Click(_app.Saves.LoadTab);
        Check(!_app.Saves.Saving && _app.Saves.Heading.Text == "LOAD GAME" && _app.Saves.PageCount == 2 && SlotName(SlotButton(0)) == "Extra 11",
            "the Load tab lists the saves without an empty slot");
        await Click(_app.Saves.NextButton);
        Check(SlotName(SlotButton(2)) == "Payment due" && SlotButton(3).Disabled, "the last load page holds the remainder");
        await PressKey(Key.Escape);
        Check(!_app.Saves.Visible && _app.Office.FilingButton.HasFocus(), "Escape closes the save grid and restores cabinet focus");
        foreach (var id in seeded) System.IO.File.Delete(_app.SoloSaves.PathOf(id));
    }
}
