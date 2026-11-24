using System.Text.Json.Serialization;
using Godot;

namespace Mech;

/// <summary>A battlefield order or setting from a player slot. Unit commands act only on the friendly mechs of the slot's own lance;
/// any other ids are ignored.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$command")]
[JsonDerivedType(typeof(MoveCommand), "move")]
[JsonDerivedType(typeof(FollowCommand), "follow")]
[JsonDerivedType(typeof(JumpCommand), "jump")]
[JsonDerivedType(typeof(HoldCommand), "hold")]
[JsonDerivedType(typeof(PriorityCommand), "priority")]
[JsonDerivedType(typeof(MovementCommand), "movement")]
[JsonDerivedType(typeof(SpeedCommand), "speed")]
[JsonDerivedType(typeof(FiringCommand), "firing")]
[JsonDerivedType(typeof(HeatLimitCommand), "heat-limit")]
[JsonDerivedType(typeof(AmmoEconomyCommand), "ammo-economy")]
[JsonDerivedType(typeof(DistanceCommand), "distance")]
[JsonDerivedType(typeof(MaintainRangeCommand), "maintain-range")]
[JsonDerivedType(typeof(WeaponCommand), "weapon")]
[JsonDerivedType(typeof(PauseCommand), "pause")]
[JsonDerivedType(typeof(TimeScaleCommand), "time-scale")]
[JsonDerivedType(typeof(EjectCommand), "eject")]
[JsonDerivedType(typeof(SurrenderAnswerCommand), "surrender-answer")]
[JsonDerivedType(typeof(DropSiteCommand), "drop-site")]
[JsonDerivedType(typeof(DropReadyCommand), "drop-ready")]
[JsonDerivedType(typeof(ForceDropCommand), "force-drop")]
public abstract record CombatCommand
{
    internal abstract void Apply(CombatSimulation simulation, int slot);
}

public abstract record UnitCommand(string[] Units) : CombatCommand
{
    internal sealed override void Apply(CombatSimulation simulation, int slot) =>
        Apply(simulation, (Units ?? []).Distinct().Select(id => simulation.Units.FirstOrDefault(unit => unit.Id == id)).OfType<CombatUnit>()
            .Where(unit => simulation.Commands(slot, unit)).ToArray());
    private protected abstract void Apply(CombatSimulation simulation, CombatUnit[] units);
    private protected static IEnumerable<string> Ids(CombatUnit[] units) => units.Select(unit => unit.Id);
}

public sealed record MoveCommand(string[] Units, Vector2 Destination) : UnitCommand(Units)
{
    private protected override void Apply(CombatSimulation simulation, CombatUnit[] units) => simulation.Move(Ids(units), Destination);
}

public sealed record FollowCommand(string[] Units, string Target) : UnitCommand(Units)
{
    private protected override void Apply(CombatSimulation simulation, CombatUnit[] units) => simulation.Follow(Ids(units), Target);
}

public sealed record JumpCommand(string[] Units, Vector2 Point) : UnitCommand(Units)
{
    private protected override void Apply(CombatSimulation simulation, CombatUnit[] units) => simulation.Jump(Ids(units), Point);
}

public sealed record HoldCommand(string[] Units) : UnitCommand(Units)
{
    private protected override void Apply(CombatSimulation simulation, CombatUnit[] units) => simulation.Hold(Ids(units));
}

/// <summary>Sets the priority target, or clears it (null).</summary>
public sealed record PriorityCommand(string[] Units, string? Target) : UnitCommand(Units)
{
    private protected override void Apply(CombatSimulation simulation, CombatUnit[] units) => simulation.SetPriority(Ids(units), Target);
}

public sealed record MovementCommand(string[] Units, MovementMode Mode) : UnitCommand(Units)
{
    private protected override void Apply(CombatSimulation simulation, CombatUnit[] units)
    { foreach (var unit in units.Where(item => !item.Defeated)) unit.Movement = Mode; }
}

/// <summary>Sets the desired speed in metres per second, from 10 km/h up to each mech's unheated running speed.</summary>
public sealed record SpeedCommand(string[] Units, float Speed) : UnitCommand(Units)
{
    private protected override void Apply(CombatSimulation simulation, CombatUnit[] units)
    {
        foreach (var unit in units.Where(item => !item.Defeated))
            unit.DesiredSpeed = Math.Clamp(Speed, CombatRules.MinimumDesiredSpeed, Math.Max(CombatRules.MinimumDesiredSpeed, unit.UnheatedRunningSpeed));
    }
}

public sealed record FiringCommand(string[] Units, FiringMode Mode) : UnitCommand(Units)
{
    private protected override void Apply(CombatSimulation simulation, CombatUnit[] units)
    { foreach (var unit in units.Where(item => !item.Defeated)) unit.Firing = Mode; }
}

public sealed record HeatLimitCommand(string[] Units, int Limit) : UnitCommand(Units)
{
    private protected override void Apply(CombatSimulation simulation, CombatUnit[] units)
    { foreach (var unit in units.Where(item => !item.Defeated)) unit.HeatLimit = Limit; }
}

/// <summary>Sets how sparingly ammo-fed weapons fire, 0 (freely) to 10 (only shots without penalty modifiers).</summary>
public sealed record AmmoEconomyCommand(string[] Units, int Economy) : UnitCommand(Units)
{
    private protected override void Apply(CombatSimulation simulation, CombatUnit[] units)
    { foreach (var unit in units.Where(item => !item.Defeated)) unit.AmmoEconomy = Economy; }
}

public sealed record DistanceCommand(string[] Units, float Distance) : UnitCommand(Units)
{
    private protected override void Apply(CombatSimulation simulation, CombatUnit[] units)
    {
        // Choosing a range means keeping it.
        foreach (var unit in units.Where(item => !item.Defeated)) { unit.PreferredDistance = Distance; unit.MaintainRange = true; }
    }
}

public sealed record MaintainRangeCommand(string[] Units, bool Maintain) : UnitCommand(Units)
{
    private protected override void Apply(CombatSimulation simulation, CombatUnit[] units)
    { foreach (var unit in units.Where(item => !item.Defeated)) unit.MaintainRange = Maintain; }
}

/// <summary>Turns one weapon of a mech on or off, by its index in the mech's weapon list.</summary>
public sealed record WeaponCommand(string Unit, int Weapon, bool Enabled) : UnitCommand([Unit])
{
    private protected override void Apply(CombatSimulation simulation, CombatUnit[] units)
    { foreach (var unit in units.Where(item => Weapon >= 0 && Weapon < item.Weapons.Count)) unit.Weapons[Weapon].Enabled = Enabled; }
}

/// <summary>Turns the sending player's pause toggle on or off. The battle runs only while every toggle is off.</summary>
public sealed record PauseCommand(bool Paused) : CombatCommand
{
    internal override void Apply(CombatSimulation simulation, int slot) => simulation.HoldPause(slot, Paused);
}

/// <summary>Picks the sending player's battle speed, 1x or 4x. The battle runs at 4x only while every player has picked it;
/// any other speed is ignored.</summary>
public sealed record TimeScaleCommand(double Scale) : CombatCommand
{
    internal override void Apply(CombatSimulation simulation, int slot) => simulation.PickSpeed(slot, Scale);
}

/// <summary>Orders the MechWarriors of the given mechs to eject: the pilot survives, injured, and the mech is left disabled.</summary>
public sealed record EjectCommand(string[] Units) : UnitCommand(Units)
{
    private protected override void Apply(CombatSimulation simulation, CombatUnit[] units)
    { foreach (var unit in units) simulation.EjectOrdered(unit); }
}

/// <summary>The leader's answer to the oldest waiting surrender. Other players' answers are ignored.</summary>
public sealed record SurrenderAnswerCommand(string Unit, bool Accept) : CombatCommand
{
    internal override void Apply(CombatSimulation simulation, int slot)
    {
        if (slot == Campaign.LeaderSlot && simulation.SurrenderOffers.Contains(Unit)) simulation.AnswerSurrender(Unit, Accept);
    }
}

/// <summary>Chooses or moves the sending player's drop site during the drop phase.</summary>
public sealed record DropSiteCommand(Vector2 Point) : CombatCommand
{
    internal override void Apply(CombatSimulation simulation, int slot) => simulation.ChooseDropSite(slot, Point);
}

/// <summary>Confirms the sending player's drop site, or reopens it to move it again.</summary>
public sealed record DropReadyCommand(bool Ready) : CombatCommand
{
    internal override void Apply(CombatSimulation simulation, int slot) => simulation.ReadyDrop(slot, Ready);
}

/// <summary>The leader ends the drop phase: every lance without a site drops at a random valid one.</summary>
public sealed record ForceDropCommand : CombatCommand
{
    internal override void Apply(CombatSimulation simulation, int slot) => simulation.ForceDrop(slot);
}

public sealed partial class CombatSimulation
{
    /// <summary>Carries out a player slot's command.</summary>
    public void Execute(CombatCommand command, int slot = Campaign.LeaderSlot)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (Replica) throw new InvalidOperationException("Orders go to the leader's battle.");
        if (!Players.Contains(slot)) throw new ArgumentOutOfRangeException(nameof(slot), "That player has no lance in this battle.");
        command.Apply(this, slot);
    }
    /// <summary>Whether a player slot commands a unit: the friendly mechs of its own lance.</summary>
    public bool Commands(int slot, CombatUnit unit) => unit.Side == CombatSide.Friendly && unit.Lance == slot;
    internal void HoldPause(int slot, bool paused)
    {
        if (Outcome != CombatOutcome.Ongoing) return;
        if (paused) _pauseHolds.Add(slot); else _pauseHolds.Remove(slot);
    }
    internal void PickSpeed(int slot, double scale)
    {
        if (Outcome != CombatOutcome.Ongoing || scale is not (1 or FastTimeScale)) return;
        if (scale == FastTimeScale) _fastPicks.Add(slot); else _fastPicks.Remove(slot);
    }
}
