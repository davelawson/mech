using System.Text.Json.Serialization;
using Godot;

namespace Mech;

/// <summary>A battlefield order or setting from a player slot. Unit commands act only on the friendly mechs of the slot's own lance;
/// any other ids are ignored.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$command")]
[JsonDerivedType(typeof(MoveCommand), "move")]
[JsonDerivedType(typeof(AttackCommand), "attack")]
[JsonDerivedType(typeof(FollowCommand), "follow")]
[JsonDerivedType(typeof(JumpCommand), "jump")]
[JsonDerivedType(typeof(HoldCommand), "hold")]
[JsonDerivedType(typeof(PriorityCommand), "priority")]
[JsonDerivedType(typeof(MovementCommand), "movement")]
[JsonDerivedType(typeof(FiringCommand), "firing")]
[JsonDerivedType(typeof(HeatPolicyCommand), "heat-policy")]
[JsonDerivedType(typeof(DistanceCommand), "distance")]
[JsonDerivedType(typeof(WeaponCommand), "weapon")]
[JsonDerivedType(typeof(PauseCommand), "pause")]
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

public sealed record AttackCommand(string[] Units, string Target) : UnitCommand(Units)
{
    private protected override void Apply(CombatSimulation simulation, CombatUnit[] units) => simulation.Attack(Ids(units), Target);
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

public sealed record FiringCommand(string[] Units, FiringMode Mode) : UnitCommand(Units)
{
    private protected override void Apply(CombatSimulation simulation, CombatUnit[] units)
    { foreach (var unit in units.Where(item => !item.Defeated)) unit.Firing = Mode; }
}

public sealed record HeatPolicyCommand(string[] Units, HeatPolicy Policy) : UnitCommand(Units)
{
    private protected override void Apply(CombatSimulation simulation, CombatUnit[] units)
    { foreach (var unit in units.Where(item => !item.Defeated)) unit.HeatPolicy = Policy; }
}

public sealed record DistanceCommand(string[] Units, float Distance) : UnitCommand(Units)
{
    private protected override void Apply(CombatSimulation simulation, CombatUnit[] units)
    { foreach (var unit in units.Where(item => !item.Defeated)) unit.PreferredDistance = Distance; }
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
}
