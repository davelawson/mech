using Godot;

namespace Mech;

/// <summary>One Piloting check, as a shot's log reports it: the window's counted damage, the target number, the 2d6 roll and
/// whether the mech fell. <see cref="Modifiers"/> lists the leg and gyro additions to the target number.</summary>
public sealed record CombatPilotingCheck(int Damage, int Target, int Roll, bool Fell, int DamagePenalty = 0, string Modifiers = "")
{
    public string Text => $"Piloting check · {Target}+ ("
        + string.Join(", ", new[] { $"dmg +{DamagePenalty}" }.Concat(Modifiers.Length > 0 ? [Modifiers] : Array.Empty<string>()))
        + $") · rolled {Roll} · {(Fell ? "KNOCKED DOWN" : "keeps footing")}";
}

public static partial class CombatRules
{
    /// <summary>Damage counted within this many seconds can knock a mech down.</summary>
    public const double KnockdownWindowSeconds = 1;
    /// <summary>A check is made once counted damage exceeds 20% of tonnage, in tenths of a stock point.</summary>
    public static int KnockdownThreshold(int tonnage) => 2 * tonnage;
    /// <summary>Each further 10% of tonnage adds 1 to the target number.</summary>
    public static int KnockdownStep(int tonnage) => tonnage;
    public static int KnockdownPenalty(int tonnage, int damage) => Math.Max(0, (damage - KnockdownThreshold(tonnage)) / KnockdownStep(tonnage));
    public const int GyroPilotingModifier = 3, HipPilotingModifier = 2, LegActuatorPilotingModifier = 1, LostLegPilotingModifier = 5;
    /// <summary>Lasers, machine guns and flamers never knock a mech down; neither does a PPC's splash.</summary>
    public static bool CountsTowardKnockdown(WeaponKind kind) =>
        WeaponDefinitions.Of(kind).Family is not (WeaponFamily.Laser or WeaponFamily.MachineGun or WeaponFamily.Flamer);
}

public sealed partial class CombatUnit
{
    /// <summary>Knocked down: no firing, twisting, turning, moving or jumping until it stands.</summary>
    public bool Prone { get; internal set; }
    /// <summary>Seconds until a prone mech is back on its feet: its MechWarrior's Piloting, paused while shut down.</summary>
    public double StandUpRemaining { get; internal set; }
    /// <summary>Simulation times of the last fall and the last stand, for animation.</summary>
    public double? FellAt { get; internal set; }
    public double? StoodAt { get; internal set; }
    /// <summary>A Piloting check failed in the air: the mech lands prone.</summary>
    internal bool KnockdownPending { get; set; }
    private readonly List<(double Time, int Damage)> _knockdownDamage = [];

    /// <summary>Leg and gyro damage added to the Piloting target number, with the text the log shows for it.</summary>
    public (int Modifier, string Text) PilotingModifiers
    {
        get
        {
            var parts = new List<string>(); var total = 0;
            void Add(int value, string name) { if (value > 0) { total += value; parts.Add($"{name} +{value}"); } }
            Add(Levels(ComponentKind.Gyro) * CombatRules.GyroPilotingModifier, "gyro");
            Add(Levels(ComponentKind.Hip) * CombatRules.HipPilotingModifier, "hip");
            Add((Levels(ComponentKind.UpperLegActuator) + Levels(ComponentKind.LowerLegActuator) + Levels(ComponentKind.FootActuator))
                * CombatRules.LegActuatorPilotingModifier, "leg actuators");
            Add(new[] { MechLocation.LeftLeg, MechLocation.RightLeg }.Count(leg => !Intact(leg)) * CombatRules.LostLegPilotingModifier, "lost leg");
            return (total, string.Join(", ", parts));
        }
    }

    /// <summary>Adds counted damage at <paramref name="now"/>, returning the window's total once it exceeds the threshold, which
    /// clears the window. Damage to a prone or defeated mech is not counted.</summary>
    internal int? AccrueKnockdownDamage(double now, int damage)
    {
        if (damage <= 0 || Prone || KnockdownPending || Defeated) return null;
        _knockdownDamage.RemoveAll(item => item.Time <= now - CombatRules.KnockdownWindowSeconds + 1e-6);
        _knockdownDamage.Add((now, damage));
        var total = _knockdownDamage.Sum(item => item.Damage);
        if (total <= CombatRules.KnockdownThreshold(Spec.Tonnage)) return null;
        _knockdownDamage.Clear();
        return total;
    }

    /// <summary>" · Prone · stands in 3s" for a knocked-down mech, empty otherwise.</summary>
    public string StanceText() => !Prone ? ""
        : Defeated ? " · Prone" : FormattableString.Invariant($" · Prone · stands in {Math.Ceiling(StandUpRemaining):0}s")
            + (Shutdown ? " after restart" : "");

    internal void FallProne(double now)
    {
        KnockdownPending = false; Prone = true; FellAt = now; StoodAt = null;
        StandUpRemaining = Piloting; KnockedDownSinceCheck = true;
        Moving = TorsoTurning = false; CurrentSpeed = AngularVelocity = 0;
        CurrentTarget = CurrentRearTarget = null; Route = Array.Empty<Vector2>();
    }

    internal void AdvanceStandUp(double delta, double now)
    {
        if (!Prone || Defeated || Shutdown) return;
        StandUpRemaining = Math.Max(0, Math.Round(StandUpRemaining - delta, 9));
        if (StandUpRemaining > 0) return;
        Prone = false; StoodAt = now;
    }
}

public sealed partial class CombatSimulation
{
    private const int PilotingSeedSalt = 0x27d4eb2f;
    private readonly Random _pilotingRandom;
    /// <summary>Replaces the 2d6 Piloting roll, for checks that need a fall or a success.</summary>
    internal Func<int>? PilotingRollOverride { get; set; }

    /// <summary>Counts damage toward a knockdown and makes the Piloting check once it passes the threshold. A failed check in the
    /// air takes effect on landing.</summary>
    internal CombatPilotingCheck? RecordKnockdownDamage(CombatUnit target, int damage)
    {
        if (target.AccrueKnockdownDamage(Elapsed, damage) is not { } total) return null;
        var penalty = CombatRules.KnockdownPenalty(target.Spec.Tonnage, total);
        var (modifier, text) = target.PilotingModifiers;
        var number = target.Piloting + penalty + modifier;
        var roll = PilotingRollOverride?.Invoke() ?? _pilotingRandom.Next(1, 7) + _pilotingRandom.Next(1, 7);
        var fell = roll < number;
        target.Use = target.Use with { PilotingChecks = target.Use.PilotingChecks + 1, FootingKept = target.Use.FootingKept + (fell ? 0 : 1) };
        if (fell) { if (target.Airborne) target.KnockdownPending = true; else target.FallProne(Elapsed); }
        return new(total, number, roll, fell, penalty, text);
    }
}
