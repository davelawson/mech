using Godot;

namespace Mech.Tests;

/// <summary>Co-op over real ENet on localhost, in one process: the app leads while an engine-free client takes the other seat, then
/// the app joins an engine-free leader.</summary>
public partial class SmokeTests
{
    private static int FreePort() => 20000 + Random.Shared.Next(30000);

    /// <summary>Lets the app and the test's own session exchange packets until a condition holds.</summary>
    private async Task Exchange(Func<bool> until, string message, CoopHost? host = null, CoopClient? client = null)
    {
        for (var i = 0; i < 300 && !until(); i++)
        {
            host?.Update(CoopHost.SyncInterval); client?.Update();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        Check(until(), message);
    }

    /// <summary>Stands in for the router's forwarding of the co-op port.</summary>
    private sealed class FakeRouterPort : IDisposable
    {
        public bool Closed { get; private set; }
        public void Dispose() => Closed = true;
    }

    private async Task TestHostingUi()
    {
        _app.PublicAddressLookup = () => Task.FromResult<string?>("203.0.113.7");
        var router = new FakeRouterPort(); int? forwarded = null;
        _app.RouterPortOpener = port => { forwarded = port; return Task.FromResult<IDisposable?>(router); };
        await Click(_app.MainMenu.HostButton);
        var lobby = _app.Lobby;
        await Frames();
        Check(lobby.AddressLabel.IsVisibleInTree() && lobby.AddressLabel.Text.StartsWith($"Internet: 203.0.113.7:{NetProtocol.DefaultPort}") && !lobby.CopyAddressButton.Disabled,
            "the host form shows the internet address the router reports");
        Check(lobby.Visible && lobby.Hosting && !lobby.SeatsShown && lobby.ConfirmButton.Disabled, "Host Co-op opens the hosting form, which needs a name");
        Check(lobby.PlayersPicker.IsItemDisabled(1) && lobby.PlayersPicker.GetItemTooltip(1).Contains("27 regular"),
            "three players wait for enough MechWarriors in the catalog");
        Check(lobby.AddressLabel.Text.EndsWith($"UDP port {NetProtocol.DefaultPort} is opened on your router when you host, if it supports UPnP.") && forwarded is null,
            "the host form says the port is opened on the router once hosting");
        var port = FreePort();
        lobby.NameInput.Text = "Dave"; lobby.NameInput.EmitSignal(LineEdit.SignalName.TextChanged, "Dave"); lobby.PortInput.Value = port;
        await Click(lobby.ConfirmButton);
        Check(_app.Host is { Players: 2 } && lobby.SeatsShown && lobby.StartButton.Disabled && lobby.SeatLabels[1].Text.Contains("Open"),
            "the leader's lobby waits for the second player");
        Check(lobby.AddressLabel.IsVisibleInTree() && lobby.AddressLabel.Text.Contains($"203.0.113.7:{port}"), "the leader's lobby shows the address to join at, with the chosen port");
        await Frames();
        Check(forwarded == port && lobby.AddressLabel.Text.EndsWith($"UDP port {port} is open on your router."), "hosting opens the chosen port on the router");

        var sam = new CoopClient(ENetTransport.Connect("127.0.0.1", port), "Sam");
        try
        {
            await Exchange(() => sam.Seats.Count == 2, "a player connects to the leader's lobby", client: sam);
            sam.Claim(1);
            await Exchange(() => !lobby.StartButton.Disabled, "the leader can start once the seat is taken", client: sam);
            Check(lobby.SeatLabels[1].Text.Contains("Sam"), "the lobby names the seated player");
            await Click(lobby.StartButton);
            Check(_app.Drafting.Visible && _app.Drafting.NameInput.Visible && _app.Drafting.EstablishButton.Text == "Confirm Lance", "the leader drafts and names the company");
            await Exchange(() => sam.Draft is not null, "the other player receives their own draft", client: sam);
            await FinishDraft();
            Check(_app.Drafting.Waiting && _app.Host!.Campaign is null, "the leader waits for the other player's draft");
            sam.SubmitDraft();
            await Exchange(() => _app.Office.Visible && sam.Campaign is not null, "the company forms once every draft is in", client: sam);
            var roster = _app.State.Roster!;
            Check(_app.State.Players == 2 && roster.LanceMechs(0).Count() == 4 && roster.LanceMechs(1).Count() == 4 && _app.State.PlayerNames![1] == "Sam",
                "the co-op company has a lance for each player");

            var samMech = roster.LanceMechs(1).First();
            sam.Send(new SetLanceCommand(samMech.Id, null));
            await Exchange(() => samMech.Lance is null, "the other player's command changes the leader's campaign", client: sam);
            await Exchange(() => sam.Campaign!.Roster!.Mechs.Single(mech => mech.Id == samMech.Id).Lance is null, "the change reaches the other player", client: sam);
            string? refused = null; sam.CommandRefused += reason => refused = reason;
            sam.Send(new AdvanceDayCommand());
            await Exchange(() => refused is not null, "the leader refuses the other player's leader-only command", client: sam);

            await Click(_app.Office.HangarButton);
            Hangar(samMech);
            Check(_app.Hangar.LanceText.Text == "Lance pool" && _app.Hangar.LanceButton.Text == "Add to Lance 1", "the leader may take a lance pool mech");
            Hangar(roster.LanceMechs(1).First()); await Frames();
            Check(_app.Hangar.LanceText.Text.StartsWith("Lance 2 · Sam") && _app.Hangar.LanceButton.Disabled && _app.Hangar.ApplyButton.Disabled && _app.Hangar.PilotPicker.Disabled,
                "the leader cannot change another player's lance");
            Check(_app.Hangar.QuickLaunchButton.Disabled && _app.Hangar.QuickLaunchButton.TooltipText == "Accept a mission from the office phone first.",
                "the leader launches only an accepted mission");
            await Click(_app.Hangar.BackButton);
            await LeadMission(sam);

            sam.Dispose();
            await Exchange(() => _app.Dialog.Visible && _app.Dialog.Heading == "Player disconnected" && !_app.Host!.AllPresent, "the leader is told a player disconnected");
            await Click(DialogButton("Continue"));
            _app.StartAdvancing(false); await Frames();
            Check(!_app.IsAdvancing && _app.Dialog.Heading == "Time cannot advance" && _app.Dialog.Visible, "time cannot advance while a player is away");
            await Click(DialogButton("Close"));

            var back = new CoopClient(ENetTransport.Connect("127.0.0.1", port), "Sam");
            try
            {
                await Exchange(() => back.Seats.Count == 2, "the player reconnects", client: back);
                back.Claim(1);
                await Exchange(() => back.Campaign is not null && _app.Host!.AllPresent, "the player retakes their seat and receives the campaign", client: back);
                await Click(DialogButton("Continue"));
                _app.StartAdvancing(false);
                Check(_app.IsAdvancing, "time advances once every player is back");
                _app.StopAdvancing();
                await StopWaiting(back, port);
                Check(router.Closed, "the router's forwarding is removed when the co-op game ends");
            }
            finally { back.Dispose(); }
        }
        finally { sam.Dispose(); }
        void Hangar(OwnedMech mech) => _app.Hangar.Select(_app.State.Roster!.Mechs.ToList().IndexOf(mech));
    }

    /// <summary>A player drops out of the app's co-op battle: everyone waits until they rejoin, and when they drop again the leader stops
    /// waiting, ending the game. The campaign keeps the save made when the mission launched.</summary>
    private async Task StopWaiting(CoopClient sam, int port)
    {
        if (_app.Dialog.Visible) await Click(DialogButton("Continue"));
        var state = _app.State; var host = _app.Host!;
        state.AcceptMission(state.Missions.Available.First(item => state.DeadlineDays(item) is not null).Id); host.MarkDirty();
        await Click(_app.Office.HangarButton);
        _app.Hangar.Select(state.Roster!.Mechs.ToList().FindIndex(mech => mech.Lance == 0)); await Frames();
        await Click(_app.Hangar.QuickLaunchButton);
        var tactical = _app.Tactical;
        await Exchange(() => sam.Deployment is not null, "the other player is pulled into force selection", client: sam);
        sam.ChooseDeployment(state.Roster.LanceMechs(1).Select(mech => mech.Id).Take(1), true);
        await Exchange(() => !tactical.LaunchButton.Disabled, "the leader can launch once the other player is ready", client: sam);
        await Click(tactical.LaunchButton);
        Check(!tactical.WaitingBanner.Visible, "nobody is waited for while every player is present");
        sam.Dispose();
        await Exchange(() => tactical.WaitingBanner.Visible, "the battle shows who it is waiting for");
        Check(tactical.WaitingText.Text == "Waiting for Sam to reconnect…" && tactical.StopWaitingButton.Visible && tactical.Simulation!.Paused,
            "the battle waits for the disconnected player, and the leader may stop waiting");
        var again = new CoopClient(ENetTransport.Connect("127.0.0.1", port), "Sam");
        try
        {
            await Exchange(() => again.Seats.Count == 2, "the player reconnects during the battle", client: again);
            again.Claim(1);
            await Exchange(() => again.Battle is not null && !tactical.WaitingBanner.Visible, "the returning player rejoins the battle and the banner clears", client: again);
        }
        finally { again.Dispose(); }
        await Exchange(() => tactical.WaitingBanner.Visible, "the battle waits again when the player drops");
        await Click(tactical.StopWaitingButton);
        Check(_app.Dialog.Visible && _app.Dialog.Heading == "Stop waiting?", "stopping the wait asks first");
        await Click(DialogButton("Stop Waiting"));
        Check(_app.MainMenu.Visible && !_app.InSession && _app.UnsavedDialog.Visible && _app.UnsavedDialog.Body.Contains("You stopped waiting for Sam"),
            "stopping the wait ends the co-op game for the leader");
        Check(new CampaignStore(ProjectSettings.GlobalizePath(_app.SavePath)).Load() is { Missions.Current: not null, PlayerNames.Count: 2 },
            "the campaign keeps the save made when the mission launched, to fight it again");
        await Click(DialogButton("Close"));
    }

    /// <summary>The app leads a co-op mission: it opens force selection for everyone, launches once the other player is ready,
    /// and settles the battle for both.</summary>
    private async Task LeadMission(CoopClient sam)
    {
        var state = _app.State; var host = _app.Host!;
        state.AcceptMission(state.Missions.Available.First(item => state.DeadlineDays(item) is not null).Id); host.MarkDirty();
        await Click(_app.Office.HangarButton);
        _app.Hangar.Select(_app.State.Roster!.Mechs.ToList().FindIndex(mech => mech.Lance == 0)); await Frames();
        Check(!_app.Hangar.QuickLaunchButton.Disabled, "the leader can launch the accepted mission");
        await Click(_app.Hangar.QuickLaunchButton);
        var tactical = _app.Tactical;
        Check(tactical.Visible && tactical.Modal == CombatModal.ForceSelection && tactical.Coop && tactical.Leading && tactical.LaunchButton.Disabled
            && tactical.ForceChoices.Count(choice => !choice.Disabled) == 4, "the leader chooses from their own lance and waits for the other player");
        await Exchange(() => sam.Deployment is { Count: 2 }, "the other player is pulled into force selection", client: sam);
        sam.ChooseDeployment(state.Roster!.LanceMechs(1).Select(mech => mech.Id).Take(2), true);
        await Exchange(() => !tactical.LaunchButton.Disabled, "the leader can launch once the other player is ready", client: sam);
        Check(tactical.DeploymentStatus.Text.Contains("Sam · Ready · 2 mechs"), "force selection shows every player's readiness");
        await Click(tactical.LaunchButton);
        var battle = tactical.Simulation!;
        Check(tactical.Modal == CombatModal.None && !battle.Replica && battle.Units.Count(unit => unit.Lance == 1) == 2
            && tactical.UnitButtons.Count == battle.Units.Count(unit => unit.Lance == 0), "the leader fights with the whole company, commanding their own lance");
        Check(!_app.HasUnsavedChanges, "the campaign is saved as the mission starts");
        await Exchange(() => sam.Battle is not null, "the other player receives the battle", client: sam);
        tactical.SelectUnits(battle.Units.Where(unit => unit.Lance == 1).Select(unit => unit.Id).ToArray(), false);
        Check(tactical.Selected.Count == 0, "the leader cannot select another player's mechs");
        tactical.TogglePause(); await Frames();
        Check(battle.Paused && tactical.PauseButton.Text.StartsWith("Pause"), "the leader's toggle is off while the other player's holds the battle");
        sam.Send(new PauseCommand(false));
        await Exchange(() => !battle.Paused, "the battle runs once both toggles are off", client: sam);
        await Exchange(() => sam.Battle!.Elapsed > 0, "the running battle reaches the other player", client: sam);
        sam.Ping(new Vector2(0, 100));
        await Exchange(() => tactical.Battlefield.Pings.Any(ping => ping.Label == "Sam"), "the other player's ping appears on the leader's battlefield", client: sam);
        int? pinged = null; sam.Pinged += (slot, _) => pinged = slot;
        tactical.Battlefield.GrabFocus(); await PressKey(Key.V);
        await Exchange(() => pinged == 0, "V pings the battlefield for every player", client: sam);
        tactical.OpenMenu(); await Frames();
        Check(!tactical.EndButton.Disabled, "the leader may withdraw");
        await Click(tactical.EndButton);
        await Exchange(() => sam.Settlement is not null && sam.Battle!.Outcome == CombatOutcome.Ended, "the other player sees the leader's settlement", client: sam);
        Check(tactical.Modal == CombatModal.Results && _app.State.Missions.Current is null && !_app.HasUnsavedChanges, "the mission is settled and saved as it ends");
        await Click(tactical.ReturnButton);
        await Exchange(() => sam.Battle is null && _app.Hangar.Visible, "leaving the results returns every player to the hangar", client: sam);
        await Click(_app.Hangar.BackButton);
    }

    /// <summary>The app joins the leader's co-op mission: it is pulled into force selection, marks its force ready, and orders its own
    /// lance in the leader's battle until the leader closes the results.</summary>
    private async Task JoinMission(CoopHost host)
    {
        var campaign = host.Campaign!;
        campaign.AcceptMission(campaign.Missions.Available.First(item => campaign.DeadlineDays(item) is not null).Id); host.MarkDirty();
        await Exchange(() => _app.State.Missions.Current is not null, "the joined player sees the accepted mission", host);
        Check(_app.Hangar.QuickLaunchBlock == "Only the leader launches missions.", "only the leader launches missions");
        host.OpenDeployment();
        var tactical = _app.Tactical;
        await Exchange(() => tactical.Visible && tactical.Modal == CombatModal.ForceSelection, "the joined player is pulled into force selection", host);
        Check(tactical.Coop && !tactical.Leading && !tactical.BackButton.Visible && !tactical.TerrainSeedInput.IsVisibleInTree() && tactical.LaunchButton.Text == "Ready",
            "the joined player marks their force ready; the leader chooses the terrain and closes force selection");
        await Click(tactical.LaunchButton);
        await Exchange(() => host.OthersReady, "the joined player's force reaches the leader", host);
        Check(tactical.LaunchButton.Text == "Not Ready" && tactical.ForceChoices.All(choice => choice.Disabled), "a ready force is locked until it is unmarked");
        var mine = host.Deployment![1].Mechs;
        var battle = host.Launch(campaign.Roster!.LanceMechs(0).Take(1).Select(mech => mech.Id).ToArray(), 4242);
        await Exchange(() => tactical.Simulation is { Replica: true } && tactical.Modal == CombatModal.None, "the joined player sees the leader's battle", host);
        Check(tactical.UnitButtons.Count == mine.Length && tactical.Selected.All(mine.Contains) && tactical.Simulation!.Terrain.Seed == 4242,
            "the joined player commands their own lance on the leader's terrain");
        tactical.TogglePause();
        await Exchange(() => !battle.PauseHolds.Contains(1), "the joined player's pause toggle reaches the leader", host);
        tactical.IssueCommand(new Vector2(0, 0), null);
        await Exchange(() => mine.All(id => battle.Unit(id).Order == CombatOrder.Move), "the joined player's orders reach the leader's battle", host);
        await Exchange(() => tactical.Simulation!.Units.Where(unit => mine.Contains(unit.Id)).All(unit => unit.Order == CombatOrder.Move), "the joined player sees their orders carried out", host);
        tactical.OpenMenu(); await Frames();
        Check(tactical.EndButton.Disabled, "only the leader withdraws"); tactical.Resume(); await Frames();
        battle.End(); host.FinishBattle("Withdrew");
        await Exchange(() => tactical.Modal == CombatModal.Results, "the joined player sees the battle's results", host);
        Check(tactical.ReturnButton.Disabled && tactical.ReturnButton.Text == "Waiting for leader", "only the leader leaves the results");
        await PressKey(Key.Escape);
        Check(tactical.Visible, "the joined player stays on the results until the leader leaves them");
        host.CloseBattle();
        await Exchange(() => !tactical.Visible && _app.Hangar.Visible, "the joined player returns to the hangar with the leader", host);
        campaign.Missions.Abandon(); host.MarkDirty();
        await Click(_app.Hangar.BackButton);
    }

    /// <summary>The leader leaves while the router is still asked to forward the port, then hosts with a router that refuses.</summary>
    private async Task TestRouterPortFailures()
    {
        var lobby = _app.Lobby;
        async Task HostLobby()
        {
            await Click(_app.MainMenu.HostButton); await Frames();
            lobby.NameInput.Text = "Dave"; lobby.NameInput.EmitSignal(LineEdit.SignalName.TextChanged, "Dave"); lobby.PortInput.Value = FreePort();
            await Click(lobby.ConfirmButton); await Frames();
        }
        var asking = new TaskCompletionSource<IDisposable?>();
        _app.RouterPortOpener = _ => asking.Task;
        await HostLobby();
        Check(lobby.SeatsShown && lobby.AddressLabel.Text.EndsWith($"Opening UDP port {lobby.PortInput.Value:0} on your router…"), "the lobby says while the port is being opened");
        await Click(lobby.BackButton);
        var late = new FakeRouterPort(); asking.SetResult(late); await Frames();
        Check(!_app.InSession && late.Closed, "a port the router opens after the leader left is closed again");

        _app.RouterPortOpener = _ => Task.FromResult<IDisposable?>(null);
        await HostLobby();
        Check(lobby.AddressLabel.Text.EndsWith($"Your router did not open UDP port {lobby.PortInput.Value:0} (UPnP may be off, or the port is forwarded to another computer). Forward it on the router for internet play."),
            "the lobby says when the router does not open the port");
        await Click(lobby.BackButton);
        Check(_app.MainMenu.Visible && !_app.InSession, "the leader leaves the lobby");
    }

    private async Task TestJoiningUi()
    {
        _app.PublicAddressLookup = () => Task.FromResult<string?>(null);
        await Click(_app.MainMenu.HostButton); await Frames();
        Check(_app.Lobby.AddressLabel.Text.Contains("your router did not say") && _app.Lobby.CopyAddressButton.Disabled,
            "the host form says when the router does not report the internet address");
        await Click(_app.Lobby.BackButton);
        await TestRouterPortFailures();
        var port = FreePort();
        using var host = new CoopHost(ENetTransport.Host(port, 1), "Dave", 2);
        await Click(_app.MainMenu.JoinButton);
        var lobby = _app.Lobby;
        Check(lobby.Visible && !lobby.Hosting && lobby.AddressInput.IsVisibleInTree(), "Join Co-op asks for the leader's address");
        lobby.NameInput.Text = "Sam"; lobby.NameInput.EmitSignal(LineEdit.SignalName.TextChanged, "Sam"); lobby.PortInput.Value = port;
        await Click(lobby.ConfirmButton);
        await Exchange(() => lobby.ClaimButtons.Count == 2 && lobby.ClaimButtons[1].Visible, "the joined player sees the free seat", host);
        Check(!lobby.ClaimButtons[0].Visible && !lobby.StartButton.Visible, "only the leader starts, and the leader's seat is taken");
        await Click(lobby.ClaimButtons[1]);
        await Exchange(() => host.CanStart && _app.LocalSlot == 1, "the joined player takes the seat", host);
        host.Start(new Random(9));
        await Exchange(() => _app.Drafting.Visible, "the joined player drafts their lance", host);
        Check(!_app.Drafting.NameInput.Visible, "only the leader names the company");
        for (var pool = 0; pool < 8; pool++) await Click(_app.Drafting.Choices[pool, 1]);
        await Click(_app.Drafting.EstablishButton);
        await Exchange(() => host.Seats[1].Drafted, "the joined player's draft reaches the leader", host);
        Check(_app.Drafting.Waiting, "the joined player waits for the leader's draft");
        host.LeaderDraft!.ForceName = "Joined Company"; host.SubmitLeaderDraft();
        await Exchange(() => _app.Office.Visible, "the company forms and the joined player reaches the office", host);
        var campaign = host.Campaign!;
        Check(!_app.IsLeader && _app.State.Roster!.ForceName == "Joined Company" && !_app.HasUnsavedChanges, "the joined player holds the leader's campaign");
        Check(campaign.Roster!.LanceMechs(1).Select(mech => mech.Definition.Id).SequenceEqual(Enumerable.Range(0, 4).Select(pool => _app.Drafting.Draft.MechCandidate(pool, 1).Id)),
            "the joined player's lance is the one they drafted");

        _app.StartAdvancing(false); await Frames();
        Check(!_app.IsAdvancing && _app.Dialog.Heading == "Time cannot advance", "only the leader advances time"); await Click(DialogButton("Close"));
        _app.ShowFiling(); await Frames();
        Check(_app.Dialog.Body.Contains("Only the leader saves"), "only the leader saves and loads"); await Click(DialogButton("Close"));
        await Click(_app.Office.PhoneButton);
        Check(_app.Missions.AcceptButton.Disabled && _app.Missions.AcceptButton.TooltipText == "Only the leader accepts missions.", "only the leader accepts missions");
        await PressKey(Key.Escape);

        await Click(_app.Office.HangarButton);
        var mine = campaign.Roster.LanceMechs(1).First();
        _app.Hangar.Select(_app.State.Roster!.Mechs.ToList().FindIndex(mech => mech.Id == mine.Id)); await Frames();
        Check(!_app.Hangar.LanceButton.Disabled && _app.Hangar.LanceText.Text.StartsWith("Lance 2 · Sam"), "the joined player manages their own lance");
        await Click(_app.Hangar.LanceButton);
        await Exchange(() => mine.Lance is null && _app.Hangar.LanceText.Text == "Lance pool", "the joined player's change goes through the leader and back", host);
        _app.Hangar.Select(0); await Frames();
        Check(_app.Hangar.LanceButton.Disabled && _app.Hangar.InstallProblem("weapon:medium-laser", MechLocation.LeftArm) == "Another player's lance",
            "the joined player cannot change the leader's lance");
        await Click(_app.Hangar.BackButton);

        while (campaign.Date < new DateOnly(3025, 4, 1)) campaign.AdvanceOneDay();
        host.MarkDirty();
        await Exchange(() => _app.Dialog.Visible && _app.Dialog.Heading == "Quarterly finances", "the joined player sees the quarterly bill", host);
        Check(DialogButton("Waiting for leader").Disabled, "only the leader settles the bill");
        campaign.PayNow(); host.MarkDirty();
        await Exchange(() => !_app.Dialog.Visible, "the bill closes once the leader pays", host);
        await JoinMission(host);

        var saved = Campaign.Restore(campaign.Snapshot());
        host.Dispose();
        await Exchange(() => _app.MainMenu.Visible && _app.UnsavedDialog.Visible && !_app.InSession, "the joined player returns to the menu when the connection drops");
        Check(_app.UnsavedDialog.Body.Contains("lost") && _app.State.RequiresDraft, "the joined player is told why, and keeps no copy of the campaign");
        using var resumed = new CoopHost(ENetTransport.Host(port, 1), "Dave", 2, saved);
        await Click(DialogButton("Rejoin"));
        await Exchange(() => resumed.CanStart && _app.LocalSlot == 1, "Rejoin reconnects to the leader and retakes the same seat", resumed);
        resumed.Start();
        await Exchange(() => _app.Office.Visible && _app.State.Roster is not null, "the rejoined player returns to the campaign", resumed);
        resumed.Shutdown("The leader left the game.");
        await Exchange(() => resumed.Finished, "the leader's connection closes once the other player has gone", resumed);
        await Exchange(() => _app.MainMenu.Visible && _app.UnsavedDialog.Visible && !_app.InSession, "the joined player returns to the menu when the leader leaves");
        Check(_app.UnsavedDialog.Body == "The leader left the game." && !_app.UnsavedDialog.FindChildren("*", "Button", true, false).OfType<Button>().Any(button => button.Text == "Rejoin" && button.IsInsideTree()),
            "a player whose leader ended the game is told so, with no seat to rejoin");
        await Click(DialogButton("Close"));
    }
}
