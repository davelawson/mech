using System.Text.Json.Serialization;

namespace Mech;

/// <summary>A change a player slot asks of the campaign. Every screen changes the campaign through commands, so the same rules hold
/// whichever player sends one. Leader-only commands cover travel, contracts, time and company finances; the rest are open to every
/// player but act only on mechs the slot controls: its own lance and the lance pool.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$command")]
[JsonDerivedType(typeof(AcceptMissionCommand), "accept-mission")]
[JsonDerivedType(typeof(AbandonMissionCommand), "abandon-mission")]
[JsonDerivedType(typeof(PlotJourneyCommand), "plot-journey")]
[JsonDerivedType(typeof(EmbarkCommand), "embark")]
[JsonDerivedType(typeof(CancelJourneyCommand), "cancel-journey")]
[JsonDerivedType(typeof(AdvanceDayCommand), "advance-day")]
[JsonDerivedType(typeof(PayNowCommand), "pay-now")]
[JsonDerivedType(typeof(DeferPaymentCommand), "defer-payment")]
[JsonDerivedType(typeof(SettleMissionCommand), "settle-mission")]
[JsonDerivedType(typeof(ApplyLoadoutCommand), "apply-loadout")]
[JsonDerivedType(typeof(AssignWarriorCommand), "assign-warrior")]
[JsonDerivedType(typeof(SetLanceCommand), "set-lance")]
public abstract record CampaignCommand
{
    [JsonIgnore] public virtual bool LeaderOnly => false;
    internal abstract object? Run(Campaign campaign, int slot);
}

/// <summary>A command whose result the sender is shown, such as the events a day brings.</summary>
public abstract record CampaignCommand<TResult> : CampaignCommand
{
    internal sealed override object? Run(Campaign campaign, int slot) => Apply(campaign, slot);
    internal abstract TResult Apply(Campaign campaign, int slot);
}

public sealed record AcceptMissionCommand(string MissionId) : CampaignCommand
{
    public override bool LeaderOnly => true;
    internal override object? Run(Campaign campaign, int slot) { campaign.AcceptMission(MissionId); return null; }
}

public sealed record AbandonMissionCommand : CampaignCommand
{
    public override bool LeaderOnly => true;
    internal override object? Run(Campaign campaign, int slot) { campaign.Missions.Abandon(); return null; }
}

/// <summary>Plots a route from the orbited system; the result says whether one was found.</summary>
public sealed record PlotJourneyCommand(string SystemId) : CampaignCommand<bool>
{
    public override bool LeaderOnly => true;
    internal override bool Apply(Campaign campaign, int slot) => campaign.PlotJourney(SystemId);
}

public sealed record EmbarkCommand : CampaignCommand
{
    public override bool LeaderOnly => true;
    internal override object? Run(Campaign campaign, int slot) { campaign.Embark(); return null; }
}

public sealed record CancelJourneyCommand : CampaignCommand
{
    public override bool LeaderOnly => true;
    internal override object? Run(Campaign campaign, int slot) { campaign.CancelJourney(); return null; }
}

public sealed record AdvanceDayCommand : CampaignCommand<DayResult>
{
    public override bool LeaderOnly => true;
    internal override DayResult Apply(Campaign campaign, int slot) => campaign.AdvanceOneDay();
}

public sealed record PayNowCommand : CampaignCommand<IReadOnlyList<CampaignEvent>>
{
    public override bool LeaderOnly => true;
    internal override IReadOnlyList<CampaignEvent> Apply(Campaign campaign, int slot) => campaign.PayNow();
}

public sealed record DeferPaymentCommand : CampaignCommand<IReadOnlyList<CampaignEvent>>
{
    public override bool LeaderOnly => true;
    internal override IReadOnlyList<CampaignEvent> Apply(Campaign campaign, int slot) => campaign.DeferPayment();
}

/// <summary>Settles the current mission from its battle; only the leader's machine runs the battle, so only it reports.</summary>
public sealed record SettleMissionCommand(MissionReport Report) : CampaignCommand<MissionSettlement>
{
    public override bool LeaderOnly => true;
    internal override MissionSettlement Apply(Campaign campaign, int slot) => campaign.SettleMission(Report);
}

/// <summary>Applies a mech's loadout, paying for the items it buys from the company's cash.</summary>
public sealed record ApplyLoadoutCommand(string MechId, LoadoutPlan Plan, string[] Bought) : CampaignCommand
{
    internal override object? Run(Campaign campaign, int slot)
    {
        Campaign.RequireControl(campaign.RequireRoster(), slot, MechId);
        campaign.ApplyLoadout(MechId, Plan, Bought);
        return null;
    }
}

/// <summary>Assigns a MechWarrior to a mech, or unassigns it (null). Moving a MechWarrior from another mech changes that mech too,
/// so the slot must control both.</summary>
public sealed record AssignWarriorCommand(string MechId, string? WarriorId, bool ConfirmSwap = false) : CampaignCommand
{
    internal override object? Run(Campaign campaign, int slot)
    {
        var roster = campaign.RequireRoster();
        Campaign.RequireControl(roster, slot, MechId);
        if (WarriorId is not null && roster.Assignment(WarriorId) is { } previous) Campaign.RequireControl(roster, slot, previous.Id);
        roster.Assign(MechId, WarriorId, ConfirmSwap);
        return null;
    }
}

/// <summary>Moves a mech between the slot's own lance and the lance pool (null).</summary>
public sealed record SetLanceCommand(string MechId, int? Lance) : CampaignCommand
{
    internal override object? Run(Campaign campaign, int slot)
    {
        var roster = campaign.RequireRoster();
        Campaign.RequireControl(roster, slot, MechId);
        if (Lance is { } lance && lance != slot) throw new InvalidOperationException("A player can only move mechs into their own lance.");
        roster.SetLance(MechId, Lance);
        return null;
    }
}
